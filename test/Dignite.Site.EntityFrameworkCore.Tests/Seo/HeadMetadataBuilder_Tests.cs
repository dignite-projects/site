using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Routing;
using Dignite.Site.Settings;
using Shouldly;
using Xunit;

namespace Dignite.Site.Seo;

/// <summary>
/// <see cref="HeadMetadataBuilder"/> end to end, against the seeded scenario (总体设计 §5.3, §5.4, §5.5,
/// §5.9 - GitHub issues #13, #16, #17, #20).
/// </summary>
public class HeadMetadataBuilder_Tests : SiteEntityFrameworkCoreTestBase
{
    private const string BaseUrl = "https://acme.example";

    private readonly HeadMetadataBuilder _builder;
    private readonly SiteRouteResolver _routeResolver;
    private readonly IFieldRepository _fieldRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentRepository _contentRepository;
    private readonly IPageRepository _pageRepository;
    private readonly ContentTypeManager _contentTypeManager;
    private readonly ContentManager _contentManager;
    private readonly PageManager _pageManager;
    private readonly TestSettingValueProvider _settings;

    public HeadMetadataBuilder_Tests()
    {
        _builder = GetRequiredService<HeadMetadataBuilder>();
        _routeResolver = GetRequiredService<SiteRouteResolver>();
        _fieldRepository = GetRequiredService<IFieldRepository>();
        _contentTypeRepository = GetRequiredService<IContentTypeRepository>();
        _contentRepository = GetRequiredService<IContentRepository>();
        _pageRepository = GetRequiredService<IPageRepository>();
        _contentTypeManager = GetRequiredService<ContentTypeManager>();
        _contentManager = GetRequiredService<ContentManager>();
        _pageManager = GetRequiredService<PageManager>();
        _settings = GetRequiredService<TestSettingValueProvider>();

        _settings.Set(SiteSettings.PrimaryDomain, BaseUrl);

        // The seeded scenario has bilingual content (the About page is en + zh-Hans), but the setting's
        // own default is "en" alone - so without this the seed describes a site serving a language it does
        // not actually enable, and every zh-Hans URL it advertises would 404 on the way back in.
        _settings.Set(SiteSettings.EnabledLanguages, $"{SiteTestData.EnglishCulture},{SiteTestData.ChineseCulture}");
    }

    [Fact]
    public async Task Title_And_Description_Should_Match_ContentSummaryResolver_For_A_Content_Match()
    {
        var metadata = await BuildAsync("/blog/my-trip", SiteTestData.EnglishCulture);

        // post-article never pulled in the SEO field, so the fallback chain lands on the first two text
        // fields in order: title supplies the title, body supplies the description.
        metadata.Title.ShouldBe("My trip");
        metadata.Description.ShouldBe("Trip body");
    }

    [Fact]
    public async Task Title_Should_Be_The_Page_DisplayName_For_A_Bare_Page_Match()
    {
        var metadata = await BuildAsync("/blog", SiteTestData.EnglishCulture);

        metadata.Title.ShouldBe("Blog");
        metadata.Description.ShouldBeNull();
    }

    [Fact]
    public async Task CanonicalUrl_Should_Match_SiteUrlBuilder_For_Every_Route_Kind()
    {
        (await BuildAsync("/", SiteTestData.EnglishCulture)).CanonicalUrl.ShouldBe($"{BaseUrl}/");
        (await BuildAsync("/about", SiteTestData.EnglishCulture)).CanonicalUrl.ShouldBe($"{BaseUrl}/about");
        (await BuildAsync("/blog", SiteTestData.EnglishCulture)).CanonicalUrl.ShouldBe($"{BaseUrl}/blog");
        (await BuildAsync("/blog/my-trip", SiteTestData.EnglishCulture)).CanonicalUrl
            .ShouldBe($"{BaseUrl}/blog/my-trip");
    }

    [Fact]
    public async Task NoIndex_Should_Be_False_By_Default()
    {
        var metadata = await BuildAsync("/blog/my-trip", SiteTestData.EnglishCulture);

        metadata.NoIndex.ShouldBeFalse();
    }

    [Fact]
    public async Task NoIndex_Should_Be_True_When_The_SEO_Field_Says_So()
    {
        var contentTypeId = await CreateSeoEnabledContentTypeAsync();
        var slug = $"noindexed-{Guid.NewGuid():N}";

        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?>
            {
                ["title"] = "Noindexed",
                [SeoFieldNames.FieldName] = new SeoFieldValue { NoIndex = true }
            }));

        var metadata = await BuildAsync($"/blog/{slug}", SiteTestData.EnglishCulture);

        metadata.NoIndex.ShouldBeTrue();
    }

    /// <summary>
    /// The concrete discharge of <c>SiteRouteResolver.ResolveAsync</c>'s documented obligation: a caller
    /// that resolved with <c>includeUnpublished: true</c> must force noindex on the response, since an
    /// indexable preview is duplicate content against the real URL (GitHub issue #16).
    /// </summary>
    [Fact]
    public async Task NoIndex_Should_Be_Forced_True_For_A_Preview_Regardless_Of_The_Contents_Own_Flag()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _routeResolver.ResolveAsync("/blog/my-trip", SiteTestData.EnglishCulture));

        match.IsMatch.ShouldBeTrue();

        var metadata = await WithUnitOfWorkAsync(() =>
            _builder.BuildAsync(match, SiteTestData.EnglishCulture, includeUnpublished: true));

        metadata.NoIndex.ShouldBeTrue();
    }

    /// <summary>
    /// The whole point of keeping an archived content routable: unlike a preview, this is ordinary public
    /// traffic, so it must not be force-noindexed the way the preview above is - an already-indexed URL
    /// stays indexed once archived, rather than being yanked out from under it.
    /// </summary>
    [Fact]
    public async Task NoIndex_Should_Not_Be_Forced_True_For_An_Archived_Content()
    {
        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            SiteTestData.PostArticleTypeId, SiteTestData.EnglishCulture, "archived-noindex-test",
            SiteTestData.PublishTime, ContentStatus.Archived,
            new Dictionary<string, object?> { ["title"] = "Archived noindex test" }));

        var metadata = await BuildAsync("/blog/archived-noindex-test", SiteTestData.EnglishCulture);

        metadata.NoIndex.ShouldBeFalse();
    }

    /// <summary>
    /// A partial match (Kind is Page, FilterValues non-empty - e.g. "/news/2026-07" against
    /// "/news/{publishTime:yyyy-MM}/{slug}") has no URL of its own: PageRoute can only render the page's
    /// bare address, which is a different resource than what was actually requested. Reusing that bare
    /// address as this response's canonical without also marking it noindex would tell search engines the
    /// filtered view IS the canonical page - the standard faceted-navigation fix instead points canonical
    /// at the unfiltered page (which a bare match already does for free) and keeps the filtered variant
    /// itself out of results.
    /// </summary>
    [Fact]
    public async Task NoIndex_Should_Be_True_For_A_Partial_Match()
    {
        var match = await WithUnitOfWorkAsync(() =>
            _routeResolver.ResolveAsync("/news/2026-07", SiteTestData.EnglishCulture));

        match.Kind.ShouldBe(RouteMatchKind.Page);
        match.FilterValues.ShouldNotBeEmpty();

        var metadata = await WithUnitOfWorkAsync(() =>
            _builder.BuildAsync(match, SiteTestData.EnglishCulture));

        metadata.NoIndex.ShouldBeTrue();
        metadata.CanonicalUrl.ShouldBe($"{BaseUrl}/news");
    }

    [Fact]
    public async Task OgImageUrl_Should_Be_Null_When_Unset()
    {
        var metadata = await BuildAsync("/blog/my-trip", SiteTestData.EnglishCulture);

        metadata.OgImageUrl.ShouldBeNull();
    }

    [Fact]
    public async Task OgImageUrl_Should_Be_Read_From_The_SEO_Field_When_Set()
    {
        var contentTypeId = await CreateSeoEnabledContentTypeAsync();
        var slug = $"og-image-{Guid.NewGuid():N}";

        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?>
            {
                ["title"] = "Has an image",
                [SeoFieldNames.FieldName] = new SeoFieldValue { OgImage = "https://acme.example/share.jpg" }
            }));

        var metadata = await BuildAsync($"/blog/{slug}", SiteTestData.EnglishCulture);

        metadata.OgImageUrl.ShouldBe("https://acme.example/share.jpg");
    }

    /// <summary>
    /// A cleared-but-not-removed value: an author fills in og:image, then deletes the text, leaving an
    /// empty string rather than a null. <c>SeoFieldType.Validate</c> requires nothing inside the bundle, so
    /// this reaches storage - and must be treated the same as unset, matching
    /// <c>ContentSummaryResolver</c>'s own <c>NullIfBlank</c> handling of the sibling MetaTitle/MetaDescription
    /// fields, or SeoTags rejects the blank string outright when it reaches <c>SetImageInfo</c>.
    /// </summary>
    [Fact]
    public async Task OgImageUrl_Should_Be_Null_When_The_SEO_Field_Holds_Only_Whitespace()
    {
        var contentTypeId = await CreateSeoEnabledContentTypeAsync();
        var slug = $"og-image-blank-{Guid.NewGuid():N}";

        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?>
            {
                ["title"] = "Blank image",
                [SeoFieldNames.FieldName] = new SeoFieldValue { OgImage = "   " }
            }));

        var metadata = await BuildAsync($"/blog/{slug}", SiteTestData.EnglishCulture);

        metadata.OgImageUrl.ShouldBeNull();
    }

    /// <summary>Same fail-open stance as <c>NoIndexRecognizer</c>: one corrupted value must not blank out the whole page.</summary>
    [Fact]
    public async Task OgImageUrl_Should_Degrade_To_Null_On_An_Unreadable_SEO_Value()
    {
        var contentTypeId = await CreateSeoEnabledContentTypeAsync();
        var slug = $"og-image-malformed-{Guid.NewGuid():N}";

        var contentId = await WithUnitOfWorkAsync(async () =>
        {
            var content = await _contentManager.CreateAsync(
                contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
                new Dictionary<string, object?> { ["title"] = "Malformed" });

            content.SetField(SeoFieldNames.FieldName, "not-an-seo-object");
            await _contentRepository.UpdateAsync(content, autoSave: true);
            return content.Id;
        });
        contentId.ShouldNotBe(Guid.Empty);

        var metadata = await BuildAsync($"/blog/{slug}", SiteTestData.EnglishCulture);

        metadata.OgImageUrl.ShouldBeNull();
    }

    [Fact]
    public async Task Hreflang_Should_Cover_Both_Languages_Of_A_Bilingual_Content_And_Include_Itself()
    {
        var metadata = await BuildAsync("/about", SiteTestData.EnglishCulture);

        metadata.HreflangAlternates.Select(a => a.CultureName).OrderBy(c => c)
            .ShouldBe(new[] { "en", "zh-Hans" });
        metadata.HreflangAlternates.Single(a => a.CultureName == "en").Url.ShouldBe($"{BaseUrl}/about");
        metadata.HreflangAlternates.Single(a => a.CultureName == "zh-Hans").Url.ShouldBe($"{BaseUrl}/zh-Hans/about");
    }

    [Fact]
    public async Task Hreflang_Should_Be_Self_Referencing_Only_For_A_Single_Language_Content()
    {
        var metadata = await BuildAsync("/blog/my-trip", SiteTestData.EnglishCulture);

        metadata.HreflangAlternates.Count.ShouldBe(1);
        metadata.HreflangAlternates.Single().CultureName.ShouldBe("en");
        metadata.HreflangAlternates.Single().Url.ShouldBe($"{BaseUrl}/blog/my-trip");
    }

    /// <summary>
    /// A bare page match has no translation rows of its own (Page carries no CultureName) - its language
    /// footprint instead mirrors <c>SitemapEntrySource.CollectForPageAsync</c>'s own rule: the default
    /// language always, plus any other language that actually has published content beneath the page.
    /// </summary>
    [Fact]
    public async Task Hreflang_For_A_Bare_Page_Match_Should_Mirror_The_Sitemaps_Language_Rule()
    {
        var pageId = await WithUnitOfWorkAsync(async () =>
            (await _pageManager.CreateAsync("bilingual-list", "Bilingual List", "/bilingual-list")).Id);

        var contentTypeId = await WithUnitOfWorkAsync(async () =>
        {
            var titleField = await _fieldRepository.GetAsync(SiteTestData.TitleFieldId);
            var contentType = await _contentTypeManager.CreateAsync(
                pageId, "bilingual-item", "Bilingual item",
                fields: new[] { new ContentTypeField(titleField.Id, order: 0) });
            return contentType.Id;
        });

        // Empty slug: "/bilingual-list" carries no {slug}/{slug?} placeholder at all, so this page has
        // nothing beneath it but its own address (总体设计 §3.3) - these are its own content, translated.
        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, "", SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "First" }));

        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            contentTypeId, SiteTestData.ChineseCulture, "", SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "第一条" }));

        var metadata = await BuildAsync("/bilingual-list", SiteTestData.EnglishCulture);

        metadata.HreflangAlternates.Select(a => a.CultureName).OrderBy(c => c)
            .ShouldBe(new[] { "en", "zh-Hans" });
        metadata.HreflangAlternates.Single(a => a.CultureName == "en").Url.ShouldBe($"{BaseUrl}/bilingual-list");
        metadata.HreflangAlternates.Single(a => a.CultureName == "zh-Hans").Url
            .ShouldBe($"{BaseUrl}/zh-Hans/bilingual-list");
    }

    /// <summary>
    /// The route is being viewed in this language right now, so it belongs in its own alternate set even
    /// when nothing under the page happens to be published in it yet - all of /blog's seeded content is
    /// English, but viewing it in zh-Hans must still self-reference, or the set is not self-referencing at
    /// all and search engines discard it entirely (总体设计 §5.5).
    /// </summary>
    [Fact]
    public async Task Hreflang_For_A_Bare_Page_Match_Should_Include_The_Current_Language_Even_With_No_Content_In_It()
    {
        var metadata = await BuildAsync("/blog", SiteTestData.ChineseCulture);

        metadata.HreflangAlternates.Select(a => a.CultureName).ShouldContain("zh-Hans");
        metadata.HreflangAlternates.Single(a => a.CultureName == "zh-Hans").Url
            .ShouldBe($"{BaseUrl}/zh-Hans/blog");
    }

    /// <summary>
    /// A translation an author marked noindex must not be advertised as an alternate either, or hreflang
    /// points at a URL the sitemap (<c>SitemapEntrySource</c>) deliberately excludes - the exact "two
    /// derived SEO artifacts disagree" trap <c>HeadMetadataBuilder</c>'s own doc warns about.
    /// </summary>
    [Fact]
    public async Task Hreflang_Should_Exclude_A_Noindexed_Translation()
    {
        var (slug, _) = await CreateBilingualContentWithNoindexedChineseAsync();

        var metadata = await BuildAsync($"/blog/{slug}", SiteTestData.EnglishCulture);

        metadata.HreflangAlternates.Select(a => a.CultureName).ShouldBe(new[] { "en" });
    }

    /// <summary>
    /// The noindex filter must not remove the row being rendered. A set that omits its own page is not
    /// self-referencing, and a non-self-referencing hreflang cluster is discarded wholesale - which would
    /// invalidate the *sibling* language's annotations too, not just this page's.
    /// </summary>
    [Fact]
    public async Task Hreflang_Should_Still_Self_Reference_When_The_Rendered_Content_Is_Itself_Noindexed()
    {
        var (slug, _) = await CreateBilingualContentWithNoindexedChineseAsync();

        // The path carries no language prefix - SiteRouteResolver takes the culture as its own parameter
        // and never parses one out of the path (that is SiteUrlContext.TryStripCulturePrefix's job, one
        // layer up).
        var metadata = await BuildAsync($"/blog/{slug}", SiteTestData.ChineseCulture);

        metadata.HreflangAlternates.Select(a => a.CultureName).ShouldContain("zh-Hans");
        metadata.HreflangAlternates.Single(a => a.CultureName == "zh-Hans").Url
            .ShouldBe($"{BaseUrl}/zh-Hans/blog/{slug}");
    }

    /// <summary>
    /// The same broadening as the detail route itself: a sibling translation that is archived rather than
    /// noindexed is still reachable, so hreflang must keep pointing at it - the opposite expectation from
    /// <see cref="Hreflang_Should_Exclude_A_Noindexed_Translation"/>.
    /// </summary>
    [Fact]
    public async Task Hreflang_Should_Include_An_Archived_Translation()
    {
        var slug = await CreateBilingualContentWithArchivedChineseAsync();

        var metadata = await BuildAsync($"/blog/{slug}", SiteTestData.EnglishCulture);

        metadata.HreflangAlternates.Select(a => a.CultureName).OrderBy(c => c)
            .ShouldBe(new[] { "en", "zh-Hans" });
    }

    /// <summary>
    /// A language the tenant does not serve must never reach an alternate. <c>SiteUrlContext</c> refuses to
    /// strip a non-enabled culture prefix on the way back in, so advertising one means publishing a URL the
    /// site then 404s on - the exact drift its two-direction design exists to prevent.
    /// </summary>
    [Fact]
    public async Task Hreflang_Should_Not_Advertise_A_Language_The_Site_Does_Not_Serve()
    {
        // The seeded default is "en"; "fr" is a perfectly valid culture name that this tenant never enabled.
        var metadata = await BuildAsync("/blog", "fr");

        metadata.HreflangAlternates.Select(a => a.CultureName).ShouldNotContain("fr");
        metadata.HreflangAlternates.Select(a => a.CultureName).ShouldContain("en");
    }

    [Fact]
    public async Task XDefaultUrl_Should_Point_At_The_Seeded_Home_Page()
    {
        var metadata = await BuildAsync("/blog/my-trip", SiteTestData.EnglishCulture);

        metadata.XDefaultUrl.ShouldBe($"{BaseUrl}/");
    }

    [Fact]
    public async Task XDefaultUrl_Should_Be_Null_When_No_Page_Is_The_Home_Page()
    {
        // Being the home page is derived from Route now, not a flag that can be cleared while Route stays
        // "/" - the only way for no page to be the home page is for no page's own address to be the root,
        // so this moves the seeded home page's route away from it instead.
        await WithUnitOfWorkAsync(async () =>
        {
            var home = await _pageRepository.GetAsync(SiteTestData.HomePageId);
            await _pageManager.UpdateAsync(home, home.Name, home.DisplayName, "/home-moved");
        });

        var metadata = await BuildAsync("/blog/my-trip", SiteTestData.EnglishCulture);

        metadata.XDefaultUrl.ShouldBeNull();
    }

    /// <summary>
    /// A deactivated page is not routable - <c>SiteRouteResolver</c> 404s it and the sitemap drops it. An
    /// <c>x-default</c> still pointing there would have two derived SEO artifacts contradicting each other
    /// about the same URL, and an x-default pointing at a 404 invalidates the whole hreflang cluster.
    /// </summary>
    [Fact]
    public async Task XDefaultUrl_Should_Ignore_A_Deactivated_Home_Page()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var home = await _pageRepository.GetAsync(SiteTestData.HomePageId);
            await _pageManager.UpdateAsync(home, home.Name, home.DisplayName, home.Route, isActive: false);
        });

        var metadata = await BuildAsync("/blog/my-trip", SiteTestData.EnglishCulture);

        metadata.XDefaultUrl.ShouldBeNull();
    }

    private async Task<HeadMetadata> BuildAsync(string path, string cultureName)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var match = await _routeResolver.ResolveAsync(path, cultureName);
            match.IsMatch.ShouldBeTrue($"expected '{path}' to resolve");
            return await _builder.BuildAsync(match, cultureName);
        });
    }

    /// <summary>
    /// One content in two languages sharing a slug, where the zh-Hans row is marked noindex. Returns the
    /// slug and the content type id, both already committed.
    /// </summary>
    private async Task<(string Slug, Guid ContentTypeId)> CreateBilingualContentWithNoindexedChineseAsync()
    {
        var seoField = await WithUnitOfWorkAsync(() => _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName));

        var contentTypeId = await WithUnitOfWorkAsync(async () =>
        {
            var titleField = await _fieldRepository.GetAsync(SiteTestData.TitleFieldId);
            var contentType = await _contentTypeManager.CreateAsync(
                SiteTestData.BlogPageId, $"noindex-hreflang-{Guid.NewGuid():N}", "Noindex hreflang test",
                fields: new[]
                {
                    new ContentTypeField(titleField.Id, order: 0),
                    new ContentTypeField(seoField!.Id, order: 1)
                });
            return contentType.Id;
        });

        var slug = $"bilingual-{Guid.NewGuid():N}";

        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            contentTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "English" }));

        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            contentTypeId, SiteTestData.ChineseCulture, slug, SiteTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?>
            {
                ["title"] = "中文",
                [SeoFieldNames.FieldName] = new SeoFieldValue { NoIndex = true }
            }));

        return (slug, contentTypeId);
    }

    /// <summary>One content in two languages sharing a slug, where the zh-Hans row is archived rather than published.</summary>
    private async Task<string> CreateBilingualContentWithArchivedChineseAsync()
    {
        var slug = $"bilingual-archived-{Guid.NewGuid():N}";

        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            SiteTestData.PostArticleTypeId, SiteTestData.EnglishCulture, slug, SiteTestData.PublishTime,
            ContentStatus.Published, new Dictionary<string, object?> { ["title"] = "English" }));

        await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
            SiteTestData.PostArticleTypeId, SiteTestData.ChineseCulture, slug, SiteTestData.PublishTime,
            ContentStatus.Archived, new Dictionary<string, object?> { ["title"] = "中文" }));

        return slug;
    }

    /// <summary>Returns the new content type's id, already committed in its own unit of work.</summary>
    private async Task<Guid> CreateSeoEnabledContentTypeAsync()
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var seoField = await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);
            seoField.ShouldNotBeNull();

            var titleField = await _fieldRepository.GetAsync(SiteTestData.TitleFieldId);

            var contentType = await _contentTypeManager.CreateAsync(
                SiteTestData.BlogPageId, $"head-metadata-seo-test-{Guid.NewGuid():N}", "SEO test type",
                fields: new[]
                {
                    new ContentTypeField(titleField.Id, required: true, order: 0),
                    new ContentTypeField(seoField!.Id, order: 1)
                });

            return contentType.Id;
        });
    }
}

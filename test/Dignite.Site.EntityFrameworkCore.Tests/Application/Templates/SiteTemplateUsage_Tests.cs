using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Pages;
using Dignite.Site.Templates;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dignite.Site.Admin.Templates;

/// <summary>
/// Who uses a template (<see cref="ISiteTemplateAdminAppService.GetUsageAsync"/>), the counts the list shows
/// and the pages no published template renders. The seeded pages are home (<c>/</c>), about (<c>/about</c>),
/// blog (<c>/blog/{slug?}</c>) and news (<c>/news/{publishTime:yyyy-MM}/{slug}</c>), all on the
/// <c>Default</c> template, which no seeded template is called; each test points the pages it needs at its own
/// templates.
/// </summary>
public class SiteTemplateUsage_Tests : SiteEntityFrameworkCoreTestBase
{
    private const string PageTemplateName = "Usage/Page";
    private const string PartialName = "partials/usage-card";
    private const string OuterPartialName = "partials/usage-outer";
    private const string AssetName = "assets/usage.css";

    private const string LayoutBody = "<html><head>{{ site_head }}</head><body>{{ body }}</body></html>";

    private readonly ISiteTemplateAdminAppService _templateAppService;
    private readonly IPageRepository _pageRepository;

    public SiteTemplateUsage_Tests()
    {
        _templateAppService = GetRequiredService<ISiteTemplateAdminAppService>();
        _pageRepository = GetRequiredService<IPageRepository>();
    }

    [Fact]
    public async Task Should_List_The_Pages_Of_A_Page_Template_With_Their_Paths_And_A_Sample_Content()
    {
        var template = await CreateAsync(PageTemplateName, "<h1>{{ page.name }}</h1>");
        await SetPageTemplateAsync(SiteTestData.AboutPageId, PageTemplateName);
        await SetPageTemplateAsync(SiteTestData.NewsPageId, PageTemplateName);

        var usage = await _templateAppService.GetUsageAsync(template.Id);

        usage.TemplateId.ShouldBe(template.Id);
        usage.TemplateName.ShouldBe(PageTemplateName);
        usage.Kind.ShouldBe(SiteTemplateKind.Page);
        usage.ReferencedBy.ShouldBeEmpty();
        usage.Pages.Select(p => p.PageId).ShouldBe(new[] { SiteTestData.AboutPageId, SiteTestData.NewsPageId });

        var about = usage.Pages[0];
        about.Name.ShouldBe("about");
        about.DisplayName.ShouldBe("About");
        about.Route.ShouldBe("/about");
        about.Template.ShouldBe(PageTemplateName);
        about.IsHomePage.ShouldBeFalse();
        about.HasSlug.ShouldBeFalse();
        about.Path.ShouldBe("/about");
        about.SampleContent.ShouldBeNull();

        var news = usage.Pages[1];
        news.HasSlug.ShouldBeTrue();
        news.Path.ShouldBe("/news");
        news.SampleContent.ShouldNotBeNull();
        news.SampleContent!.Slug.ShouldBe(SiteTestData.NewsSlug);
        news.SampleContent.CultureName.ShouldBe(SiteTestData.EnglishCulture);
        news.SampleContent.Path.ShouldBe("/news/2026-07/launch");
    }

    /// <summary>
    /// A sample is a content with an address of its own and public: never the draft, and never the page's
    /// own empty-slug content, whose address is the page's.
    /// </summary>
    [Fact]
    public async Task Should_Pick_A_Published_Content_With_A_Slug_As_The_Sample()
    {
        var template = await CreateAsync(PageTemplateName, "<h1>{{ page.name }}</h1>");
        await SetPageTemplateAsync(SiteTestData.BlogPageId, PageTemplateName);

        var blog = (await _templateAppService.GetUsageAsync(template.Id)).Pages.ShouldHaveSingleItem();

        blog.HasSlug.ShouldBeTrue();
        blog.Path.ShouldBe("/blog");
        blog.SampleContent.ShouldNotBeNull();
        blog.SampleContent!.Slug.ShouldBeOneOf(SiteTestData.TripSlug, SiteTestData.GallerySlug);
        blog.SampleContent.Path.ShouldBe("/blog/" + blog.SampleContent.Slug);
    }

    [Fact]
    public async Task Should_List_Every_Page_For_The_Layout_Home_Page_First()
    {
        var layout = await CreateAsync(SiteTemplateConsts.LayoutName, LayoutBody);

        var usage = await _templateAppService.GetUsageAsync(layout.Id);

        usage.Kind.ShouldBe(SiteTemplateKind.Layout);
        usage.Pages.Select(p => p.PageId).ShouldBe(new[]
        {
            SiteTestData.HomePageId, SiteTestData.AboutPageId, SiteTestData.BlogPageId, SiteTestData.NewsPageId
        });
        usage.Pages[0].IsHomePage.ShouldBeTrue();
        usage.Pages[0].Path.ShouldBe("/");
    }

    [Fact]
    public async Task Should_Follow_A_Partial_To_The_Pages_Of_The_Template_That_Includes_It()
    {
        var partial = await CreateAsync(PartialName, "<div class=\"card\">{{ page.name }}</div>");
        var pageTemplate = await CreateAsync(PageTemplateName, "<main>{{ include \"partials/usage-card\" }}</main>");
        await SetPageTemplateAsync(SiteTestData.AboutPageId, PageTemplateName);

        var usage = await _templateAppService.GetUsageAsync(partial.Id);

        usage.Kind.ShouldBe(SiteTemplateKind.Partial);
        usage.Pages.Select(p => p.PageId).ShouldBe(new[] { SiteTestData.AboutPageId });

        var reference = usage.ReferencedBy.ShouldHaveSingleItem();
        reference.TemplateId.ShouldBe(pageTemplate.Id);
        reference.TemplateName.ShouldBe(PageTemplateName);
        reference.Kind.ShouldBe(SiteTemplateKind.Page);
        reference.VersionNumber.ShouldBe(1);
        reference.IsPublished.ShouldBeTrue();
        reference.FunctionName.ShouldBe("include");
    }

    /// <summary>
    /// A partial included by a partial reaches the pages of whatever includes the outer one; its own
    /// <c>ReferencedBy</c> stays direct - the outer partial, not the page template.
    /// </summary>
    [Fact]
    public async Task Should_Follow_A_Chain_Of_Partials()
    {
        var inner = await CreateAsync(PartialName, "<div>inner</div>");
        await CreateAsync(OuterPartialName, "<section>{{ \"partials/usage-card\" | include }}</section>");
        await CreateAsync(PageTemplateName, "<main>{{ include \"partials/usage-outer\" }}</main>");
        await SetPageTemplateAsync(SiteTestData.BlogPageId, PageTemplateName);

        var usage = await _templateAppService.GetUsageAsync(inner.Id);

        usage.Pages.Select(p => p.PageId).ShouldBe(new[] { SiteTestData.BlogPageId });
        usage.ReferencedBy.ShouldHaveSingleItem().TemplateName.ShouldBe(OuterPartialName);
    }

    [Fact]
    public async Task Should_Reach_Every_Page_For_An_Asset_Referenced_By_The_Layout()
    {
        var asset = await CreateAsync(AssetName, "body { color: red; }");
        var layout = await CreateAsync(
            SiteTemplateConsts.LayoutName,
            "<html><head>{{ site_head }}<link rel=\"stylesheet\" href=\"{{ asset_url \"assets/usage.css\" }}\"></head>"
            + "<body>{{ body }}</body></html>");

        var usage = await _templateAppService.GetUsageAsync(asset.Id);

        usage.Kind.ShouldBe(SiteTemplateKind.Asset);
        usage.Pages.Count.ShouldBe(4);

        var reference = usage.ReferencedBy.ShouldHaveSingleItem();
        reference.TemplateId.ShouldBe(layout.Id);
        reference.Kind.ShouldBe(SiteTemplateKind.Layout);
        reference.FunctionName.ShouldBe("asset_url");
    }

    /// <summary>The syntax tree is walked, not the text: the name in markup or in an unrelated string is not a use.</summary>
    [Fact]
    public async Task Should_Not_Count_A_Name_In_Markup_Or_In_A_String()
    {
        var partial = await CreateAsync(PartialName, "<div></div>");
        await CreateAsync(
            PageTemplateName,
            "<p>include \"partials/usage-card\"</p>{{ name = \"partials/usage-card\" }}{{ name }}");
        await SetPageTemplateAsync(SiteTestData.AboutPageId, PageTemplateName);

        var usage = await _templateAppService.GetUsageAsync(partial.Id);

        usage.ReferencedBy.ShouldBeEmpty();
        usage.Pages.ShouldBeEmpty();

        var row = (await _templateAppService.GetListAsync(new GetSiteTemplateListInput()))
            .Items.Single(t => t.Name == PartialName);
        row.UsageCount.ShouldBe(0);
    }

    /// <summary>
    /// References are read from the current version: the published one even when a newer draft drops the
    /// include, and the newest one of a template that was never published.
    /// </summary>
    [Fact]
    public async Task Should_Read_References_From_The_Current_Version()
    {
        var partial = await CreateAsync(PartialName, "<div></div>");

        var published = await CreateAsync(PageTemplateName, "{{ include \"partials/usage-card\" }}");
        await _templateAppService.UpdateAsync(published.Id, new UpdateSiteTemplateDto { Body = "no include", Publish = false });

        var draftOnly = await _templateAppService.CreateAsync(new CreateSiteTemplateDto
        {
            Name = "Usage/DraftOnly",
            Body = "{{ include \"partials/usage-card\" }}",
            Publish = false
        });

        var references = (await _templateAppService.GetUsageAsync(partial.Id)).ReferencedBy;

        references.Select(r => r.TemplateName).ShouldBe(new[] { "Usage/DraftOnly", PageTemplateName });

        var fromDraftOnly = references.Single(r => r.TemplateId == draftOnly.Id);
        fromDraftOnly.VersionNumber.ShouldBe(1);
        fromDraftOnly.IsPublished.ShouldBeFalse();

        var fromPublished = references.Single(r => r.TemplateId == published.Id);
        fromPublished.VersionNumber.ShouldBe(1);
        fromPublished.IsPublished.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Fill_Kind_And_Usage_Count_In_The_List()
    {
        await CreateAsync(SiteTemplateConsts.LayoutName, LayoutBody);
        await CreateAsync(PartialName, "<div></div>");
        await CreateAsync(AssetName, "body {}");
        // Included twice by one template: one use.
        await CreateAsync(PageTemplateName, "{{ include \"partials/usage-card\" }}{{ include \"partials/usage-card\" }}");
        await CreateAsync("Usage/Orphan", "<p>nobody</p>");
        await SetPageTemplateAsync(SiteTestData.AboutPageId, PageTemplateName);
        await SetPageTemplateAsync(SiteTestData.NewsPageId, PageTemplateName);

        var items = (await _templateAppService.GetListAsync(new GetSiteTemplateListInput())).Items;

        var layout = items.Single(t => t.Name == SiteTemplateConsts.LayoutName);
        layout.Kind.ShouldBe(SiteTemplateKind.Layout);
        layout.UsageCount.ShouldBeNull();

        var partial = items.Single(t => t.Name == PartialName);
        partial.Kind.ShouldBe(SiteTemplateKind.Partial);
        partial.UsageCount.ShouldBe(1);

        var asset = items.Single(t => t.Name == AssetName);
        asset.Kind.ShouldBe(SiteTemplateKind.Asset);
        asset.UsageCount.ShouldBe(0);

        var pageTemplate = items.Single(t => t.Name == PageTemplateName);
        pageTemplate.Kind.ShouldBe(SiteTemplateKind.Page);
        pageTemplate.UsageCount.ShouldBe(2);

        items.Single(t => t.Name == "Usage/Orphan").UsageCount.ShouldBe(0);
        items.Single(t => t.Name == SiteTestData.PublishedTemplateName).UsageCount.ShouldBe(0);
    }

    /// <summary>A filtered list still counts uses from templates the filter hides.</summary>
    [Fact]
    public async Task Should_Count_Uses_From_Templates_A_Filtered_List_Hides()
    {
        await CreateAsync(PartialName, "<div></div>");
        await CreateAsync(PageTemplateName, "{{ include \"partials/usage-card\" }}");

        var items = (await _templateAppService.GetListAsync(new GetSiteTemplateListInput { Prefix = "partials/" })).Items;

        items.ShouldHaveSingleItem().UsageCount.ShouldBe(1);
    }

    [Fact]
    public async Task Should_List_Pages_Whose_Template_Has_No_Published_Template()
    {
        await SetPageTemplateAsync(SiteTestData.AboutPageId, SiteTestData.DraftTemplateName);
        await SetPageTemplateAsync(SiteTestData.BlogPageId, SiteTestData.PublishedTemplateName);

        var items = (await _templateAppService.GetUnmatchedPagesAsync()).Items;

        // Blog is rendered by the published Seeded/Published; the others by Razor.
        items.Select(p => p.PageId).ShouldBe(new[]
        {
            SiteTestData.HomePageId, SiteTestData.AboutPageId, SiteTestData.NewsPageId
        });

        var home = items[0];
        home.Name.ShouldBe("home");
        home.DisplayName.ShouldBe("Home");
        home.Template.ShouldBe("Default");
        home.TemplateId.ShouldBeNull();
        home.HasDraft.ShouldBeFalse();

        var about = items[1];
        about.Template.ShouldBe(SiteTestData.DraftTemplateName);
        about.TemplateId.ShouldBe(SiteTestData.DraftTemplateId);
        about.HasDraft.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Throw_Not_Found_For_The_Usage_Of_A_Missing_Template()
    {
        await Should.ThrowAsync<EntityNotFoundException>(() => _templateAppService.GetUsageAsync(Guid.NewGuid()));
    }

    private Task<SiteTemplateDto> CreateAsync(string name, string body)
    {
        return _templateAppService.CreateAsync(new CreateSiteTemplateDto { Name = name, Body = body });
    }

    private Task SetPageTemplateAsync(Guid pageId, string template)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            var page = await _pageRepository.GetAsync(pageId);
            page.SetTemplate(template);
            await _pageRepository.UpdateAsync(page);
        });
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.Select;
using Dignite.Sites.ContentTypes;
using Dignite.Sites.Contents;
using Dignite.Sites.Fields;
using Dignite.Sites.Pages;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Sites;

/// <summary>
/// Builds 总体设计 §2.6's worked example - a company site with a home page, an "about" page, a blog with
/// several post shapes, and a dated news section.
/// <para>
/// Lives in the EF Core test project rather than in <c>Dignite.Sites.TestBase</c> deliberately: it writes
/// through repositories, so it can only run where a DbContext exists. The domain test project shares
/// TestBase and has no database.
/// </para>
/// <para>
/// Contents are created through <see cref="ContentManager"/> rather than by inserting rows, so the seed
/// exercises the same write path as production - which is what makes the derived index rows the tests
/// assert on real rather than fabricated.
/// </para>
/// </summary>
public class SitesTestDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Field, Guid> _fieldRepository;
    private readonly IRepository<Page, Guid> _pageRepository;
    private readonly IRepository<ContentType, Guid> _contentTypeRepository;
    private readonly ContentManager _contentManager;

    public SitesTestDataSeedContributor(
        IRepository<Field, Guid> fieldRepository,
        IRepository<Page, Guid> pageRepository,
        IRepository<ContentType, Guid> contentTypeRepository,
        ContentManager contentManager)
    {
        _fieldRepository = fieldRepository;
        _pageRepository = pageRepository;
        _contentTypeRepository = contentTypeRepository;
        _contentManager = contentManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await SeedFieldsAsync();
        await SeedPagesAndTypesAsync();
        await SeedContentsAsync();
    }

    /// <summary>
    /// The tenant's field library. Note that title and body are defined once here and pulled into five
    /// different content types below - that reuse is the whole reason definitions live in their own table
    /// (§2.3).
    /// </summary>
    private async Task SeedFieldsAsync()
    {
        // autoSave throughout this class: the whole seed runs in one unit of work, and the later steps
        // read these rows back through repositories (ContentManager resolves the content type, the field
        // provider resolves the definitions). An unsaved insert is only in EF Core's change tracker,
        // which a query does not consult, so without this the reads find nothing.
        await _fieldRepository.InsertAsync(
            new Field(SitesTestData.TitleFieldId, "title", "Title", "TextEdit"), autoSave: true);

        await _fieldRepository.InsertAsync(
            new Field(SitesTestData.BodyFieldId, "body", "Body", "TextEdit"), autoSave: true);

        var categoryConfiguration = new SelectConfiguration
        {
            Multiple = true,
            Options = new List<SelectListItem>
            {
                new() { Text = "Travel", Value = "travel" },
                new() { Text = "Food", Value = "food" },
                new() { Text = "Tech", Value = "tech" }
            }
        };

        await _fieldRepository.InsertAsync(
            new Field(
                SitesTestData.CategoryFieldId, "category", "Category", "Select",
                configuration: categoryConfiguration.ConfigurationDictionary),
            autoSave: true);

        await _fieldRepository.InsertAsync(
            new Field(SitesTestData.ViewsFieldId, "views", "Views", "NumericEdit"), autoSave: true);

        await _fieldRepository.InsertAsync(
            new Field(SitesTestData.FeaturedFieldId, "featured", "Featured", "Switch"), autoSave: true);
    }

    private async Task SeedPagesAndTypesAsync()
    {
        await _pageRepository.InsertAsync(
            new Page(SitesTestData.HomePageId, "home", "Home", "/", isHomePage: true), autoSave: true);

        await _pageRepository.InsertAsync(
            new Page(SitesTestData.AboutPageId, "about", "About", "/about"), autoSave: true);

        await _pageRepository.InsertAsync(
            new Page(SitesTestData.BlogPageId, "blog", "Blog", "/blog", "{slug}"), autoSave: true);

        // The dated variant of §3.3, so route resolution is exercised against both patterns.
        await _pageRepository.InsertAsync(
            new Page(SitesTestData.NewsPageId, "news", "News", "/news", "{date:yyyy/MM}/{slug}"), autoSave: true);

        await _contentTypeRepository.InsertAsync(
            new ContentType(
                SitesTestData.HomeTypeId, SitesTestData.HomePageId, "home", "Home",
                fields: new[] { Usage(SitesTestData.TitleFieldId, order: 0), Usage(SitesTestData.BodyFieldId, order: 1) }),
            autoSave: true);

        await _contentTypeRepository.InsertAsync(
            new ContentType(
                SitesTestData.AboutTypeId, SitesTestData.AboutPageId, "about", "About",
                fields: new[] { Usage(SitesTestData.TitleFieldId, order: 0), Usage(SitesTestData.BodyFieldId, order: 1) }),
            autoSave: true);

        // The searchable usages live here, not on the definitions - the same title field is searchable in
        // this type and (below) unsearchable in the gallery type, which is exactly the per-usage split.
        await _contentTypeRepository.InsertAsync(
            new ContentType(
                SitesTestData.PostArticleTypeId, SitesTestData.BlogPageId, "post-article", "Article",
                fields: new[]
                {
                    Usage(SitesTestData.TitleFieldId, required: true, searchable: true, order: 0),
                    Usage(SitesTestData.BodyFieldId, order: 1),

                    Usage(SitesTestData.CategoryFieldId, searchable: true, order: 2),
                    Usage(SitesTestData.ViewsFieldId, searchable: true, order: 3),
                    Usage(SitesTestData.FeaturedFieldId, searchable: true, order: 4)
                }),
            autoSave: true);

        await _contentTypeRepository.InsertAsync(
            new ContentType(
                SitesTestData.PostGalleryTypeId, SitesTestData.BlogPageId, "post-gallery", "Gallery",
                fields: new[]
                {
                    Usage(SitesTestData.TitleFieldId, order: 0),
                    Usage(SitesTestData.BodyFieldId, order: 1)
                }),
            autoSave: true);

        await _contentTypeRepository.InsertAsync(
            new ContentType(
                SitesTestData.NewsItemTypeId, SitesTestData.NewsPageId, "news-item", "News item",
                fields: new[] { Usage(SitesTestData.TitleFieldId, order: 0) }),
            autoSave: true);
    }

    private async Task SeedContentsAsync()
    {
        // Empty slug: the single content of a page, whose URL is the page route itself.
        await _contentManager.CreateAsync(
            SitesTestData.HomeTypeId, SitesTestData.EnglishCulture, "", SitesTestData.PublishTime,
            ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "Welcome", ["body"] = "Home body" });

        // Two languages of one content, so the natural-key translation grouping (§2.4) has something to
        // group. Same slug in both, which is the cost that model accepts.
        await _contentManager.CreateAsync(
            SitesTestData.AboutTypeId, SitesTestData.EnglishCulture, "", SitesTestData.PublishTime,
            ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "About us", ["body"] = "About body" });

        await _contentManager.CreateAsync(
            SitesTestData.AboutTypeId, SitesTestData.ChineseCulture, "", SitesTestData.PublishTime,
            ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "关于我们", ["body"] = "关于我们的正文" });

        await _contentManager.CreateAsync(
            SitesTestData.PostArticleTypeId, SitesTestData.EnglishCulture, SitesTestData.TripSlug,
            SitesTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?>
            {
                ["title"] = "My trip",
                ["body"] = "Trip body",
                ["category"] = new List<string> { "travel", "food" },
                ["views"] = 42,
                ["featured"] = true
            });

        // Unpublished, so route resolution has something it must refuse to serve publicly.
        await _contentManager.CreateAsync(
            SitesTestData.PostArticleTypeId, SitesTestData.EnglishCulture, SitesTestData.DraftSlug,
            SitesTestData.PublishTime, ContentStatus.Draft,
            new Dictionary<string, object?> { ["title"] = "Draft post", ["views"] = 7 });

        // A second shape under the same page - the point of a page hosting several content types.
        await _contentManager.CreateAsync(
            SitesTestData.PostGalleryTypeId, SitesTestData.EnglishCulture, SitesTestData.GallerySlug,
            SitesTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "Summer photos" });

        await _contentManager.CreateAsync(
            SitesTestData.NewsItemTypeId, SitesTestData.EnglishCulture, SitesTestData.NewsSlug,
            SitesTestData.PublishTime, ContentStatus.Published,
            new Dictionary<string, object?> { ["title"] = "We launched" });
    }

    private static ContentTypeField Usage(
        Guid fieldId,
        bool required = false,
        bool searchable = false,
        int order = 0)
    {
        return new ContentTypeField(fieldId, required, searchable, showInList: true, displayName: null, order: order);
    }
}

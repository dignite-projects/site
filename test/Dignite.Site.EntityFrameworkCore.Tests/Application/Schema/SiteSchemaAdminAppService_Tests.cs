using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Select;
using Dignite.Site.Admin.Pages;
using Dignite.Site.ContentTypes;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Settings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dignite.Site.Admin.Schema;

/// <summary>
/// The schema document is the contract handed to an AI client (总体设计 §1.2 principle 3, §6.2.4), so
/// these tests are about it carrying everything a write needs - and carrying it under the names a write
/// will actually use.
/// </summary>
public class SiteSchemaAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ISiteSchemaAdminAppService _schemaAppService;
    private readonly TestSettingValueProvider _settings;

    public SiteSchemaAdminAppService_Tests()
    {
        _schemaAppService = GetRequiredService<ISiteSchemaAdminAppService>();
        _settings = GetRequiredService<TestSettingValueProvider>();
    }

    /// <summary>
    /// The first entry is the default language, and there is no second setting that could disagree
    /// (总体设计 §5.5). A client picks <c>cultureName</c> out of this list rather than inventing one.
    /// </summary>
    [Fact]
    public async Task Should_Report_Enabled_Languages_In_Order_With_The_First_As_Default()
    {
        _settings.Set(SiteSettings.EnabledLanguages, $"{SiteTestData.ChineseCulture},{SiteTestData.EnglishCulture}");

        var schema = await _schemaAppService.GetAsync();

        schema.EnabledLanguages.ShouldBe(new[] { SiteTestData.ChineseCulture, SiteTestData.EnglishCulture });
        schema.DefaultLanguage.ShouldBe(SiteTestData.ChineseCulture);
    }

    [Fact]
    public async Task Should_Report_Every_Page_With_Its_Route()
    {
        var schema = await _schemaAppService.GetAsync();

        schema.Pages.Select(page => page.Name)
            .ShouldBe(new[] { "home", "about", "blog", "news" }, ignoreOrder: true);

        var blog = schema.Pages.Single(page => page.Name == "blog");
        blog.Route.ShouldBe("/blog");
        blog.ContentPathPattern.ShouldBe("{slug}");
        blog.IsHomePage.ShouldBeFalse();

        schema.Pages.Single(page => page.Name == "home").IsHomePage.ShouldBeTrue();
    }

    /// <summary>
    /// The three shapes under <c>/blog</c> and <c>/news</c> are the case 总体设计 §6.2.1 uses to reject a
    /// scenario cut: a compile-time <c>publish_article</c> tool could not know these names, because they
    /// are the tenant's runtime data. They have to arrive here instead.
    /// </summary>
    [Fact]
    public async Task Should_Report_Each_Page_Content_Types_By_Name()
    {
        var schema = await _schemaAppService.GetAsync();

        schema.Pages.Single(page => page.Name == "blog").ContentTypes
            .Select(contentType => contentType.Name)
            .ShouldBe(new[] { "post-article", "post-gallery" }, ignoreOrder: true);

        schema.Pages.Single(page => page.Name == "news").ContentTypes
            .Single().Name.ShouldBe("news-item");
    }

    /// <summary>
    /// Definition ∪ usage, merged. The name and field type come from the library definition; Required,
    /// Searchable and Order are this content type's own.
    /// </summary>
    [Fact]
    public async Task Should_Merge_Field_Definitions_With_This_Types_Usage_Of_Them()
    {
        var schema = await _schemaAppService.GetAsync();

        var article = schema.Pages
            .Single(page => page.Name == "blog").ContentTypes
            .Single(contentType => contentType.Name == "post-article");

        article.Fields.Select(field => field.Name)
            .ShouldBe(new[] { "title", "body", "category", "views", "featured" });

        var title = article.Fields.Single(field => field.Name == "title");
        title.FieldTypeName.ShouldBe("TextEdit");
        title.Required.ShouldBeTrue();
        title.Searchable.ShouldBeTrue();
        title.Order.ShouldBe(0);

        var category = article.Fields.Single(field => field.Name == "category");
        category.FieldTypeName.ShouldBe("Select");
        // The option list a generated value has to come from - the schema is the write gate as well as
        // the target shape, so what validates and what is advertised are the same thing.
        category.Configuration.ShouldContainKey(SelectConfigurationNames.Options);
    }

    /// <summary>
    /// The same definition, used differently in two types. This is why the merge cannot be collapsed into
    /// either half on its own.
    /// </summary>
    [Fact]
    public async Task Should_Report_The_Same_Field_Differently_In_Two_Content_Types()
    {
        var schema = await _schemaAppService.GetAsync();

        var blog = schema.Pages.Single(page => page.Name == "blog");

        var inArticle = blog.ContentTypes
            .Single(contentType => contentType.Name == "post-article").Fields
            .Single(field => field.Name == "title");

        var inGallery = blog.ContentTypes
            .Single(contentType => contentType.Name == "post-gallery").Fields
            .Single(field => field.Name == "title");

        inArticle.Required.ShouldBeTrue();
        inArticle.Searchable.ShouldBeTrue();

        inGallery.Required.ShouldBeFalse();
        inGallery.Searchable.ShouldBeFalse();
    }

    /// <summary>
    /// A field can be deleted while content types still reference it - deleting a definition does not
    /// rewrite every arrangement. The schema must skip that usage rather than fail, or one stale
    /// reference would make the whole document unreadable.
    /// </summary>
    [Fact]
    public async Task Should_Skip_A_Usage_Whose_Definition_Has_Been_Deleted()
    {
        var fieldRepository = GetRequiredService<IRepository<Field, System.Guid>>();
        await fieldRepository.DeleteAsync(SiteTestData.ViewsFieldId, autoSave: true);

        var schema = await _schemaAppService.GetAsync();

        var article = schema.Pages
            .Single(page => page.Name == "blog").ContentTypes
            .Single(contentType => contentType.Name == "post-article");

        article.Fields.ShouldNotContain(field => field.Name == "views");
        article.Fields.ShouldContain(field => field.Name == "title");
    }

    /// <summary>
    /// The description is what a model reads to choose between sibling types (总体设计 §6.2.6), so it has
    /// to survive into the document rather than being dropped as decoration.
    /// </summary>
    [Fact]
    public async Task Should_Carry_A_Content_Types_Description_Through()
    {
        var contentTypeRepository = GetRequiredService<IRepository<ContentType, System.Guid>>();
        var newsItem = await contentTypeRepository.GetAsync(SiteTestData.NewsItemTypeId);
        newsItem.SetDescription("A short dated announcement. Use this for press releases and news.");
        await contentTypeRepository.UpdateAsync(newsItem, autoSave: true);

        var schema = await _schemaAppService.GetAsync();

        schema.Pages.Single(page => page.Name == "news").ContentTypes.Single()
            .Description.ShouldBe("A short dated announcement. Use this for press releases and news.");
    }

    /// <summary>
    /// Same soft-delete semantics as everywhere else: <c>PageManager.DeleteAsync</c>'s cascade (总体设计
    /// §2.5) removes the content types under a deleted page too, so nothing from that page's subtree
    /// should reappear here afterwards. This is the schema-assembly side of the cascade pinned at the
    /// app-service level in <c>PageAdminAppService_Tests</c>.
    /// </summary>
    [Fact]
    public async Task Should_Omit_A_Deleted_Page_And_Its_Content_Types_From_The_Schema()
    {
        var pageAppService = GetRequiredService<IPageAdminAppService>();
        await pageAppService.DeleteAsync(SiteTestData.BlogPageId);

        var schema = await _schemaAppService.GetAsync();

        schema.Pages.ShouldNotContain(page => page.Name == "blog");

        var contentTypeNames = schema.Pages.SelectMany(page => page.ContentTypes).Select(ct => ct.Name).ToList();
        contentTypeNames.ShouldNotContain("post-article");
        contentTypeNames.ShouldNotContain("post-gallery");
    }

    /// <summary>
    /// Guards the transient state 总体设计 §2.5 warns a pre-cascade-fix deployment could still have on
    /// disk: a page removed directly (bypassing <c>PageManager.DeleteAsync</c>, exactly as the old,
    /// uncascaded delete path used to) while its content type stays live and still points at the now-gone
    /// <c>PageId</c>. The document must quietly drop that orphan rather than surface a dangling reference
    /// or fail to assemble the rest of the schema around it.
    /// </summary>
    [Fact]
    public async Task Should_Omit_A_Content_Type_Whose_Page_Was_Deleted_Without_Cascading()
    {
        var pageRepository = GetRequiredService<IRepository<Page, System.Guid>>();
        var page = await pageRepository.GetAsync(SiteTestData.NewsPageId);
        await pageRepository.DeleteAsync(page, autoSave: true);

        var schema = await _schemaAppService.GetAsync();

        schema.Pages.ShouldNotContain(p => p.Name == "news");
        schema.Pages.SelectMany(p => p.ContentTypes).Select(ct => ct.Name).ShouldNotContain("news-item");

        // The rest of the document is assembled normally around the orphan.
        schema.Pages.Select(p => p.Name).ShouldBe(new[] { "home", "about", "blog" }, ignoreOrder: true);
    }
}

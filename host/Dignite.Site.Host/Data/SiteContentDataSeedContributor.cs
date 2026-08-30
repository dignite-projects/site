using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.CKEditor;
using Dignite.Abp.FlexFields.FileExplorer;
using Dignite.FlexFields.Site.Seo;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;

namespace Dignite.Site.Host.Data;

/// <summary>
/// Local-testing convenience: gives a fresh host database a small but coherent "company site" out of the
/// box - Home / About / Events / News / Contact - the same shape 总体设计 §2.6's worked example describes,
/// so there is something to click through and demo without hand-building pages first.
/// <para>
/// Idempotent by name, not by a run-once flag, the same way <see cref="Dignite.Site.Seo.SeoFieldPresetSeedContributor"/>
/// and <c>OpenIddictDataSeedContributor</c> are: every page, field and content type is looked up by its
/// unique name first and only created if missing, and every content by its slug. That means this never
/// touches an already-populated database - including this repo's own dev <c>Host.db</c>, which already has
/// pages with these same names from manual testing - and only fills in what a genuinely fresh database is
/// missing.
/// </para>
/// <para>
/// Builds entities directly and inserts through the repositories with <c>autoSave: true</c>, the same way
/// <c>Dignite.Site.EntityFrameworkCore.Tests.SiteTestDataSeedContributor</c> does, rather than through
/// <c>PageManager</c>/<c>FieldManager</c>/<c>ContentTypeManager</c> - and for the same reason its own
/// comments give: everything here runs in one unit of work, and <see cref="ContentManager.CreateAsync"/>
/// re-reads the page, content type and declared fields through plain queries (not <c>DbSet.Find</c>), which
/// do not see a still-unsaved insert sitting only in EF Core's change tracker. The managers' own
/// <c>InsertAsync</c> calls do not pass <c>autoSave</c>, so going through them here would work on an empty
/// page/type/field set and throw <c>EntityNotFoundException</c> the moment a content is created against a
/// content type inserted moments earlier in the same pass.
/// </para>
/// <para>
/// Host-only: guarded on <see cref="DataSeedContext.TenantId"/> being null. A new tenant building its own
/// site has no use for a demo company's pages.
/// </para>
/// </summary>
public class SiteContentDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private const string EnglishCulture = "en";
    private const string ChineseCulture = "zh-Hans";

    private readonly IFieldRepository _fieldRepository;
    private readonly IPageRepository _pageRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentRepository _contentRepository;
    private readonly ContentManager _contentManager;
    private readonly IGuidGenerator _guidGenerator;

    public SiteContentDataSeedContributor(
        IFieldRepository fieldRepository,
        IPageRepository pageRepository,
        IContentTypeRepository contentTypeRepository,
        IContentRepository contentRepository,
        ContentManager contentManager,
        IGuidGenerator guidGenerator)
    {
        _fieldRepository = fieldRepository;
        _pageRepository = pageRepository;
        _contentTypeRepository = contentTypeRepository;
        _contentRepository = contentRepository;
        _contentManager = contentManager;
        _guidGenerator = guidGenerator;
    }

    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (context.TenantId != null)
        {
            return;
        }

        var titleFieldId = await GetOrCreateFieldAsync("title", "Title", "Text");
        var bodyFieldId = await GetOrCreateFieldAsync("body", "Body", CKEditorFieldType.ControlName);

        var companyNameFieldId = await GetOrCreateFieldAsync(
            "company_name", "公司名称", "Text", "联系页面展示的公司或组织名称。");
        var companyAddressFieldId = await GetOrCreateFieldAsync(
            "company_address", "公司地址", "Text", "公司的办公地址或联系地址。");
        var companyPhoneFieldId = await GetOrCreateFieldAsync(
            "company_phone", "公司电话", "Text", "公司对外联系使用的电话号码。");
        var companyEmailFieldId = await GetOrCreateFieldAsync(
            "company_email", "公司邮箱", "Text", "公司对外联系使用的电子邮箱地址。");
        var companyWechatQrFieldId = await GetOrCreateFieldAsync(
            "company_wechat_qr", "公司微信二维码", FileExplorerFieldType.ControlName,
            "公司官方微信或客服微信的二维码图片。",
            new FileExplorerConfiguration { FileContainerName = "site-files", UploadFileMultiple = false }
                .ConfigurationDictionary);

        // The platform's SEO field is seeded separately (SeoFieldPresetSeedContributor, host-side, ahead
        // of this contributor in the same pass) - looked up rather than created here, and simply left out
        // of the Contact type below on the off chance this ever ran first.
        var seoField = await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);

        await SeedHomeAsync(titleFieldId, bodyFieldId);
        await SeedAboutAsync(titleFieldId, bodyFieldId);
        await SeedEventsAsync(titleFieldId, bodyFieldId);
        await SeedNewsAsync(titleFieldId, bodyFieldId);
        await SeedContactAsync(
            seoField?.Id, companyNameFieldId, companyAddressFieldId, companyPhoneFieldId, companyEmailFieldId,
            companyWechatQrFieldId);
    }

    private async Task SeedHomeAsync(Guid titleFieldId, Guid bodyFieldId)
    {
        var page = await GetOrCreatePageAsync("home", "Home", "/");
        var contentType = await GetOrCreateContentTypeAsync(
            page.Id, "home", "Home",
            new[]
            {
                Usage(titleFieldId, required: true, showInList: true, order: 0),
                Usage(bodyFieldId, order: 1)
            });

        await EnsureContentAsync(
            contentType.Id, page.Id, EnglishCulture, "", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new Dictionary<string, object?>
            {
                ["title"] = "Welcome to Dignite",
                ["body"] = "<p>Dignite is a small headless CMS for building and running content-driven websites.</p>"
            });
    }

    private async Task SeedAboutAsync(Guid titleFieldId, Guid bodyFieldId)
    {
        var page = await GetOrCreatePageAsync("about", "About", "/about");
        var contentType = await GetOrCreateContentTypeAsync(
            page.Id, "about", "About",
            new[]
            {
                Usage(titleFieldId, required: true, showInList: true, order: 0),
                Usage(bodyFieldId, order: 1)
            });

        await EnsureContentAsync(
            contentType.Id, page.Id, EnglishCulture, "", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new Dictionary<string, object?>
            {
                ["title"] = "About Us",
                ["body"] = "<p>Dignite is a small team building tools for editors and developers to manage " +
                           "content together, without either side having to compromise on how they work.</p>"
            });
    }

    private async Task SeedEventsAsync(Guid titleFieldId, Guid bodyFieldId)
    {
        var page = await GetOrCreatePageAsync("events", "Events", "/events/{slug?}");
        var contentType = await GetOrCreateContentTypeAsync(
            page.Id, "events", "Events",
            new[]
            {
                Usage(titleFieldId, required: true, showInList: true, order: 0),
                Usage(bodyFieldId, order: 1)
            });

        await EnsureContentAsync(
            contentType.Id, page.Id, EnglishCulture, "", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new Dictionary<string, object?>
            {
                ["title"] = "Events",
                ["body"] = "<p>Upcoming talks and meetups from the Dignite team.</p>"
            });

        await EnsureContentAsync(
            contentType.Id, page.Id, EnglishCulture, "summer-fest", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new Dictionary<string, object?>
            {
                ["title"] = "Summer Fest",
                ["body"] = "<p>Join us for an afternoon of lightning talks and demos from the community.</p>"
            });
    }

    /// <summary>
    /// A parent/child pair of pages, so the routing table demonstrates both a page hierarchy that is
    /// purely organizational (总体设计 §3.1 - <see cref="Page.ParentId"/> plays no part in route
    /// resolution) and a route placeholder with an embedded validation regex
    /// (<c>{publishTime:yyyy-MM:^\d{4}-(0[1-9]|1[0-2])$}</c>): <c>news-home</c> lists a given month,
    /// <c>news</c> is where an individual item's own address lives.
    /// <para>
    /// Both render through <c>Default</c>, the one template this Host actually ships
    /// (<c>Views/Shared/Default.cshtml</c>) - the live dev database had <c>news</c> pointing at a
    /// <c>news/details</c> view that was never built, which 500s the moment it is requested (confirmed
    /// while verifying this seed). Point a real <c>news/details.cshtml</c> at this page's <c>Template</c>
    /// later if one gets built; until then, a template naming a view that does not exist is not something
    /// to carry into seed data.
    /// </para>
    /// </summary>
    private async Task SeedNewsAsync(Guid titleFieldId, Guid bodyFieldId)
    {
        const string monthRoute = @"/news/{publishTime:yyyy-MM:^\d{4}-(0[1-9]|1[0-2])$}";

        var newsHomePage = await GetOrCreatePageAsync("news-home", "News Home", monthRoute);
        var newsPage = await GetOrCreatePageAsync(
            "news", "News", monthRoute + "/{slug}", parentId: newsHomePage.Id);

        var contentType = await GetOrCreateContentTypeAsync(
            newsPage.Id, "news-item", "News item",
            new[]
            {
                Usage(titleFieldId, required: true, showInList: true, order: 0),
                Usage(bodyFieldId, order: 1)
            });

        const string slug = "aurora-robotics-unveils-warehousepilot-to-cut";
        var publishTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        await EnsureContentAsync(
            contentType.Id, newsPage.Id, EnglishCulture, slug, publishTime,
            new Dictionary<string, object?>
            {
                ["title"] = "Aurora Robotics Unveils WarehousePilot to Cut Fulfillment Errors by Half",
                ["body"] =
                    "<p>Aurora Robotics today announced the general availability of WarehousePilot, an " +
                    "autonomous inventory-scanning platform the company says reduced pick-and-pack errors " +
                    "by 52% across a six-month pilot with three mid-size logistics partners.</p>\n" +
                    "<p>“We built WarehousePilot because most warehouse errors happen in the last ten " +
                    "feet between a shelf and a shipping box, not in the routing software,” said Priya " +
                    "Nakamura, Aurora's co-founder and CTO. “Getting that number down by half, without " +
                    "adding headcount, is the part our pilot partners actually cared about.”</p>\n" +
                    "<p>The platform combines a fleet of camera-equipped scanning robots with a lightweight " +
                    "reconciliation service that cross-checks physical counts against warehouse-management " +
                    "records every fifteen minutes, flagging mismatches to floor staff before an order ships " +
                    "rather than after a customer complains.</p>\n" +
                    "<p>Aurora says WarehousePilot will roll out to its full partner network starting next " +
                    "quarter, with pricing based on warehouse square footage rather than per-robot " +
                    "licensing — a model the company argues better matches how fulfillment operators " +
                    "already budget for space and labor.</p>"
            });

        await EnsureContentAsync(
            contentType.Id, newsPage.Id, ChineseCulture, slug, publishTime,
            new Dictionary<string, object?>
            {
                ["title"] = "Aurora Robotics 发布 WarehousePilot，将仓储履约错误率降低一半",
                ["body"] =
                    "<p>Aurora Robotics 今日宣布其自主库存扫描平台 WarehousePilot 正式全面上市。该公司表示，" +
                    "在与三家中型物流合作伙伴进行的为期六个月的试点中，拣货与打包错误率降低了 52%。</p>\n" +
                    "<p>“我们打造 WarehousePilot，是因为大多数仓库错误发生在货架与货箱之间最后的十英尺，" +
                    "而不是在路径规划软件里，” Aurora 联合创始人兼首席技术官 Priya Nakamura 表示。" +
                    "“在不增加人手的情况下把这个数字降低一半，才是试点合作伙伴真正在意的部分。”</p>\n" +
                    "<p>该平台将一支配备摄像头的扫描机器人机队，与一项轻量级的核对服务结合在一起，每十五分钟将" +
                    "实物库存与仓库管理系统记录进行交叉核对，在订单发出前而不是客户投诉后，就把不一致之处提示" +
                    "给一线员工。</p>\n" +
                    "<p>Aurora 表示，WarehousePilot 将于下季度开始向其全部合作伙伴网络推广，定价将基于仓库面积" +
                    "而非按机器人数量收费——公司认为这一模式更符合履约运营商现有的空间和人力预算方式。</p>"
            });
    }

    private async Task SeedContactAsync(
        Guid? seoFieldId,
        Guid companyNameFieldId,
        Guid companyAddressFieldId,
        Guid companyPhoneFieldId,
        Guid companyEmailFieldId,
        Guid companyWechatQrFieldId)
    {
        var page = await GetOrCreatePageAsync("contact", "联系我们", "/contact");

        var fields = new List<ContentTypeField>();
        var order = 0;
        if (seoFieldId != null)
        {
            fields.Add(Usage(seoFieldId.Value, order: order++));
        }

        fields.Add(Usage(companyNameFieldId, required: true, showInList: true, order: order++));
        fields.Add(Usage(companyAddressFieldId, required: true, order: order++));
        fields.Add(Usage(companyPhoneFieldId, required: true, showInList: true, order: order++));
        fields.Add(Usage(companyEmailFieldId, required: true, showInList: true, order: order++));
        fields.Add(Usage(companyWechatQrFieldId, order: order));

        var contentType = await GetOrCreateContentTypeAsync(page.Id, "contact", "联系我们", fields);

        var publishTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await EnsureContentAsync(
            contentType.Id, page.Id, EnglishCulture, "", publishTime,
            new Dictionary<string, object?>
            {
                [SeoFieldNames.FieldName] = new SeoFieldValue
                {
                    MetaTitle = "Contact Us | Dignite",
                    MetaDescription =
                        "Contact Dignite for company information, address, phone, email, and WeChat support.",
                    OgImage = "",
                    NoIndex = false
                },
                ["company_name"] = "Dignite",
                ["company_address"] = "1-1-1 Marunouchi, Chiyoda-ku, Tokyo, Japan",
                ["company_phone"] = "+81 3-1234-5678",
                ["company_email"] = "contact@example.com"
            });

        await EnsureContentAsync(
            contentType.Id, page.Id, ChineseCulture, "", publishTime,
            new Dictionary<string, object?>
            {
                [SeoFieldNames.FieldName] = new SeoFieldValue
                {
                    MetaTitle = "联系我们 | Dignite",
                    MetaDescription = "联系 Dignite，获取公司地址、电话、邮箱以及微信联系方式。",
                    OgImage = "",
                    NoIndex = false
                },
                ["company_name"] = "Dignite",
                ["company_address"] = "日本东京都千代田区丸之内1-1-1",
                ["company_phone"] = "+81 3-1234-5678",
                ["company_email"] = "contact@example.com"
            });
    }

    private async Task<Guid> GetOrCreateFieldAsync(
        string name,
        string displayName,
        string fieldTypeName,
        string? description = null,
        FieldConfigurationDictionary? configuration = null)
    {
        var existing = await _fieldRepository.FindByNameAsync(name);
        if (existing != null)
        {
            return existing.Id;
        }

        var field = new Field(
            _guidGenerator.Create(), name, displayName, fieldTypeName, description, configuration, tenantId: null);
        field = await _fieldRepository.InsertAsync(field, autoSave: true);
        return field.Id;
    }

    private async Task<Page> GetOrCreatePageAsync(
        string name,
        string displayName,
        string route,
        string template = "Default",
        Guid? parentId = null)
    {
        var existing = await _pageRepository.FindByNameAsync(name);
        if (existing != null)
        {
            return existing;
        }

        var page = new Page(
            _guidGenerator.Create(), name, displayName, route, template, tenantId: null, parentId: parentId);
        return await _pageRepository.InsertAsync(page, autoSave: true);
    }

    private async Task<ContentType> GetOrCreateContentTypeAsync(
        Guid pageId,
        string name,
        string displayName,
        IEnumerable<ContentTypeField> fields)
    {
        var existing = await _contentTypeRepository.FindByNameAsync(pageId, name);
        if (existing != null)
        {
            return existing;
        }

        var contentType = new ContentType(_guidGenerator.Create(), pageId, name, displayName, fields: fields, tenantId: null);
        return await _contentTypeRepository.InsertAsync(contentType, autoSave: true);
    }

    private async Task EnsureContentAsync(
        Guid contentTypeId,
        Guid pageId,
        string cultureName,
        string slug,
        DateTime publishTime,
        Dictionary<string, object?> fieldValues)
    {
        if (await _contentRepository.SlugExistsAsync(pageId, cultureName, slug))
        {
            return;
        }

        await _contentManager.CreateAsync(contentTypeId, cultureName, slug, publishTime, ContentStatus.Published, fieldValues);
    }

    private static ContentTypeField Usage(Guid fieldId, bool required = false, bool showInList = false, int order = 0)
    {
        return new ContentTypeField(fieldId, required, searchable: false, showInList: showInList, order: order);
    }
}

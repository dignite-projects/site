using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.CKEditor;
using Dignite.Abp.FlexFields.Matrix;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Table;
using Dignite.FlexFields.Site.Files;
using Dignite.FlexFields.Site.Seo;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Files;
using Dignite.Site.Pages;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;

namespace Dignite.Site.Host.Data;

/// <summary>
/// Local-testing convenience: gives a fresh host database a small but coherent "company site" out of the
/// box - Home / About / News / Events / Contact and two legal pages - with enough content to try every part
/// of the rendering side by hand, together with the Scriban theme that <see cref="SiteThemeDataSeedContributor"/>
/// publishes for it:
/// <list type="bullet">
/// <item><b>Paging</b>: <c>/news</c> (15 published items, 10 a page) and <c>/events</c> (22, so three pages).</item>
/// <item><b>Route filters and counts per field</b>: news items carry a <c>Select</c> category the page's route
/// has a placeholder for (<c>/news/product</c>, <c>/news/product/2026-08</c>), and the theme counts the
/// contents of each category and reads the labels from the field's own configuration.</item>
/// <item><b>What a list must leave out</b>: a draft, a scheduled and an archived news item, and drafts among
/// the events, none of which may appear in a list, a count or a sitemap.</item>
/// <item><b>Field types</b>: <c>Text</c>, <c>Select</c>, <c>Boolean</c>, <c>DateTime</c>, rich text, the composite
/// <c>Matrix</c> and <c>Table</c>, a file field, and the SEO field.</item>
/// <item><b>Routes</b>: a required slug, an optional placeholder, a regular expression on a slug that gives two
/// contents root-level addresses, a page hierarchy, and a second language (a few contents have a
/// <c>zh-Hans</c> version, most do not, which shows the language switcher's untranslated state).</item>
/// </list>
/// <para>
/// Idempotent by name, not by a run-once flag, the same way <see cref="Dignite.Site.Seo.SeoFieldPresetSeedContributor"/>
/// and <c>OpenIddictDataSeedContributor</c> are: every page, field and content type is looked up by its
/// unique name first and only created if missing, and every content by its slug. That means this never
/// touches an already-populated database - and also that it does not reshape one: a database seeded by an
/// older version keeps its old pages and routes, and only a fresh database (delete <c>SiteHost.db</c>) gets the
/// shape described here.
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

    private static readonly DateTime Jan2026 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IFieldRepository _fieldRepository;
    private readonly IPageRepository _pageRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentRepository _contentRepository;
    private readonly ContentManager _contentManager;
    private readonly IGuidGenerator _guidGenerator;

    /// <summary>
    /// Every field id already used by some content type, computed once at the start of
    /// <see cref="SeedAsync"/> and updated as this run attaches more. See
    /// <see cref="GetOrCreateContentTypeAsync"/> for what it is used for and why "used nowhere yet"
    /// rather than "created this run" is the right test - a field this exact run just created can
    /// still be a field an EARLIER, pre-fix run of this same seed already created and left orphaned,
    /// which is precisely the case this exists to repair.
    /// </summary>
    private HashSet<Guid> _fieldIdsInUseAnywhere = new();

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

        var existingContentTypes = await _contentTypeRepository.GetListAsync();
        _fieldIdsInUseAnywhere = existingContentTypes.SelectMany(ct => ct.GetFieldIds()).ToHashSet();

        var titleFieldId = await GetOrCreateFieldAsync("title", "Title", "Text");
        var bodyFieldId = await GetOrCreateFieldAsync("body", "Body", CKEditorFieldType.ControlName);

        // The platform's SEO field is seeded separately (SeoFieldPresetSeedContributor, host-side, ahead
        // of this contributor in the same pass) - looked up rather than created here, and simply left out
        // of the Contact type below on the off chance this ever ran first.
        var seoField = await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);

        await SeedHomeAsync(titleFieldId, bodyFieldId);
        var aboutPage = await SeedAboutAsync(titleFieldId, bodyFieldId);
        await SeedNewsAsync(titleFieldId, bodyFieldId);
        await SeedEventsAsync(titleFieldId, bodyFieldId);
        await SeedContactAsync(aboutPage.Id, titleFieldId, seoField?.Id);
        await SeedLegalAsync(titleFieldId, bodyFieldId);
    }

    private async Task SeedHomeAsync(Guid titleFieldId, Guid bodyFieldId)
    {
        var page = await GetOrCreatePageAsync("home", "Home", "/", SeedTheme.HomeTemplate);
        var contentType = await GetOrCreateContentTypeAsync(
            page.Id, "home", "Home",
            new[]
            {
                Usage(titleFieldId, required: true, showInList: true, order: 0),
                Usage(bodyFieldId, order: 1)
            });

        await EnsureContentAsync(
            contentType.Id, page.Id, EnglishCulture, "", Jan2026,
            new Dictionary<string, object?>
            {
                ["title"] = "Welcome to Dignite",
                ["body"] = "<p>Dignite is a small headless CMS for building and running content-driven websites. " +
                           "This is the demo site of the local host: change anything, it is only seed data.</p>"
            });

        await EnsureContentAsync(
            contentType.Id, page.Id, ChineseCulture, "", Jan2026,
            new Dictionary<string, object?>
            {
                ["title"] = "欢迎来到 Dignite",
                ["body"] = "<p>Dignite 是一个小型的无头 CMS，用来搭建和运营内容驱动的网站。这是本地 Host 的演示站点，" +
                           "可以随意修改，它只是种子数据。</p>"
            });
    }

    /// <summary>
    /// Carries the two composite field types as well as the scalar ones, so a fresh database has a
    /// worked example of each: <c>Matrix</c> (a repeatable list of polymorphic blocks) and <c>Table</c>
    /// (a homogeneous grid over one column schema). Both are flex-fields kernel built-ins as of
    /// 10.0.0-rc.16; nothing in Site declares them any more, which is precisely why having them in the
    /// seed is worth the lines - otherwise the only way to see either type render is to hand-build a
    /// configuration in the admin UI first.
    /// <para>
    /// Their configurations are built from the kernel's own <c>MatrixConfiguration</c>/
    /// <c>TableConfiguration</c> rather than hand-written dictionaries, so a rename or reshape upstream
    /// breaks this at compile time instead of producing a seed that silently stores an unreadable
    /// configuration. Values likewise use <c>MatrixBlockValue</c>/<c>TableRow</c>: <c>ContentManager</c>
    /// runs them through the field type's own <c>Normalize</c> on the way in, which is what turns them
    /// into the canonical camelCase wire shape every reader downstream expects.
    /// </para>
    /// </summary>
    private async Task<Page> SeedAboutAsync(Guid titleFieldId, Guid bodyFieldId)
    {
        var page = await GetOrCreatePageAsync("about", "About", "/about");

        var highlightsFieldId = await GetOrCreateFieldAsync(
            "about_highlights", "Highlights", MatrixFieldType.ControlName,
            "Repeatable blocks shown down the About page - either a paragraph or a headline number.",
            new MatrixConfiguration
            {
                BlockTypes = new List<MatrixBlockType>
                {
                    new()
                    {
                        Name = "paragraph",
                        DisplayName = "Paragraph",
                        Fields = new List<InlineFieldDefinition>
                        {
                            new() { Name = "text", DisplayName = "Text", FieldTypeName = "Text", Required = true }
                        }
                    },
                    new()
                    {
                        Name = "stat",
                        DisplayName = "Statistic",
                        Fields = new List<InlineFieldDefinition>
                        {
                            new() { Name = "value", DisplayName = "Value", FieldTypeName = "Text", Required = true },
                            new() { Name = "label", DisplayName = "Label", FieldTypeName = "Text", Required = true }
                        }
                    }
                }
            }.ConfigurationDictionary);

        var milestonesFieldId = await GetOrCreateFieldAsync(
            "about_milestones", "Milestones", TableFieldType.ControlName,
            "One row per milestone, all sharing the same columns.",
            new TableConfiguration
            {
                Columns = new List<InlineFieldDefinition>
                {
                    new() { Name = "year", DisplayName = "Year", FieldTypeName = "Text", Required = true },
                    new() { Name = "event", DisplayName = "Event", FieldTypeName = "Text", Required = true }
                }
            }.ConfigurationDictionary);

        var contentType = await GetOrCreateContentTypeAsync(
            page.Id, "about", "About",
            new[]
            {
                Usage(titleFieldId, required: true, showInList: true, order: 0),
                Usage(bodyFieldId, order: 1),
                Usage(highlightsFieldId, order: 2),
                Usage(milestonesFieldId, order: 3)
            });

        await EnsureContentAsync(
            contentType.Id, page.Id, EnglishCulture, "", Jan2026,
            new Dictionary<string, object?>
            {
                ["title"] = "About Us",
                ["body"] = "<p>Dignite is a small team building tools for editors and developers to manage " +
                           "content together, without either side having to compromise on how they work.</p>",
                ["about_highlights"] = new List<MatrixBlockValue>
                {
                    new()
                    {
                        BlockTypeName = "paragraph",
                        Values = new FlexFieldDictionary
                        {
                            ["text"] = "We started Dignite after one too many projects where the CMS decided " +
                                       "what the site could be."
                        }
                    },
                    new()
                    {
                        BlockTypeName = "stat",
                        Values = new FlexFieldDictionary { ["value"] = "12", ["label"] = "Field types out of the box" }
                    },
                    new()
                    {
                        BlockTypeName = "stat",
                        Values = new FlexFieldDictionary { ["value"] = "2", ["label"] = "Languages in this demo" }
                    }
                },
                ["about_milestones"] = new List<TableRow>
                {
                    new() { Values = new FlexFieldDictionary { ["year"] = "2024", ["event"] = "First internal build" } },
                    new() { Values = new FlexFieldDictionary { ["year"] = "2025", ["event"] = "Flexible fields land" } },
                    new() { Values = new FlexFieldDictionary { ["year"] = "2026", ["event"] = "Themes in Scriban" } }
                }
            });

        return page;
    }

    private sealed record NewsSeed(
        string Slug,
        string Category,
        DateTime PublishTime,
        string Title,
        string Summary,
        string Detail,
        bool Featured = false,
        ContentStatus Status = ContentStatus.Published,
        (string Title, string Summary, string Detail)? Chinese = null);

    /// <summary>
    /// The page that exercises the most: the route carries two field placeholders, <c>category</c> (a
    /// <c>Select</c> the content type marks Searchable, so a request can filter by it and the theme can count
    /// by it) and <c>publishTime</c> (a month), and the slug. So <c>/news</c> lists everything,
    /// <c>/news/product</c> one category, <c>/news/product/2026-08</c> one category in one month, and
    /// <c>/news/product/2026-08/scriban-templates-sandbox</c> an item.
    /// <para>
    /// Besides the 15 items a list shows, there are three that it must not: a draft, one scheduled for the
    /// future, and an archived one. No list, count or sitemap includes them, which is what to look at when
    /// checking the numbers next to the categories. A draft and a scheduled item are not served at their own
    /// address either; an archived item still answers there, so that a link already indexed does not go dead
    /// (<see cref="Content.IsPubliclyAccessible"/>).
    /// </para>
    /// </summary>
    private async Task SeedNewsAsync(Guid titleFieldId, Guid bodyFieldId)
    {
        var page = await GetOrCreatePageAsync(
            "news", "News",
            @"/news/{category:^(company|product|industry|tutorials)$}/{publishTime:yyyy-MM:^\d{4}-(0[1-9]|1[0-2])$}/{slug}",
            SeedTheme.NewsTemplate);

        var categoryFieldId = await GetOrCreateFieldAsync(
            "category", "Category", "Select",
            "The news category. The News page's route filters by it and the theme counts by it.",
            new SelectConfiguration
            {
                Multiple = false,
                Options = new List<SelectListItem>
                {
                    new() { Text = "Company", Value = "company" },
                    new() { Text = "Product", Value = "product" },
                    new() { Text = "Industry", Value = "industry" },
                    new() { Text = "Tutorials", Value = "tutorials" }
                }
            }.ConfigurationDictionary);
        var summaryFieldId = await GetOrCreateFieldAsync(
            "summary", "Summary", "Text", "One sentence for lists and link previews.");
        var featuredFieldId = await GetOrCreateFieldAsync(
            "featured", "Featured", "Boolean", "Shown with a badge.");

        var contentType = await GetOrCreateContentTypeAsync(
            page.Id, "news-item", "News item",
            new[]
            {
                Usage(titleFieldId, required: true, showInList: true, order: 0),
                Usage(categoryFieldId, required: true, searchable: true, showInList: true, order: 1),
                Usage(summaryFieldId, showInList: true, order: 2),
                Usage(featuredFieldId, searchable: true, showInList: true, order: 3),
                Usage(bodyFieldId, order: 4)
            });

        static DateTime At(int month, int day) => new(2026, month, day, 9, 0, 0, DateTimeKind.Utc);

        var items = new NewsSeed[]
        {
            // company: 4 published
            new("dignite-opens-tokyo-office", "company", At(6, 12), "Dignite Opens a New Office in Tokyo",
                "The team now has a home in Marunouchi, a short walk from the people who use the product every day.",
                "The office will host the monthly meetups and the open office hours for customers.",
                Chinese: ("Dignite 在东京开设新办公室",
                    "团队在丸之内有了新的办公室，离每天使用产品的用户只有几步之遥。",
                    "办公室将用于举办每月的线下聚会和面向客户的开放答疑时间。")),
            new("dignite-closes-seed-round", "company", At(8, 5), "Dignite Closes Its Seed Round",
                "The funding will go to the theme tooling and to a second engineer for the editor.",
                "The round was led by two investors who have run content-heavy sites themselves."),
            new("dignite-joins-open-source-fund", "company", At(8, 21), "Dignite Joins an Open Source Fund",
                "A share of every subscription now goes to the libraries the product is built on.",
                "The first payments went out this month, split by how much of the code each library accounts for.",
                Featured: true),
            new("dignite-hires-head-of-design", "company", At(10, 2), "Dignite Hires Its First Head of Design",
                "Themes and the admin UI will get the same designer, and the same standards.",
                "Expect a calmer editor and fewer settings that nobody understands."),

            // product: 5 published
            new("site-2-0-released", "product", At(7, 8), "Dignite Site 2.0: Themes, Releases and Rollbacks",
                "A whole theme is now published as one release, and any release can be made live again.",
                "Editors change the working copy, check it, preview it in a browser and publish it in one step.",
                Featured: true,
                Chinese: ("Dignite Site 2.0：主题、发布与回滚",
                    "整套主题现在作为一个版本发布，任何一个旧版本都可以重新设为线上版本。",
                    "编辑在工作副本里修改，检查，在浏览器里预览，然后一步发布。")),
            new("scriban-templates-sandbox", "product", At(8, 12), "Write Your Theme in Scriban",
                "Templates are stored in the database and run in a sandbox, so a tenant or an AI client can write them.",
                "The sandbox limits loops, output and time, and only lets a template read the data it is given."),
            new("mcp-tools-for-themes", "product", At(9, 9), "Edit Your Theme from an AI Client",
                "Twelve new MCP tools save, check, preview and publish the files of a theme.",
                "The recommended flow is to save, check, fix every error, preview and then publish."),
            new("content-counts-arrive", "product", At(10, 28), "Count Contents by Any Field",
                "A template can now show how many news items each category has, next to the list itself.",
                "The counts follow the same filters as the list, so the numbers and the list never disagree.",
                Featured: true),
            new("url-paging-for-lists", "product", At(11, 1), "Turn the Pages of a List from the Address",
                "Add ?page=2 to the address of a list and the template follows it.",
                "Each page of a list is indexed as its own, with the page number in the canonical address."),

            // industry: 3 published
            new("headless-cms-market-2026", "industry", At(6, 28), "The Headless CMS Market in 2026",
                "More sites are separating where content is written from where it is shown.",
                "The interesting question has moved from whether to go headless to who writes the templates."),
            new("ai-agents-edit-websites", "industry", At(8, 30), "AI Agents Start Editing Websites",
                "Teams are letting agents draft pages and themes, as long as a person publishes them.",
                "The work that remains for people is review, which is why previews and rollbacks matter."),
            new("static-or-dynamic-rendering", "industry", At(9, 18), "Static or Dynamic: A Rendering Checklist",
                "Most sites are neither fully one nor the other, and that is fine.",
                "A page that changes with the visitor is dynamic; everything else can be cached a long time."),

            // tutorials: 3 published
            new("build-a-news-page", "tutorials", At(7, 22), "Build a News Page with Category Filters",
                "A route with a category placeholder is all it takes to give every category its own address.",
                "Mark the field Searchable on the content type, put {category} in the route, and the list filters itself.",
                Chinese: ("用分类筛选搭建新闻页",
                    "只要在路由里放一个分类占位符，每个分类就有了自己的地址。",
                    "在内容类型上把字段标记为可搜索，在路由里写上 {category}，列表就会自动筛选。")),
            new("add-paging-to-a-list", "tutorials", At(9, 25), "Add Paging to a List",
                "Pass the request's page number to the list and write a pager with page_url.",
                "Keep the page size small while testing, so there is more than one page to turn to."),
            new("localize-your-theme", "tutorials", At(10, 14), "Localize Your Theme",
                "Translate the texts a template writes with t, and the contents with their own language.",
                "A content that is not translated is simply not served in the other language."),

            // not shown anywhere: a draft, one scheduled for the future, an archived one
            new("roadmap-2027", "product", At(10, 30), "Roadmap 2027 (draft)",
                "Not ready to share.", "A draft is never served, listed or counted.", Status: ContentStatus.Draft),
            new("year-end-party", "company", new DateTime(2026, 12, 24, 9, 0, 0, DateTimeKind.Utc),
                "Year-End Party", "Scheduled for the future.",
                "Published, but its publish time has not come, so nothing shows it yet."),
            new("legacy-announcement", "industry", At(5, 1), "A Withdrawn Announcement",
                "Withdrawn from the lists.", "An archived content is kept, and still answers at its own address, but no list shows it.",
                Status: ContentStatus.Archived)
        };

        foreach (var item in items)
        {
            await EnsureContentAsync(
                contentType.Id, page.Id, EnglishCulture, item.Slug, item.PublishTime,
                NewsValues(item.Title, item.Category, item.Summary, item.Detail, item.Featured), item.Status);

            if (item.Chinese is { } zh)
            {
                await EnsureContentAsync(
                    contentType.Id, page.Id, ChineseCulture, item.Slug, item.PublishTime,
                    NewsValues(zh.Title, item.Category, zh.Summary, zh.Detail, item.Featured), item.Status);
            }
        }
    }

    private static Dictionary<string, object?> NewsValues(
        string title, string category, string summary, string detail, bool featured)
    {
        return new Dictionary<string, object?>
        {
            ["title"] = title,
            ["category"] = category,
            ["summary"] = summary,
            ["featured"] = featured,
            ["body"] = $"<p>{detail}</p>"
        };
    }

    /// <summary>
    /// A long plain list, to turn the pages of: 21 events and the Winter Fest are published (three pages of
    /// ten), and every eighth one is a draft. The route has a required slug, so <c>/events</c> is the page
    /// itself and every event, in either language, has its own address.
    /// </summary>
    private async Task SeedEventsAsync(Guid titleFieldId, Guid bodyFieldId)
    {
        var page = await GetOrCreatePageAsync("events", "Events", "/events/{slug}", SeedTheme.EventsTemplate);

        var dateFieldId = await GetOrCreateFieldAsync(
            "event_date", "Event date", "DateTime", "The day the event takes place.");
        var locationFieldId = await GetOrCreateFieldAsync(
            "location", "Location", "Text", "Where the event takes place.");

        var contentType = await GetOrCreateContentTypeAsync(
            page.Id, "events", "Events",
            new[]
            {
                Usage(titleFieldId, required: true, showInList: true, order: 0),
                Usage(dateFieldId, required: true, searchable: true, showInList: true, order: 1),
                Usage(locationFieldId, showInList: true, order: 2),
                Usage(bodyFieldId, order: 3)
            });

        await EnsureContentAsync(
            contentType.Id, page.Id, EnglishCulture, "winter-fest", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            EventValues("Winter Fest", new DateTime(2026, 12, 12), "Tokyo",
                "<p>Join us for an afternoon of lightning talks and demos from the community.</p>"));
        await EnsureContentAsync(
            contentType.Id, page.Id, ChineseCulture, "winter-fest", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            EventValues("冬季盛会", new DateTime(2026, 12, 12), "东京",
                "<p>欢迎来参加社区的闪电演讲和演示下午场。</p>"));

        var kinds = new[] { "Meetup", "Workshop", "Lightning Talks", "Office Hours", "Hackathon" };
        var cities = new[] { "Tokyo", "Osaka", "Online", "Shanghai", "Singapore" };

        for (var i = 1; i <= 24; i++)
        {
            var kind = kinds[i % kinds.Length];
            var city = cities[(i * 2) % cities.Length];
            var status = i % 8 == 0 ? ContentStatus.Draft : ContentStatus.Published;

            await EnsureContentAsync(
                contentType.Id, page.Id, EnglishCulture, $"event-{i:00}", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i),
                EventValues($"Dignite {kind} #{i:00}", new DateTime(2026, 12, 15).AddDays(i * 3), city,
                    $"<p>The {kind.ToLowerInvariant()} in {city}. Bring a laptop and a question.</p>"),
                status);
        }
    }

    private static Dictionary<string, object?> EventValues(string title, DateTime date, string location, string body)
    {
        return new Dictionary<string, object?>
        {
            ["title"] = title,
            ["event_date"] = date,
            ["location"] = location,
            ["body"] = body
        };
    }

    private async Task SeedContactAsync(Guid parentPageId, Guid titleFieldId, Guid? seoFieldId)
    {
        var companyNameFieldId = await GetOrCreateFieldAsync(
            "company_name", "公司名称", "Text", "联系页面展示的公司或组织名称。");
        var companyAddressFieldId = await GetOrCreateFieldAsync(
            "company_address", "公司地址", "Text", "公司的办公地址或联系地址。");
        var companyPhoneFieldId = await GetOrCreateFieldAsync(
            "company_phone", "公司电话", "Text", "公司对外联系使用的电话号码。");
        var companyEmailFieldId = await GetOrCreateFieldAsync(
            "company_email", "公司邮箱", "Text", "公司对外联系使用的电子邮箱地址。");
        var companyWechatQrFieldId = await GetOrCreateFieldAsync(
            "company_wechat_qr", "公司微信二维码", FileFieldType.ControlName,
            "公司官方微信或客服微信的二维码图片。",
            new FileFieldConfiguration { FileContainerName = SiteFileContainerNames.Images, UploadFileMultiple = false }
                .ConfigurationDictionary);

        // A child of About in the admin UI's tree: the hierarchy is purely organizational, the route is /contact.
        var page = await GetOrCreatePageAsync("contact", "联系我们", "/contact", parentId: parentPageId);

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

        await EnsureContentAsync(
            contentType.Id, page.Id, EnglishCulture, "", Jan2026,
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
            contentType.Id, page.Id, ChineseCulture, "", Jan2026,
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

    /// <summary>
    /// Two contents at the root of the site, <c>/privacy-policy</c> and <c>/terms-of-service</c>, from one page
    /// whose slug carries a regular expression: any other root-level path is still a 404, and the home page
    /// at <c>/</c> is not disturbed.
    /// </summary>
    private async Task SeedLegalAsync(Guid titleFieldId, Guid bodyFieldId)
    {
        var page = await GetOrCreatePageAsync(
            "legal", "Legal", "/{slug:^(privacy-policy|terms-of-service)$}", SeedTheme.DefaultTemplate);
        var contentType = await GetOrCreateContentTypeAsync(
            page.Id, "legal", "Legal",
            new[]
            {
                Usage(titleFieldId, required: true, showInList: true, order: 0),
                Usage(bodyFieldId, order: 1)
            });

        await EnsureContentAsync(
            contentType.Id, page.Id, EnglishCulture, "privacy-policy", Jan2026,
            new Dictionary<string, object?>
            {
                ["title"] = "Privacy Policy",
                ["body"] = "<p>This demo site collects nothing about you.</p>"
            });
        await EnsureContentAsync(
            contentType.Id, page.Id, EnglishCulture, "terms-of-service", Jan2026,
            new Dictionary<string, object?>
            {
                ["title"] = "Terms of Service",
                ["body"] = "<p>This demo site is provided as it is, for trying things out.</p>"
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

    /// <summary>
    /// Creates the content type when missing, and otherwise leaves an existing one almost entirely
    /// alone - the one exception being requested usages of a field that is not attached to
    /// <i>any</i> content type anywhere, which get appended.
    /// <para>
    /// That exception exists because without it, introducing a new field to this seed half-applies on
    /// every database that already ran an older version of it: <see cref="GetOrCreateFieldAsync"/>
    /// finds the field already exists (created by that earlier run), this method finds the content
    /// type already present and returns it untouched, and the field stays orphaned in the field
    /// library forever - worse than not seeding it at all, and exactly the state introducing
    /// <c>about_highlights</c>/<c>about_milestones</c> left this repo's own dev database in.
    /// </para>
    /// <para>
    /// "Attached nowhere" rather than "missing from this content type" is deliberate: the latter would
    /// silently reattach a field an admin removed from this one content type on purpose, forever. A
    /// field genuinely in use somewhere - even a different content type than this seed method asks
    /// for - is never this seed's to move; only a field with no usage anywhere is unambiguous enough
    /// to touch automatically, and even that only for a <i>local dev convenience</i> seed (see this
    /// class's own remarks) - not a rule this repository would want in a seed that ran against
    /// production data. New fields get appended after the current highest <c>Order</c> rather than at
    /// their declared position, so an existing arrangement someone has deliberately ordered is not
    /// reshuffled underneath them.
    /// </para>
    /// <para>
    /// Values are still not written for them: <see cref="EnsureContentAsync"/> leaves existing content
    /// alone, so on an established database these fields appear on the editor form empty, which is the
    /// right side to err on - the alternative is a seed that overwrites content someone authored.
    /// </para>
    /// </summary>
    private async Task<ContentType> GetOrCreateContentTypeAsync(
        Guid pageId,
        string name,
        string displayName,
        IEnumerable<ContentTypeField> fields)
    {
        var requested = fields.ToList();
        var existing = await _contentTypeRepository.FindByNameAsync(pageId, name);

        if (existing != null)
        {
            var alreadyAttached = existing.GetFieldIds().ToHashSet();
            var toAppend = requested
                .Where(f => !alreadyAttached.Contains(f.FieldId) && !_fieldIdsInUseAnywhere.Contains(f.FieldId))
                .ToList();

            if (toAppend.Count == 0)
            {
                return existing;
            }

            var nextOrder = existing.Fields.Count == 0 ? 0 : existing.Fields.Max(f => f.Order) + 1;
            var merged = existing.Fields.ToList();
            foreach (var usage in toAppend)
            {
                merged.Add(new ContentTypeField(
                    usage.FieldId,
                    required: usage.Required,
                    searchable: usage.Searchable,
                    showInList: usage.ShowInList,
                    displayName: usage.DisplayName,
                    order: nextOrder++));
                _fieldIdsInUseAnywhere.Add(usage.FieldId);
            }

            existing.SetFields(merged);
            return await _contentTypeRepository.UpdateAsync(existing, autoSave: true);
        }

        foreach (var usage in requested)
        {
            _fieldIdsInUseAnywhere.Add(usage.FieldId);
        }

        var contentType = new ContentType(_guidGenerator.Create(), pageId, name, displayName, fields: requested, tenantId: null);
        return await _contentTypeRepository.InsertAsync(contentType, autoSave: true);
    }

    private async Task EnsureContentAsync(
        Guid contentTypeId,
        Guid pageId,
        string cultureName,
        string slug,
        DateTime publishTime,
        Dictionary<string, object?> fieldValues,
        ContentStatus status = ContentStatus.Published)
    {
        if (await _contentRepository.SlugExistsAsync(pageId, cultureName, slug))
        {
            return;
        }

        await _contentManager.CreateAsync(contentTypeId, cultureName, slug, publishTime, status, fieldValues);
    }

    private static ContentTypeField Usage(
        Guid fieldId, bool required = false, bool searchable = false, bool showInList = false, int order = 0)
    {
        return new ContentTypeField(fieldId, required, searchable: searchable, showInList: showInList, order: order);
    }
}

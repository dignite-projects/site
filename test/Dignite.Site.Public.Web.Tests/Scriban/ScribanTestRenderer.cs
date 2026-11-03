using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.Web;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Public.Contents;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Seo;
using Dignite.Site.Public.TagHelpers;
using Dignite.Site.Public.Themes;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// A <see cref="ScribanPageRenderer"/> over one request (<see cref="SiteLanguageTestHost"/>), every service it
/// calls replaced by a recording fake - the renderer's own logic, with no application around it.
/// </summary>
public class ScribanTestRenderer
{
    public ScribanTestRenderer(string enabledLanguages = "en,ja", string path = "/ja/about", SiteScribanOptions? options = null)
    {
        Host = new SiteLanguageTestHost(enabledLanguages, path);
        Options = Microsoft.Extensions.Options.Options.Create(options ?? new SiteScribanOptions());

        var switcher = new SiteLanguageSwitcher(Host.LanguageContext, new HttpContextAccessor { HttpContext = Host.HttpContext });
        ModelBuilder = new ScribanRenderModelBuilder(Host.LanguageContext, switcher);

        Renderer = new ScribanPageRenderer(
            Options,
            new ScribanSandbox(Options, Encoder),
            new ScribanTemplateCache(Options),
            ModelBuilder,
            Localizer,
            Host.LanguageContext,
            Host.Options,
            new UrlHelperFactory(),
            ContentListQuery,
            PartialRenderer,
            FieldService,
            Encoder);
    }

    /// <summary>
    /// The host's encoder, as a site serving CJK text configures it (<c>WebEncoderOptions</c>): markup characters
    /// encoded, letters of any script left as they are.
    /// </summary>
    public static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public SiteLanguageTestHost Host { get; }

    public Microsoft.Extensions.Options.IOptions<SiteScribanOptions> Options { get; }

    public ScribanRenderModelBuilder ModelBuilder { get; }

    public ScribanPageRenderer Renderer { get; }

    public FakeTemplateLocalizer Localizer { get; } = new();

    public FakeContentListQuery ContentListQuery { get; } = new();

    public FakePartialRenderer PartialRenderer { get; } = new();

    public FakeFieldPublicAppService FieldService { get; } = new();

    public ActionContext ActionContext => new(Host.HttpContext, new RouteData(), new ActionDescriptor());

    /// <summary>Where <see cref="RenderSet(string, string?, IReadOnlyDictionary{string, string}?, IReadOnlyDictionary{string, string}?)"/> puts the page template.</summary>
    public const string PagePath = "Default";

    /// <summary>Where it puts the layout, which it also makes the theme's default layout.</summary>
    public const string LayoutPath = "layouts/default";

    /// <summary>Renders <paramref name="page"/> - and <paramref name="layout"/> around it, when given - for <paramref name="viewModel"/>.</summary>
    public Task<string> RenderAsync(string page, string? layout = null, SiteRenderViewModel? viewModel = null)
    {
        return RenderAsync(RenderSet(page, layout), viewModel);
    }

    /// <summary>Renders <paramref name="page"/> with <paramref name="partials"/> (path to body) in the theme next to it.</summary>
    public Task<string> RenderAsync(string page, string? layout, IReadOnlyDictionary<string, string> partials)
    {
        return RenderAsync(RenderSet(page, layout, partials));
    }

    /// <summary>Renders <paramref name="renderSet"/>'s <see cref="PagePath"/> as the page's template.</summary>
    public async Task<string> RenderAsync(SiteThemeRenderSet renderSet, SiteRenderViewModel? viewModel = null)
    {
        return (await RenderPageAsync(renderSet, viewModel)).Html;
    }

    /// <summary>Like <see cref="RenderAsync(SiteThemeRenderSet, SiteRenderViewModel?)"/>, with the layout the page rendered in.</summary>
    public Task<ScribanPageRender> RenderPageAsync(SiteThemeRenderSet renderSet, SiteRenderViewModel? viewModel = null, string templatePath = PagePath)
    {
        return Renderer.RenderAsync(templatePath, renderSet, viewModel ?? PageViewModel(), ActionContext);
    }

    /// <param name="page">The page template's body, at <see cref="PagePath"/>.</param>
    /// <param name="layout">The layout's body, if any, at <see cref="LayoutPath"/> and made the default layout.</param>
    /// <param name="partials">Other templates of the theme: path (<c>partials/card</c>, <c>_card</c>) to body.</param>
    /// <param name="assets">Static files of the theme: path (<c>assets/site.css</c>) to body.</param>
    public static SiteThemeRenderSet RenderSet(
        string page,
        string? layout = null,
        IReadOnlyDictionary<string, string>? partials = null,
        IReadOnlyDictionary<string, string>? assets = null)
    {
        var files = new Dictionary<string, string> { [PagePath] = page };
        if (layout != null)
        {
            files[LayoutPath] = layout;
        }

        foreach (var (path, body) in (partials ?? new Dictionary<string, string>()).Concat(assets ?? new Dictionary<string, string>()))
        {
            files[path] = body;
        }

        return RenderSet(files, layout == null ? null : LayoutPath);
    }

    /// <summary>A theme of <paramref name="files"/> (path to body) with <paramref name="defaultLayout"/>.</summary>
    public static SiteThemeRenderSet RenderSet(IReadOnlyDictionary<string, string> files, string? defaultLayout = null)
    {
        return new SiteThemeRenderSet(Snapshot(files, defaultLayout));
    }

    public static SiteThemeSnapshotDto Snapshot(IReadOnlyDictionary<string, string> files, string? defaultLayout = null)
    {
        return new SiteThemeSnapshotDto
        {
            DefaultLayout = defaultLayout,
            Files = files.Select(file => File(file.Key, file.Value)).ToList()
        };
    }

    public static SiteThemeSnapshotFileDto File(string path, string body)
    {
        return new SiteThemeSnapshotFileDto { Path = path, Body = body, ContentHash = SiteThemePaths.ComputeContentHash(body) };
    }

    public static readonly HeadMetadataDto HeadMetadata = new()
    {
        MetaTitle = "About",
        BaseMetaTitle = "About",
        CanonicalUrl = "https://acme.example/ja/about",
        HreflangAlternates = new List<HreflangAlternateDto>
        {
            new() { CultureName = "en", Url = "https://acme.example/about", Path = "/about" },
            new() { CultureName = "ja", Url = "https://acme.example/ja/about", Path = "/ja/about" }
        }
    };

    public static PageDto Page() => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Name = "about",
        DisplayName = "About us",
        Route = "/about",
        Template = "Default",
        IsActive = true,
        CreationTime = new DateTime(2026, 1, 2, 3, 4, 5),
        ContentTypes = new List<ContentTypeDto>()
    };

    /// <summary>A page (list/index) match: no content.</summary>
    /// <param name="currentPage">The page of a paged list the request asked for.</param>
    public static SiteRenderViewModel PageViewModel(int currentPage = 1) => new()
    {
        Page = Page(),
        CultureName = "ja",
        FilterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = "engineering",
            ["publishTime:yyyy"] = "2026"
        },
        FieldFilters = new Dictionary<string, string> { ["category"] = "engineering" },
        PublishedAfter = new DateTime(2026, 1, 1),
        PublishedBefore = new DateTime(2026, 12, 31, 23, 59, 59),
        HeadMetadata = HeadMetadata,
        CurrentPage = currentPage
    };

    /// <summary>A content match: one content of type "post" with a Title, a Tags and a Gallery field.</summary>
    public static SiteRenderViewModel ContentViewModel() => new()
    {
        Page = Page(),
        CultureName = "ja",
        FieldFilters = new Dictionary<string, string>(),
        HeadMetadata = HeadMetadata,
        Content = Content("my-trip", "My trip")
    };

    public static ContentRenderViewModel Content(string slug, string title)
    {
        var contentType = new ContentTypeDto
        {
            Id = Guid.NewGuid(),
            Name = "post",
            DisplayName = "Post",
            Fields = new List<ContentTypeFieldDto>
            {
                new() { FieldId = Guid.NewGuid(), Required = true, ShowInList = true },
                new() { FieldId = Guid.NewGuid() },
                new() { FieldId = Guid.NewGuid() }
            }
        };

        var content = new ContentDto
        {
            Id = Guid.NewGuid(),
            PageId = Page().Id,
            ContentTypeId = contentType.Id,
            CultureName = "ja",
            Slug = slug,
            Url = "/ja/about/" + slug,
            Status = ContentStatus.Published,
            PublishTime = new DateTime(2026, 8, 15, 9, 30, 0),
            CreationTime = new DateTime(2026, 8, 1),
            FieldValues = new Dictionary<string, object?>
            {
                // As over HTTP...
                ["Title"] = JsonSerializer.SerializeToElement(title),
                ["Gallery"] = JsonSerializer.SerializeToElement(new[] { new { url = "/a.png", name = "a" } }),
                // ...and as a single-process host may still hold them.
                ["Tags"] = new List<string> { "travel", "japan" },
                ["view_count"] = 42
            }
        };

        var fields = new List<FlexFieldValue>
        {
            new(new FlexFieldData { Id = contentType.Fields[0].FieldId, Name = "Title", DisplayName = "Title", FieldTypeName = "Text" },
                required: true, value: content.FieldValues["Title"]),
            new(new FlexFieldData { Id = contentType.Fields[1].FieldId, Name = "Tags", DisplayName = "Tags", FieldTypeName = "Select" },
                value: content.FieldValues["Tags"]),
            new(new FlexFieldData { Id = contentType.Fields[2].FieldId, Name = "Gallery", DisplayName = "Gallery", FieldTypeName = "FileExplorer" },
                value: content.FieldValues["Gallery"])
        };

        return new ContentRenderViewModel { Content = content, ContentType = contentType, Fields = fields };
    }
}

public class FakeTemplateLocalizer : IStringLocalizer<SiteTemplateResource>
{
    public Dictionary<string, string> Texts { get; } = new()
    {
        ["Blog:Name"] = "ブログ",
        ["Emphasis"] = "<em>Read</em>"
    };

    public LocalizedString this[string name] =>
        Texts.TryGetValue(name, out var value)
            ? new LocalizedString(name, value)
            : new LocalizedString(name, name, resourceNotFound: true);

    public LocalizedString this[string name, params object[] arguments] => this[name];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
}

/// <summary>Records each request; answers with <see cref="Result"/>. Never touches the app services it would otherwise call.</summary>
public class FakeContentListQuery : ContentListQuery
{
    public FakeContentListQuery()
        : base(null!, null!, null!, null!, null!)
    {
    }

    public List<ContentListQueryRequest> Requests { get; } = new();

    public List<string> PagesLookedUp { get; } = new();

    public Dictionary<string, PageDto> PagesByName { get; } = new();

    public List<ContentRenderViewModel> Items { get; } = new();

    public int TotalCount { get; set; }

    public override Task<PageDto?> FindPageAsync(string pageName)
    {
        PagesLookedUp.Add(pageName);
        return Task.FromResult<PageDto?>(PagesByName.GetValueOrDefault(pageName));
    }

    public List<(ContentListQueryRequest Request, string FieldName, int MaxResultCount)> CountRequests { get; } = new();

    public List<ContentFieldValueCountDto> Counts { get; } = new();

    public override Task<IReadOnlyList<ContentFieldValueCountDto>> CountByFieldAsync(
        ContentListQueryRequest request, string fieldName, int maxResultCount = 100)
    {
        CountRequests.Add((request, fieldName, maxResultCount));
        return Task.FromResult<IReadOnlyList<ContentFieldValueCountDto>>(Counts.ToList());
    }

    public override Task<ContentListRenderViewModel> QueryAsync(ContentListQueryRequest request)
    {
        Requests.Add(request);
        return Task.FromResult(new ContentListRenderViewModel
        {
            Items = Items.ToList(),
            TotalCount = TotalCount,
            MaxResultCount = request.MaxResultCount,
            CurrentPage = request.CurrentPage
        });
    }
}

public class FakePartialRenderer : IRazorPartialRenderer
{
    public List<(string PartialName, object? Model)> Calls { get; } = new();

    public Task<string> RenderAsync<TModel>(string partialName, TModel model)
    {
        Calls.Add((partialName, model));
        return Task.FromResult($"<div class=\"partial\">{partialName}</div>");
    }
}

/// <summary>Answers from <see cref="Fields"/>, and records the ids each call asked for.</summary>
public class FakeFieldPublicAppService : IFieldPublicAppService
{
    public Dictionary<Guid, FieldDto> Fields { get; } = new();

    public List<IReadOnlyList<Guid>> Calls { get; } = new();

    public Task<FieldDto> GetAsync(Guid id) => Task.FromResult(Fields[id]);

    public Task<ListResultDto<FieldDto>> GetListAsync(IEnumerable<Guid> ids)
    {
        var requested = ids.ToList();
        Calls.Add(requested);
        return Task.FromResult(new ListResultDto<FieldDto>(requested.Where(Fields.ContainsKey).Select(id => Fields[id]).ToList()));
    }
}

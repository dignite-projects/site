using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Web;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Seo;
using Dignite.Site.Public.TagHelpers;
using Dignite.Site.Public.Templates;
using Dignite.Site.Public.Templating;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

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
            SelectOptions,
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

    public FakeSelectFieldOptionsProvider SelectOptions { get; } = new();

    public ActionContext ActionContext => new(Host.HttpContext, new RouteData(), new ActionDescriptor());

    /// <summary>Renders <paramref name="page"/> - and <paramref name="layout"/> around it, when given - for <paramref name="viewModel"/>.</summary>
    public Task<string> RenderAsync(string page, string? layout = null, SiteRenderViewModel? viewModel = null)
    {
        return Renderer.RenderAsync("Default", RenderSet(page, layout), viewModel ?? PageViewModel(), ActionContext);
    }

    public static SiteTemplateRenderSetDto RenderSet(string page, string? layout = null)
    {
        return new SiteTemplateRenderSetDto
        {
            Template = Version(page),
            Layout = layout == null ? null : Version(layout)
        };
    }

    public static SiteTemplateVersionDto Version(string body)
    {
        return new SiteTemplateVersionDto { Id = Guid.NewGuid(), TemplateId = Guid.NewGuid(), Number = 1, Body = body };
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
    public static SiteRenderViewModel PageViewModel() => new()
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
        HeadMetadata = HeadMetadata
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

public class FakeSelectFieldOptionsProvider : SelectFieldOptionsProvider
{
    public FakeSelectFieldOptionsProvider()
        : base(null!, null!, null!)
    {
    }

    public List<(string Page, string Field)> Calls { get; } = new();

    public override Task<IReadOnlyList<SelectListItem>> GetAsync(string pageName, string fieldName)
    {
        Calls.Add((pageName, fieldName));
        return Task.FromResult<IReadOnlyList<SelectListItem>>(new List<SelectListItem>
        {
            new("Engineering", "engineering", false),
            new("Essays", "essays", false)
        });
    }
}

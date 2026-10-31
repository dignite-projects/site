using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dignite.Site.Public.Preview;
using Dignite.Site.Public.Templates;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.Public.Assets;

/// <summary>
/// <c>GET /site-assets/{path}</c>: a published asset's body as it is, typed by its extension, cached for good only
/// at the address of the version it carries.
/// </summary>
public class SiteAssetController_Tests
{
    /// <summary>The id of the published version of <c>assets/site.css</c>, as <c>asset_url</c> writes it (<c>N</c> format).</summary>
    private const string SiteCssVersion = "aaaaaaaa000000000000000000000003";

    /// <summary>The id of an older version of the same asset: what a page that has not been re-rendered still links.</summary>
    private const string OlderVersion = "aaaaaaaa000000000000000000000002";

    private static readonly SiteTemplateVersionDto SiteCss = new()
    {
        Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
        TemplateId = Guid.NewGuid(),
        Number = 3,
        Body = "body { color: #333; }"
    };

    private static (SiteAssetController Controller, DefaultHttpContext HttpContext, FakeTemplateAppService Templates) Create(
        string? query = null,
        Dictionary<string, SiteTemplateVersionDto>? published = null,
        Action<SiteTemplatePreviewTestContext>? preview = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        if (query != null)
        {
            httpContext.Request.QueryString = new QueryString(query);
        }

        var templates = new FakeTemplateAppService(published ?? new Dictionary<string, SiteTemplateVersionDto>
        {
            ["assets/site.css"] = SiteCss,
            ["assets/js/site.js"] = Version("console.log(1);"),
            ["assets/notes.unknown"] = Version("notes")
        });

        // Draft mode only when the test sends a preview cookie.
        var previewContext = new SiteTemplatePreviewTestContext(httpContext, new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance));
        preview?.Invoke(previewContext);

        var controller = new SiteAssetController(templates, previewContext.CreateState())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        return (controller, httpContext, templates);
    }

    private static SiteTemplateVersionDto Version(string body) => new()
    {
        Id = Guid.NewGuid(), TemplateId = Guid.NewGuid(), Number = 1, Body = body
    };

    [Fact]
    public async Task A_Published_Asset_Should_Be_Served_With_Its_Type()
    {
        var (controller, httpContext, templates) = Create();

        var result = await controller.GetAsync("site.css");

        var file = result.ShouldBeOfType<FileContentResult>();
        file.ContentType.ShouldBe("text/css; charset=utf-8");
        Encoding.UTF8.GetString(file.FileContents).ShouldBe(SiteCss.Body);
        templates.RequestedNames.ShouldBe(new[] { "assets/site.css" });
        httpContext.Response.Headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
    }

    [Theory]
    [InlineData("js/site.js", "text/javascript; charset=utf-8")]
    [InlineData("notes.unknown", "text/plain; charset=utf-8")]
    public async Task The_Type_Should_Follow_The_Extension(string path, string contentType)
    {
        var (controller, _, _) = Create();

        var result = await controller.GetAsync(path);

        result.ShouldBeOfType<FileContentResult>().ContentType.ShouldBe(contentType);
    }

    [Theory]
    [InlineData("?v=" + SiteCssVersion)]
    [InlineData("?v=AAAAAAAA000000000000000000000003")]
    public async Task The_Address_Of_The_Published_Version_Should_Be_Cached_For_Good(string query)
    {
        var (controller, httpContext, _) = Create(query);

        await controller.GetAsync("site.css");

        httpContext.Response.Headers.CacheControl.ToString().ShouldBe("public, max-age=31536000, immutable");
    }

    /// <summary>
    /// No id, or one that is not the published version's - an older version's, a version number such as the
    /// <c>?v=3</c> of an address written before ids, text that is no id at all: stored but revalidated, so the
    /// address is never pinned to bytes its id does not name.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("?v=" + OlderVersion)]
    [InlineData("?v=3")]
    [InlineData("?v=abc")]
    [InlineData("?v=zzzzzzzz000000000000000000000003")]
    public async Task Any_Other_Address_Should_Be_Revalidated(string? query)
    {
        var (controller, httpContext, _) = Create(query);

        var result = await controller.GetAsync("site.css");

        result.ShouldBeOfType<FileContentResult>();
        httpContext.Response.Headers.CacheControl.ToString().ShouldBe("public, no-cache");
    }

    [Fact]
    public async Task A_Matching_If_None_Match_Should_Be_Answered_Not_Modified()
    {
        var (first, firstContext, _) = Create("?v=" + SiteCssVersion);
        await first.GetAsync("site.css");
        var entityTag = firstContext.Response.Headers.ETag.ToString();
        entityTag.ShouldBe("\"" + SiteCss.Id.ToString("N") + "\"");

        var (second, secondContext, _) = Create("?v=" + SiteCssVersion);
        secondContext.Request.Headers.IfNoneMatch = entityTag;

        var result = await second.GetAsync("site.css");

        result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        secondContext.Response.Headers.ETag.ToString().ShouldBe(entityTag);
        secondContext.Response.Headers.CacheControl.ToString().ShouldBe("public, max-age=31536000, immutable");
    }

    [Fact]
    public async Task Another_Versions_ETag_Should_Get_The_Body()
    {
        var (controller, httpContext, _) = Create();
        httpContext.Request.Headers.IfNoneMatch = "\"" + Guid.NewGuid().ToString("N") + "\"";

        var result = await controller.GetAsync("site.css");

        result.ShouldBeOfType<FileContentResult>();
    }

    [Fact]
    public async Task An_Unpublished_Asset_Should_Be_Not_Found()
    {
        var (controller, _, templates) = Create();

        var result = await controller.GetAsync("missing.css");

        result.ShouldBeOfType<NotFoundResult>();
        templates.RequestedNames.ShouldBe(new[] { "assets/missing.css" });
    }

    /// <summary>Not a name any asset could have: refused without a query.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../secret.css")]
    [InlineData("a/../site.css")]
    [InlineData("a//site.css")]
    [InlineData("site css")]
    [InlineData("site.css%00")]
    [InlineData(".hidden")]
    [InlineData("css/.hidden")]
    public async Task An_Invalid_Path_Should_Be_Not_Found_Without_A_Query(string? path)
    {
        var (controller, _, templates) = Create();

        var result = await controller.GetAsync(path);

        result.ShouldBeOfType<NotFoundResult>();
        templates.RequestedNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Path_Longer_Than_A_Name_Can_Be_Should_Be_Not_Found_Without_A_Query()
    {
        var (controller, _, templates) = Create();

        var result = await controller.GetAsync(new string('a', SiteTemplateConsts.MaxNameLength) + ".css");

        result.ShouldBeOfType<NotFoundResult>();
        templates.RequestedNames.ShouldBeEmpty();
    }

    /// <summary>
    /// Draft mode: the visitor whose preview cookie names this asset gets the previewed version - which no cache may
    /// keep - without the published one even being looked up.
    /// </summary>
    [Fact]
    public async Task In_Draft_Mode_The_Previewed_Version_Should_Be_Served_Uncached()
    {
        SiteTemplateVersionDto? draft = null;
        var (controller, httpContext, templates) = Create(preview: preview =>
        {
            preview.SetCookie("assets/site.css", 4);
            draft = preview.Versions.Add("assets/site.css", 4, "body { color: red; }");
        });
        httpContext.Request.QueryString = new QueryString("?v=" + draft!.Id.ToString("N"));

        var result = await controller.GetAsync("site.css");

        var file = result.ShouldBeOfType<FileContentResult>();
        file.ContentType.ShouldBe("text/css; charset=utf-8");
        Encoding.UTF8.GetString(file.FileContents).ShouldBe("body { color: red; }");
        templates.RequestedNames.ShouldBeEmpty();
        httpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        httpContext.Response.Headers["X-Robots-Tag"].ToString().ShouldBe("noindex");
        httpContext.Response.Headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
        httpContext.Response.Headers.ETag.ToString().ShouldBe("\"" + draft.Id.ToString("N") + "\"");
    }

    /// <summary>A visitor without the cookie who asks for the previewed version's id gets the published bytes.</summary>
    [Fact]
    public async Task Without_The_Cookie_A_Drafts_Address_Should_Get_The_Published_Bytes()
    {
        var (controller, httpContext, _) = Create("?v=" + Guid.NewGuid().ToString("N"));

        var result = await controller.GetAsync("site.css");

        Encoding.UTF8.GetString(result.ShouldBeOfType<FileContentResult>().FileContents).ShouldBe(SiteCss.Body);
        httpContext.Response.Headers.CacheControl.ToString().ShouldBe("public, no-cache");
        httpContext.Response.Headers.ContainsKey("X-Robots-Tag").ShouldBeFalse();
    }

    [Fact]
    public async Task Previewing_Another_Template_Should_Leave_This_Asset_Published()
    {
        FakePreviewVersionProvider? versions = null;
        var (controller, httpContext, templates) = Create(preview: preview =>
        {
            preview.SetCookie("assets/js/site.js", 2);
            preview.Versions.Add("assets/js/site.js", 2, "console.log(2);");
            versions = preview.Versions;
        });

        var result = await controller.GetAsync("site.css");

        Encoding.UTF8.GetString(result.ShouldBeOfType<FileContentResult>().FileContents).ShouldBe(SiteCss.Body);
        templates.RequestedNames.ShouldBe(new[] { "assets/site.css" });
        versions!.Requests.ShouldBeEmpty();
        httpContext.Response.Headers.CacheControl.ToString().ShouldBe("public, no-cache");
    }

    private class FakeTemplateAppService(Dictionary<string, SiteTemplateVersionDto> published) : ISiteTemplatePublicAppService
    {
        public List<string> RequestedNames { get; } = new();

        public Task<SiteTemplateRenderSetDto> GetRenderSetAsync(string name) => throw new NotSupportedException();

        public Task<SiteTemplateVersionDto?> FindPublishedAsync(string name)
        {
            RequestedNames.Add(name);
            return Task.FromResult<SiteTemplateVersionDto?>(published.GetValueOrDefault(name));
        }
    }
}

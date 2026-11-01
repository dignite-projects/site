using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dignite.Site.Public.Themes;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Assets;

/// <summary>
/// <c>GET /site-assets/{path}</c>: a static file of the live release as it is, typed by its extension, cached for good
/// only at the address of the content it carries.
/// </summary>
public class SiteAssetController_Tests
{
    private const string SiteCssBody = "body { color: #333; }";

    /// <summary>The content hash of the live <c>assets/site.css</c>, as <c>asset_url</c> writes it.</summary>
    private static readonly string SiteCssHash = SiteThemePaths.ComputeContentHash(SiteCssBody);

    /// <summary>The hash of an older content of the same file: what a page that has not been re-rendered still links.</summary>
    private static readonly string OlderHash = SiteThemePaths.ComputeContentHash("body { color: #000; }");

    private static (SiteAssetController Controller, DefaultHttpContext HttpContext, FakeThemeAppService Themes) Create(
        string? query = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        if (query != null)
        {
            httpContext.Request.QueryString = new QueryString(query);
        }

        var themes = new FakeThemeAppService(new Dictionary<string, string>
        {
            ["assets/site.css"] = SiteCssBody,
            ["js/site.js"] = "console.log(1);",
            ["notes.unknown"] = "notes"
        });

        var controller = new SiteAssetController(themes)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        return (controller, httpContext, themes);
    }

    [Fact]
    public async Task A_Live_Static_File_Should_Be_Served_With_Its_Type()
    {
        var (controller, httpContext, themes) = Create();

        var result = await controller.GetAsync("assets/site.css");

        var file = result.ShouldBeOfType<FileContentResult>();
        file.ContentType.ShouldBe("text/css; charset=utf-8");
        Encoding.UTF8.GetString(file.FileContents).ShouldBe(SiteCssBody);
        themes.RequestedPaths.ShouldBe(new[] { "assets/site.css" });
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

    [Fact]
    public async Task The_Address_Of_The_Live_Content_Should_Be_Cached_For_Good()
    {
        foreach (var hash in new[] { SiteCssHash, SiteCssHash.ToUpperInvariant() })
        {
            var (controller, httpContext, _) = Create("?v=" + hash);

            await controller.GetAsync("assets/site.css");

            httpContext.Response.Headers.CacheControl.ToString().ShouldBe("public, max-age=31536000, immutable");
        }
    }

    /// <summary>
    /// No hash, or one that is not the live content's - an older content's, text that is no hash at all: stored but
    /// revalidated, so the address is never pinned to bytes its hash does not name.
    /// </summary>
    [Fact]
    public async Task Any_Other_Address_Should_Be_Revalidated()
    {
        foreach (var query in new[] { null, "?v=" + OlderHash, "?v=3", "?v=abc" })
        {
            var (controller, httpContext, _) = Create(query);

            var result = await controller.GetAsync("assets/site.css");

            result.ShouldBeOfType<FileContentResult>();
            httpContext.Response.Headers.CacheControl.ToString().ShouldBe("public, no-cache");
        }
    }

    [Fact]
    public async Task A_Matching_If_None_Match_Should_Be_Answered_Not_Modified()
    {
        var (first, firstContext, _) = Create("?v=" + SiteCssHash);
        await first.GetAsync("assets/site.css");
        var entityTag = firstContext.Response.Headers.ETag.ToString();
        entityTag.ShouldBe("\"" + SiteCssHash + "\"");

        var (second, secondContext, _) = Create("?v=" + SiteCssHash);
        secondContext.Request.Headers.IfNoneMatch = entityTag;

        var result = await second.GetAsync("assets/site.css");

        result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        secondContext.Response.Headers.ETag.ToString().ShouldBe(entityTag);
        secondContext.Response.Headers.CacheControl.ToString().ShouldBe("public, max-age=31536000, immutable");
    }

    [Fact]
    public async Task Another_Contents_ETag_Should_Get_The_Body()
    {
        var (controller, httpContext, _) = Create();
        httpContext.Request.Headers.IfNoneMatch = "\"" + OlderHash + "\"";

        var result = await controller.GetAsync("assets/site.css");

        result.ShouldBeOfType<FileContentResult>();
    }

    [Fact]
    public async Task A_File_The_Live_Release_Does_Not_Have_Should_Be_Not_Found()
    {
        var (controller, _, themes) = Create();

        var result = await controller.GetAsync("assets/missing.css");

        result.ShouldBeOfType<NotFoundResult>();
        themes.RequestedPaths.ShouldBe(new[] { "assets/missing.css" });
    }

    /// <summary>Not a path any static file could have - a template's among them: refused without a query.</summary>
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
    [InlineData("Events")]
    [InlineData("layouts/default")]
    [InlineData(" assets/site.css")]
    public async Task An_Invalid_Path_Should_Be_Not_Found_Without_A_Query(string? path)
    {
        var (controller, _, themes) = Create();

        var result = await controller.GetAsync(path);

        result.ShouldBeOfType<NotFoundResult>();
        themes.RequestedPaths.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Path_Longer_Than_A_Path_Can_Be_Should_Be_Not_Found_Without_A_Query()
    {
        var (controller, _, themes) = Create();

        var result = await controller.GetAsync(new string('a', SiteThemeConsts.MaxPathLength) + ".css");

        result.ShouldBeOfType<NotFoundResult>();
        themes.RequestedPaths.ShouldBeEmpty();
    }

    private class FakeThemeAppService(Dictionary<string, string> live) : ISiteThemePublicAppService
    {
        public List<string> RequestedPaths { get; } = new();

        public Task<SiteLiveThemeDto> GetLiveAsync(Guid? knownReleaseId = null) => throw new NotSupportedException();

        public Task<SiteThemeAssetDto?> FindLiveAssetAsync(string path)
        {
            RequestedPaths.Add(path);
            return Task.FromResult(live.TryGetValue(path, out var body)
                ? new SiteThemeAssetDto { Path = path, ContentHash = SiteThemePaths.ComputeContentHash(body), Body = body }
                : null);
        }
    }
}

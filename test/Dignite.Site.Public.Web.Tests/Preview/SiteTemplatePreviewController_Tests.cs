using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Preview;

/// <summary><c>GET /site-preview/exit</c>: the cookie goes, and the visitor goes back - but only ever to this site.</summary>
public class SiteTemplatePreviewController_Tests
{
    private static (SiteTemplatePreviewController Controller, DefaultHttpContext HttpContext) Create(string pathBase = "")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.PathBase = pathBase;
        httpContext.Request.Path = "/" + SiteTemplatePreviewConsts.ExitPath;

        var controller = new SiteTemplatePreviewController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext, RouteData = new RouteData() }
        };
        controller.Url = new UrlHelper(controller.ControllerContext);

        return (controller, httpContext);
    }

    [Fact]
    public void Exit_Should_Delete_The_Cookie_And_Go_Back()
    {
        var (controller, httpContext) = Create(pathBase: "/site");

        var result = controller.Exit("/site/ja/about?x=1");

        result.ShouldBeOfType<LocalRedirectResult>().Url.ShouldBe("/site/ja/about?x=1");

        var cookie = SetCookieHeaderValue.ParseList(httpContext.Response.Headers.SetCookie.Select(value => value!).ToList())
            .ShouldHaveSingleItem();
        cookie.Name.ToString().ShouldBe(SiteTemplatePreviewConsts.CookieName);
        cookie.Value.ToString().ShouldBeEmpty();
        cookie.Path.ToString().ShouldBe("/site");
        cookie.Expires!.Value.Year.ShouldBe(1970);

        httpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        httpContext.Response.Headers["X-Robots-Tag"].ToString().ShouldBe("noindex");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example/")]
    [InlineData("javascript:alert(1)")]
    public void Anything_But_A_Local_Address_Should_Go_Home(string? returnUrl)
    {
        var (controller, _) = Create();

        var result = controller.Exit(returnUrl);

        result.ShouldBeOfType<LocalRedirectResult>().Url.ShouldBe("~/");
    }
}

using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Seo;

public class HttpContextSiteRequestBaseUrlAccessor_Tests
{
    [Fact]
    public void Should_Be_The_Scheme_Host_And_Path_Base_Of_The_Request()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("tenant.platform.example");
        context.Request.PathBase = "/site";
        context.Request.Path = "/blog/hello";

        Accessor(context).GetBaseUrl().ShouldBe("https://tenant.platform.example/site");
    }

    [Fact]
    public void Should_Be_Null_Outside_A_Request()
    {
        Accessor(null).GetBaseUrl().ShouldBeNull();
    }

    private static HttpContextSiteRequestBaseUrlAccessor Accessor(HttpContext? context) =>
        new(new HttpContextAccessor { HttpContext = context });
}

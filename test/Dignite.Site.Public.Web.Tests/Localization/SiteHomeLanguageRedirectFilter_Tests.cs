using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Localization;

/// <summary>The opt-in <c>/</c> redirect to a visitor's own language (GitHub issue #75).</summary>
public class SiteHomeLanguageRedirectFilter_Tests
{
    private readonly SiteLanguageTestHost _host;

    public SiteHomeLanguageRedirectFilter_Tests()
    {
        _host = new SiteLanguageTestHost("en,ja,zh-Hant");
        _host.Options.Value.RedirectHomeToVisitorLanguage = true;
    }

    private HttpRequest Request => _host.HttpContext.Request;

    /// <returns>The redirect target, or null when the request went on to the page.</returns>
    private async Task<string?> ExecuteAsync()
    {
        var actionContext = new ActionContext(_host.HttpContext, new RouteData(), new ActionDescriptor());
        var context = new ResourceExecutingContext(actionContext, new List<IFilterMetadata>(), new List<IValueProviderFactory>());

        var reachedPage = false;
        await new SiteHomeLanguageRedirectFilter(_host.Options).OnResourceExecutionAsync(context, () =>
        {
            reachedPage = true;
            return Task.FromResult(new ResourceExecutedContext(actionContext, new List<IFilterMetadata>()));
        });

        if (reachedPage)
        {
            context.Result.ShouldBeNull();
            return null;
        }

        var redirect = context.Result.ShouldBeOfType<RedirectResult>();
        redirect.Permanent.ShouldBeFalse();
        return redirect.Url;
    }

    [Fact]
    public async Task Should_Follow_Accept_Language()
    {
        Request.Headers.AcceptLanguage = "ja";

        (await ExecuteAsync()).ShouldBe("/ja");
        _host.HttpContext.Response.Headers.Vary.ToString().ShouldBe("Accept-Language, Cookie, Sec-Fetch-Site");
    }

    [Fact]
    public async Task The_Cookie_Should_Beat_Accept_Language()
    {
        Request.Headers.AcceptLanguage = "ja";
        Request.Headers.Cookie = "site-lang=zh-Hant";

        (await ExecuteAsync()).ShouldBe("/zh-Hant");
    }

    /// <summary>A visitor who chose the default language stays on "/", whatever their browser says.</summary>
    [Fact]
    public async Task A_Cookie_Naming_The_Default_Language_Should_Keep_The_Visitor_Home()
    {
        Request.Headers.AcceptLanguage = "ja";
        Request.Headers.Cookie = "site-lang=en";

        (await ExecuteAsync()).ShouldBeNull();
    }

    /// <summary>A language the site no longer serves is no choice at all.</summary>
    [Fact]
    public async Task A_Cookie_Naming_A_Language_The_Site_Does_Not_Serve_Should_Be_Ignored()
    {
        Request.Headers.AcceptLanguage = "ja";
        Request.Headers.Cookie = "site-lang=de";

        (await ExecuteAsync()).ShouldBe("/ja");
    }

    [Fact]
    public async Task Should_Honor_Quality_Values()
    {
        Request.Headers.AcceptLanguage = "fr;q=0.9, ja;q=0.5, zh-TW;q=0.8";

        // fr is not served; zh-TW (0.8) outranks ja (0.5) and reaches zh-Hant through its parent chain.
        (await ExecuteAsync()).ShouldBe("/zh-Hant");
    }

    [Fact]
    public async Task A_Language_Marked_Not_Acceptable_Should_Be_Skipped()
    {
        Request.Headers.AcceptLanguage = "ja;q=0, en;q=0.1";

        (await ExecuteAsync()).ShouldBeNull();
    }

    [Theory]
    [InlineData("en-US,ja;q=0.8")]
    [InlineData("de,fr")]
    public async Task The_Default_Language_Or_No_Match_Should_Leave_Home_Alone(string acceptLanguage)
    {
        Request.Headers.AcceptLanguage = acceptLanguage;

        (await ExecuteAsync()).ShouldBeNull();
    }

    /// <summary>What a crawler gets: no Accept-Language, no redirect.</summary>
    [Fact]
    public async Task No_Accept_Language_Should_Leave_Home_Alone()
    {
        (await ExecuteAsync()).ShouldBeNull();
    }

    /// <summary>
    /// A link followed within the site - "English" in a switcher, a default-language page's logo - means
    /// the visitor asked for "/" itself, cookie or not.
    /// </summary>
    [Fact]
    public async Task A_Navigation_From_Within_The_Site_Should_Not_Be_Redirected()
    {
        Request.Headers.AcceptLanguage = "ja";
        Request.Headers.Cookie = "site-lang=ja";
        Request.Headers["Sec-Fetch-Site"] = "same-origin";

        (await ExecuteAsync()).ShouldBeNull();
    }

    [Theory]
    [InlineData("cross-site")]
    [InlineData("none")]
    public async Task An_Arrival_From_Elsewhere_Should_Be_Redirected(string secFetchSite)
    {
        Request.Headers.AcceptLanguage = "ja";
        Request.Headers["Sec-Fetch-Site"] = secFetchSite;

        (await ExecuteAsync()).ShouldBe("/ja");
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task Should_Only_Redirect_Get_And_Head(string method)
    {
        Request.Method = method;
        Request.Headers.AcceptLanguage = "ja";

        (await ExecuteAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Redirect_Head_Too()
    {
        Request.Method = HttpMethods.Head;
        Request.Headers.AcceptLanguage = "ja";

        (await ExecuteAsync()).ShouldBe("/ja");
    }

    [Theory]
    [InlineData("/about")]
    [InlineData("/ja")]
    public async Task Should_Only_Redirect_The_Root(string path)
    {
        Request.Path = path;
        Request.Headers.AcceptLanguage = "ja";

        (await ExecuteAsync()).ShouldBeNull();
        _host.HttpContext.Response.Headers.ContainsKey(HeaderNames.Vary).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Keep_The_Path_Base_And_Query_String()
    {
        Request.PathBase = "/site";
        Request.QueryString = new QueryString("?utm_source=mail");
        Request.Headers.AcceptLanguage = "ja";

        (await ExecuteAsync()).ShouldBe("/site/ja?utm_source=mail");
    }

    [Fact]
    public async Task Should_Do_Nothing_Unless_Enabled()
    {
        _host.Options.Value.RedirectHomeToVisitorLanguage = false;
        Request.Headers.AcceptLanguage = "ja";

        (await ExecuteAsync()).ShouldBeNull();
        _host.Settings.ReadCount.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Read_The_Cookie_Name_From_The_Options()
    {
        _host.Options.Value.CookieName = "lang";
        Request.Headers.Cookie = "lang=ja";

        (await ExecuteAsync()).ShouldBe("/ja");
    }
}

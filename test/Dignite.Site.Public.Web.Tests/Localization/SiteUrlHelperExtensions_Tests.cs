using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Localization;

/// <summary><c>Url.SiteContent</c> - in-site paths built in code, in the page's language (GitHub issue #75).</summary>
public class SiteUrlHelperExtensions_Tests
{
    private static async Task<IUrlHelper> LoadedUrlHelperAsync(SiteLanguageTestHost host)
    {
        await host.LanguageContext.GetLanguagesAsync();
        return UrlHelper(host);
    }

    private static IUrlHelper UrlHelper(SiteLanguageTestHost host) =>
        new UrlHelper(new ActionContext(host.HttpContext, new RouteData(), new ActionDescriptor()));

    [Theory]
    [InlineData("/ja/about", "", "~/contact", "/ja/contact")]
    [InlineData("/ja/about", "/site", "~/contact", "/site/ja/contact")]
    [InlineData("/about", "", "~/contact", "/contact")]
    [InlineData("/ja", "", "~/Account/Login", "/Account/Login")]
    public async Task Should_Localize_Into_The_Pages_Language(string path, string pathBase, string url, string expected)
    {
        var host = new SiteLanguageTestHost("en,ja", path, pathBase);

        (await LoadedUrlHelperAsync(host)).SiteContent(url).ShouldBe(expected);
    }

    [Theory]
    [InlineData("/ja/about", "en", "/contact")]
    [InlineData("/about", "ja", "/ja/contact")]
    public async Task Should_Localize_Into_A_Language_Given_Explicitly(string path, string culture, string expected)
    {
        var host = new SiteLanguageTestHost("en,ja", path);

        (await LoadedUrlHelperAsync(host)).SiteContent("~/contact", culture).ShouldBe(expected);
    }

    /// <summary>Outside a view the languages are not loaded: a clear error, not a URL in the wrong language.</summary>
    [Fact]
    public void Should_Fail_Before_The_Languages_Are_Loaded()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");

        Should.Throw<InvalidOperationException>(() => UrlHelper(host).SiteContent("~/contact"));
    }
}

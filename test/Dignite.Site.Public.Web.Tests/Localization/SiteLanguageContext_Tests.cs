using System;
using System.Threading.Tasks;
using Dignite.Site.Seo;
using Dignite.Site.Settings;
using Microsoft.AspNetCore.Diagnostics;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// The site's languages for one request, and the language the request is in (GitHub issue #75).
/// </summary>
public class SiteLanguageContext_Tests
{
    [Theory]
    [InlineData("/", "en")]
    [InlineData("/about", "en")]
    [InlineData("/ja", "ja")]
    [InlineData("/ja/about", "ja")]
    [InlineData("/zh-Hans/blog/my-post", "zh-Hans")]
    public async Task Should_Read_The_Language_Off_The_Path(string path, string expected)
    {
        var host = new SiteLanguageTestHost("en,ja,zh-Hans", path);

        (await host.LanguageContext.GetCurrentCultureNameAsync()).ShouldBe(expected);
    }

    /// <summary>
    /// "de" is a real culture the site does not serve, so /de/about is a page path in the default language -
    /// what routing makes of it too.
    /// </summary>
    [Fact]
    public async Task A_Culture_The_Site_Does_Not_Serve_Should_Be_The_Default_Language()
    {
        var host = new SiteLanguageTestHost("en,ja", "/de/about");

        (await host.LanguageContext.GetCurrentCultureNameAsync()).ShouldBe("en");
    }

    [Theory]
    [InlineData("/JA/about")]
    [InlineData("/Ja")]
    public async Task Should_Read_The_Language_In_Any_Casing(string path)
    {
        var host = new SiteLanguageTestHost("en,ja", path);

        (await host.LanguageContext.GetCurrentCultureNameAsync()).ShouldBe("ja");
    }

    /// <summary>A route named after the default language is a route, not a prefix.</summary>
    [Fact]
    public async Task The_Default_Language_Is_Never_A_Prefix()
    {
        var host = new SiteLanguageTestHost("ja,en", "/ja/about");

        (await host.LanguageContext.GetCurrentCultureNameAsync()).ShouldBe("ja");
        host.LanguageContext.Languages!.DefaultCultureName.ShouldBe("ja");
    }

    /// <summary>A re-executed error page is in the language of the page that failed, not of <c>/Error</c>.</summary>
    [Fact]
    public async Task A_Re_Executed_Error_Page_Should_Keep_The_Original_Language()
    {
        var host = new SiteLanguageTestHost("en,ja", "/Error");
        host.HttpContext.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature
        {
            OriginalPath = "/ja/missing",
            OriginalPathBase = ""
        });

        (await host.LanguageContext.GetCurrentCultureNameAsync()).ShouldBe("ja");
    }

    [Fact]
    public async Task Should_Read_The_Setting_Once_Per_Request()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja");

        await host.LanguageContext.GetLanguagesAsync();
        await host.LanguageContext.GetLanguagesAsync();
        await host.LanguageContext.GetCurrentCultureNameAsync();

        host.Settings.ReadCount.ShouldBe(1);
    }

    /// <summary>
    /// Loaded per tenant: a view that switches tenant neither reuses the other tenant's list nor loses its
    /// own - a synchronous caller after the switch still finds it.
    /// </summary>
    [Fact]
    public async Task Should_Keep_Each_Tenants_Languages_Apart()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja/about");
        await host.LanguageContext.GetLanguagesAsync();

        using (host.CurrentTenant.Change(Guid.NewGuid(), "acme"))
        {
            host.LanguageContext.Languages.ShouldBeNull();

            host.Settings[SiteSettings.EnabledLanguages] = "fr";
            (await host.LanguageContext.GetLanguagesAsync()).DefaultCultureName.ShouldBe("fr");
        }

        var languages = host.LanguageContext.GetLoadedLanguages();
        languages.DefaultCultureName.ShouldBe("en");
        host.LanguageContext.GetCurrentCultureName(languages).ShouldBe("ja");
        host.Settings.ReadCount.ShouldBe(2);
    }

    /// <summary>Languages a Site page's route match already carries are used as they are, not read again.</summary>
    [Fact]
    public async Task Supplied_Languages_Should_Not_Be_Read_Again()
    {
        var host = new SiteLanguageTestHost("en", "/ja/about");

        host.LanguageContext.SetLanguages(SiteLanguages.Parse("en,ja"));

        (await host.LanguageContext.GetCurrentCultureNameAsync()).ShouldBe("ja");
        host.Settings.ReadCount.ShouldBe(0);
    }

    /// <summary>Synchronous callers cannot load the languages - they get a clear error, not a wrong URL.</summary>
    [Fact]
    public void A_Synchronous_Caller_Should_Fail_Before_The_Languages_Are_Loaded()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja");

        Should.Throw<InvalidOperationException>(() => host.LanguageContext.GetLoadedLanguages());
    }

    [Fact]
    public async Task A_Synchronous_Caller_Should_Work_Once_They_Are()
    {
        var host = new SiteLanguageTestHost("en,ja", "/ja");

        await host.LanguageContext.GetLanguagesAsync();

        var languages = host.LanguageContext.GetLoadedLanguages();

        host.LanguageContext.GetCurrentCultureName(languages).ShouldBe("ja");
    }

    /// <summary>
    /// An unreadable setting fails the request, as it does the Site API routing the same request - a quiet
    /// fallback to one language would disagree with the routing on every link.
    /// </summary>
    [Fact]
    public async Task An_Unreadable_Setting_Should_Not_Be_Swallowed()
    {
        var host = new SiteLanguageTestHost("ja,en", "/en/about");
        host.Settings.Failure = new InvalidOperationException("remote configuration unavailable");

        await Should.ThrowAsync<InvalidOperationException>(() => host.LanguageContext.GetLanguagesAsync());
    }
}

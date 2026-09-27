using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Public.Seo;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Localization;

/// <summary>The data behind a language switcher (GitHub issue #75).</summary>
public class SiteLanguageSwitcher_Tests
{
    private const string PrimaryDomain = "https://acme.example";

    /// <summary>Hreflang alternates the way the server builds them: absolute on the primary domain, plus the site path.</summary>
    private static HeadMetadataDto Metadata(params (string Culture, string Url)[] alternates) =>
        MetadataOn(PrimaryDomain, alternates.Select(a => (a.Culture, a.Url[PrimaryDomain.Length..])).ToArray());

    private static HeadMetadataDto MetadataOn(string baseUrl, params (string Culture, string Path)[] alternates) => new()
    {
        MetaTitle = "My trip",
        BaseMetaTitle = "My trip",
        CanonicalUrl = baseUrl + alternates[0].Path,
        HreflangAlternates = alternates
            .Select(a => new HreflangAlternateDto { CultureName = a.Culture, Url = baseUrl + a.Path, Path = a.Path })
            .ToList()
    };

    private static SiteLanguageSwitcher Switcher(SiteLanguageTestHost host) =>
        new(host.LanguageContext, new HttpContextAccessor { HttpContext = host.HttpContext });

    [Fact]
    public async Task Should_Link_Each_Language_To_The_Pages_Translation()
    {
        var host = new SiteLanguageTestHost("en,ja,fr", "/ja/blog/my-trip");
        host.LanguageContext.HeadMetadata = Metadata(
            ("en", "https://acme.example/blog/my-trip"),
            ("ja", "https://acme.example/ja/blog/my-trip"),
            ("fr", "https://acme.example/fr/blog/my-trip"));

        var items = await Switcher(host).GetItemsAsync();

        items.Select(i => i.CultureName).ShouldBe(new[] { "en", "ja", "fr" });
        items.Select(i => i.Url).ShouldBe(new[] { "/blog/my-trip", "/ja/blog/my-trip", "/fr/blog/my-trip" });
        items.ShouldAllBe(i => i.IsTranslation);
    }

    /// <summary>
    /// A translation's URL is its hreflang URL - it need not differ from the current path by its prefix only,
    /// e.g. when a route carries a date the translations were published on at different times.
    /// </summary>
    [Fact]
    public async Task Should_Take_A_Translations_Url_As_Hreflang_Gives_It()
    {
        var host = new SiteLanguageTestHost("en,ja", "/blog/2026-08/my-trip");
        host.LanguageContext.HeadMetadata = Metadata(
            ("en", "https://acme.example/blog/2026-08/my-trip"),
            ("ja", "https://acme.example/ja/blog/2026-09/my-trip"));

        var items = await Switcher(host).GetItemsAsync();

        items[1].Url.ShouldBe("/ja/blog/2026-09/my-trip");
    }

    [Fact]
    public async Task A_Missing_Translation_Should_Fall_Back_To_That_Languages_Home_Page()
    {
        var host = new SiteLanguageTestHost("en,ja,fr", "/blog/my-trip");
        host.LanguageContext.HeadMetadata = Metadata(
            ("en", "https://acme.example/blog/my-trip"),
            ("ja", "https://acme.example/ja/blog/my-trip"));

        var fr = (await Switcher(host).GetItemsAsync()).Single(i => i.CultureName == "fr");

        fr.Url.ShouldBe("/fr");
        fr.IsTranslation.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Mark_The_Current_Language()
    {
        var host = new SiteLanguageTestHost("en,ja,fr", "/ja/about");

        var items = await Switcher(host).GetItemsAsync();

        items.Single(i => i.IsCurrent).CultureName.ShouldBe("ja");
    }

    /// <summary>Off a Site page there is no head metadata: every language links to its home page.</summary>
    [Fact]
    public async Task Without_Head_Metadata_Every_Language_Should_Link_Home()
    {
        var host = new SiteLanguageTestHost("en,ja", "/Account/Login", pathBase: "/site");

        var items = await Switcher(host).GetItemsAsync();

        items.Select(i => i.Url).ShouldBe(new[] { "/site/", "/site/ja" });
        items.ShouldAllBe(i => !i.IsTranslation);
        items.Single(i => i.IsCurrent).CultureName.ShouldBe("en");
    }

    /// <summary>
    /// Translations and home pages are built from the same two parts - the request's PathBase and a site
    /// path - so they agree even when the primary domain is configured with a different base path.
    /// </summary>
    [Fact]
    public async Task Translations_Should_Follow_The_Requests_Path_Base()
    {
        var host = new SiteLanguageTestHost("en,ja,fr", "/ja/about", pathBase: "/preview");
        host.LanguageContext.HeadMetadata = MetadataOn("https://acme.example/site", ("en", "/about"), ("ja", "/ja/about"));

        var items = await Switcher(host).GetItemsAsync();

        items.Select(i => i.Url).ShouldBe(new[] { "/preview/about", "/preview/ja/about", "/preview/fr" });
    }

    /// <summary>An answer without the site path - from an older server - still links, to the absolute URL.</summary>
    [Fact]
    public async Task A_Translation_Without_A_Site_Path_Should_Use_Its_Absolute_Url()
    {
        var host = new SiteLanguageTestHost("en,ja", "/about");
        var metadata = Metadata(("en", "https://acme.example/about"), ("ja", "https://acme.example/ja/about"));
        metadata.HreflangAlternates.ForEach(a => a.Path = null!);

        var items = await Switcher(host).GetItemsAsync(metadata);

        items[1].Url.ShouldBe("https://acme.example/ja/about");
    }

    [Fact]
    public async Task Metadata_Passed_In_Should_Win_Over_The_Recorded_One()
    {
        var host = new SiteLanguageTestHost("en,ja", "/about");
        host.LanguageContext.HeadMetadata = Metadata(("en", "https://acme.example/about"));

        var items = await Switcher(host).GetItemsAsync(Metadata(
            ("en", "https://acme.example/team"),
            ("ja", "https://acme.example/ja/team")));

        items.Select(i => i.Url).ShouldBe(new[] { "/team", "/ja/team" });
    }
}

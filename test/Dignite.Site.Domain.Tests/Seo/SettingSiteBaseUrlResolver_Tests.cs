using System.Threading.Tasks;
using Dignite.Site.Settings;
using Shouldly;
using Xunit;

namespace Dignite.Site.Seo;

/// <summary>
/// Reading the primary domain (总体设计 §4.2). This is the <i>forward</i> half of the domain question -
/// "what is this tenant's address" - which is an ordinary setting; the reverse lookup that GitHub issue
/// #12 scoped out of this repository is not involved here.
/// </summary>
public class SettingSiteBaseUrlResolver_Tests : SiteDomainTestBase<SiteDomainTestModule>
{
    private readonly ISiteBaseUrlResolver _resolver;
    private readonly TestSettingValueProvider _settings;

    public SettingSiteBaseUrlResolver_Tests()
    {
        _resolver = GetRequiredService<ISiteBaseUrlResolver>();
        _settings = GetRequiredService<TestSettingValueProvider>();
    }

    [Fact]
    public async Task Should_Be_Null_When_Unconfigured()
    {
        // Null rather than a guess: the web layer's fallback is what turns this into the request's host,
        // and nothing down here has a request to look at.
        (await _resolver.GetBaseUrlAsync()).ShouldBeNull();
    }

    [Theory]
    [InlineData("https://acme.example", "https://acme.example")]
    [InlineData("https://acme.example/", "https://acme.example")]
    [InlineData("  https://acme.example/  ", "https://acme.example")]
    [InlineData("http://acme.example:8080", "http://acme.example:8080")]
    [InlineData("https://acme.example/site/", "https://acme.example/site")]
    public async Task Should_Normalize_A_Configured_Value(string configured, string expected)
    {
        _settings.Set(SiteSettings.PrimaryDomain, configured);

        (await _resolver.GetBaseUrlAsync()).ShouldBe(expected);
    }

    /// <summary>
    /// A query or fragment is not part of a site root, and carrying one through would append it to the
    /// middle of every generated URL.
    /// </summary>
    [Fact]
    public async Task Should_Drop_A_Query_String()
    {
        _settings.Set(SiteSettings.PrimaryDomain, "https://acme.example/site?utm=1");

        (await _resolver.GetBaseUrlAsync()).ShouldBe("https://acme.example/site");
    }

    /// <summary>
    /// A malformed setting is ignored rather than thrown on: one bad value must not take the whole site's
    /// robots.txt down, and it is logged instead.
    /// </summary>
    [Theory]
    [InlineData("acme.example")]
    [InlineData("/acme")]
    [InlineData("ftp://acme.example")]
    [InlineData("not a url at all")]
    public async Task Should_Ignore_A_Value_That_Is_Not_An_Absolute_Http_Url(string configured)
    {
        _settings.Set(SiteSettings.PrimaryDomain, configured);

        (await _resolver.GetBaseUrlAsync()).ShouldBeNull();
    }
}

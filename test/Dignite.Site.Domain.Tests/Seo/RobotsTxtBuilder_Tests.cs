using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Settings;
using Shouldly;
using Xunit;

namespace Dignite.Site.Seo;

/// <summary>
/// The robots.txt the AI crawler switches produce (总体设计 §5.6, GitHub issue #18).
/// </summary>
public class RobotsTxtBuilder_Tests : SiteDomainTestBase<SiteDomainTestModule>
{
    private readonly RobotsTxtBuilder _builder;
    private readonly TestSettingValueProvider _settings;

    public RobotsTxtBuilder_Tests()
    {
        _builder = GetRequiredService<RobotsTxtBuilder>();
        _settings = GetRequiredService<TestSettingValueProvider>();
        _settings.Set(SiteSettings.PrimaryDomain, "https://acme.example");
    }

    /// <summary>
    /// "Allow search, block training" is the policy site owners usually want, so it is what the platform
    /// defaults already produce - the common choice needs no configuration at all.
    /// </summary>
    [Fact]
    public async Task Platform_Defaults_Should_Allow_Search_And_Block_Training()
    {
        var robots = await _builder.BuildAsync();

        GroupOf(robots, "*").ShouldBe("Allow: /");

        foreach (var trainingBot in AiCrawlers.Training)
        {
            GroupOf(robots, trainingBot).ShouldBe("Disallow: /", $"'{trainingBot}' should be blocked by default");
        }

        foreach (var searchBot in AiCrawlers.Search)
        {
            GroupOf(robots, searchBot).ShouldBe("Allow: /", $"'{searchBot}' should be allowed by default");
        }
    }

    [Theory]
    [InlineData(true, "Allow: /")]
    [InlineData(false, "Disallow: /")]
    public async Task Training_Switch_Should_Control_Only_The_Training_Bots(bool allowTraining, string expected)
    {
        _settings.Set(SiteSettings.Robots.AllowAiTraining, allowTraining.ToString().ToLowerInvariant());

        var robots = await _builder.BuildAsync();

        foreach (var trainingBot in AiCrawlers.Training)
        {
            GroupOf(robots, trainingBot).ShouldBe(expected);
        }

        // The two classes are independent - that independence is the whole reason there are two switches.
        foreach (var searchBot in AiCrawlers.Search)
        {
            GroupOf(robots, searchBot).ShouldBe("Allow: /");
        }
    }

    [Theory]
    [InlineData(true, "Allow: /")]
    [InlineData(false, "Disallow: /")]
    public async Task Search_Switch_Should_Control_Only_The_Search_Bots(bool allowSearch, string expected)
    {
        _settings.Set(SiteSettings.Robots.AllowAiSearch, allowSearch.ToString().ToLowerInvariant());

        var robots = await _builder.BuildAsync();

        foreach (var searchBot in AiCrawlers.Search)
        {
            GroupOf(robots, searchBot).ShouldBe(expected);
        }

        foreach (var trainingBot in AiCrawlers.Training)
        {
            GroupOf(robots, trainingBot).ShouldBe("Disallow: /");
        }
    }

    /// <summary>
    /// <b>The group-precedence trap.</b> A crawler obeys only the most specific group that matches it and
    /// ignores <c>User-agent: *</c> entirely - so leaving a named group saying <c>Allow: /</c> while the
    /// site is closed would let that one crawler keep running over a site the owner believes is sealed.
    /// Closing the site has to close every group with it.
    /// </summary>
    [Fact]
    public async Task Closing_The_Site_Should_Override_Every_Named_Group()
    {
        _settings.Set(SiteSettings.Robots.AllowIndexing, "false");
        _settings.Set(SiteSettings.Robots.AllowAiTraining, "true");
        _settings.Set(SiteSettings.Robots.AllowAiSearch, "true");

        var robots = await _builder.BuildAsync();

        GroupOf(robots, "*").ShouldBe("Disallow: /");

        foreach (var userAgent in AiCrawlers.Training.Concat(AiCrawlers.Search))
        {
            GroupOf(robots, userAgent).ShouldBe("Disallow: /", $"'{userAgent}' must not out-rank the site switch");
        }
    }

    [Fact]
    public async Task Should_Advertise_The_Tenants_Sitemap_Index()
    {
        (await _builder.BuildAsync()).ShouldContain("Sitemap: https://acme.example/sitemap.xml");
    }

    /// <summary>Pointing a closed site's crawlers at a sitemap would be an invitation it just withdrew.</summary>
    [Fact]
    public async Task Should_Not_Advertise_A_Sitemap_When_Indexing_Is_Off()
    {
        _settings.Set(SiteSettings.Robots.AllowIndexing, "false");

        (await _builder.BuildAsync()).ShouldNotContain("Sitemap:");
    }

    /// <summary>
    /// A repeated <c>User-agent</c> token would merge two groups into one and make the effective rule
    /// depend on ordering.
    /// </summary>
    [Fact]
    public async Task Every_User_Agent_Should_Appear_Exactly_Once()
    {
        var robots = await _builder.BuildAsync();

        foreach (var userAgent in AiCrawlers.Training.Concat(AiCrawlers.Search).Append("*"))
        {
            CountOf(robots, $"User-agent: {userAgent}").ShouldBe(1, $"'{userAgent}' should head exactly one group");
        }
    }

    /// <summary>The rule line that follows a <c>User-agent</c> line.</summary>
    private static string GroupOf(string robots, string userAgent)
    {
        var lines = robots.Split('\n').Select(l => l.Trim()).ToList();
        var index = lines.IndexOf($"User-agent: {userAgent}");

        index.ShouldBeGreaterThanOrEqualTo(0, $"'{userAgent}' should have a group");

        return lines[index + 1];
    }

    private static int CountOf(string robots, string line)
    {
        return robots.Split('\n').Count(l => string.Equals(l.Trim(), line, StringComparison.Ordinal));
    }
}

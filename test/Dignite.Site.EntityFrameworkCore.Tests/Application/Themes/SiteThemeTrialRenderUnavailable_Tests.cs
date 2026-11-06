using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Themes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// An administration API deployed apart from the site: no <see cref="ISiteThemePreviewRenderer"/>, so the trial render
/// renders nothing - and the check has to say so rather than read as a theme whose pages rendered well.
/// </summary>
public class SiteThemeTrialRenderUnavailable_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ISiteThemeAdminAppService _themeAppService;

    public SiteThemeTrialRenderUnavailable_Tests()
    {
        _themeAppService = GetRequiredService<ISiteThemeAdminAppService>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.RemoveAll<ISiteThemePreviewRenderer>();
    }

    [Fact]
    public async Task The_Check_Should_Warn_That_No_Page_Was_Rendered()
    {
        await _themeAppService.SaveFileAsync(new SaveSiteThemeFileDto { Path = "Default", Body = "<p>x</p>" });

        var check = await _themeAppService.CheckAsync();

        check.HasErrors.ShouldBeFalse();
        var issue = check.Issues.ShouldHaveSingleItem();
        issue.Code.ShouldBe(SiteThemeIssueCodes.TrialRenderUnavailable);
        issue.Severity.ShouldBe(SiteThemeIssueSeverity.Warning);
        issue.Path.ShouldBeNull();
        issue.Message.ShouldContain("8");
        issue.Message.ShouldContain("Dignite.Site.Public.Web");

        // A warning: the publish goes ahead, as it did before the warning was there.
        var release = await _themeAppService.PublishAsync(new PublishSiteThemeDto { Fingerprint = check.Fingerprint });
        release.Number.ShouldBe(1);
    }

    /// <summary>A theme that renders no page would not have been rendered by any renderer: nothing to warn about.</summary>
    [Fact]
    public async Task The_Check_Should_Not_Warn_When_The_Theme_Renders_No_Page()
    {
        var check = await _themeAppService.CheckAsync();

        check.Issues.ShouldNotContain(issue => issue.Code == SiteThemeIssueCodes.TrialRenderUnavailable);
    }

    /// <summary>The preview itself still refuses, as before.</summary>
    [Fact]
    public async Task A_Preview_Should_Still_Fail_With_The_Renderer_Missing()
    {
        await _themeAppService.SaveFileAsync(new SaveSiteThemeFileDto { Path = "Default", Body = "<p>x</p>" });

        var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => _themeAppService.PreviewAsync("/about"));

        exception.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewUnavailable);
    }
}

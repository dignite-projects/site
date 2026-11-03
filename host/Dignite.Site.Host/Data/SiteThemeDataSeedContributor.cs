using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Host.Data;

/// <summary>
/// Local-testing convenience: publishes <see cref="SeedTheme"/>, the Scriban theme the demo pages of
/// <see cref="SiteContentDataSeedContributor"/> name as their templates, so a fresh host database renders the
/// demo site through Scriban out of the box.
/// <para>
/// A theme that already has a live release is left alone, whatever it holds: the seed fills a database that
/// never published, it does not replace what someone published. Host-only, for the same reason as the content
/// seed.
/// </para>
/// </summary>
public class SiteThemeDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly SiteThemeManager _themeManager;

    public SiteThemeDataSeedContributor(SiteThemeManager themeManager)
    {
        _themeManager = themeManager;
    }

    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (context.TenantId != null)
        {
            return;
        }

        var theme = await _themeManager.GetOrCreateAsync();
        if (theme.LiveReleaseId != null)
        {
            return;
        }

        foreach (var (path, body) in SeedTheme.Files)
        {
            await _themeManager.SaveFileAsync(theme, path, body);
        }

        // The check reads the pages' templates too, so this is only meaningful after the content seed has made
        // the pages; a missing page is a warning, not an error, and does not stop the publish.
        var check = await _themeManager.CheckAsync(theme);
        var errors = check.Analysis.Issues.Where(issue => issue.Severity == SiteThemeIssueSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The seed theme does not pass its own check: " +
                string.Join("; ", errors.Select(e => $"{e.Code} {e.Path}:{e.Line}:{e.Column}")));
        }

        await _themeManager.PublishAsync(theme, "Demo theme", check.Fingerprint);
    }
}

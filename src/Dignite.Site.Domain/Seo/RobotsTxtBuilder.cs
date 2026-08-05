using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Settings;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;

namespace Dignite.Site.Seo;

/// <summary>
/// Builds one tenant's <c>robots.txt</c> (总体设计 §5.6, GitHub issue #18).
/// <para>
/// Per domain by construction rather than by filtering: the document is composed from the current
/// tenant's settings and the current tenant's sitemap URL, so one tenant's rules cannot appear on
/// another's domain - there is no shared document to leak out of.
/// </para>
/// <para>
/// <b>The AI crawler switches are the point.</b> They are first-class settings rather than free-text
/// robots editing because the two classes of crawler want opposite answers: owners commonly want to be
/// cited in AI answers while not being trained on. The platform defaults (indexing on, training off,
/// search on) already <i>are</i> that policy, so the common choice is the one that needs no configuration
/// at all.
/// </para>
/// </summary>
public class RobotsTxtBuilder : DomainService
{
    protected ISettingProvider SettingProvider { get; }

    protected SiteUrlBuilder UrlBuilder { get; }

    public RobotsTxtBuilder(ISettingProvider settingProvider, SiteUrlBuilder urlBuilder)
    {
        SettingProvider = settingProvider;
        UrlBuilder = urlBuilder;
    }

    public virtual async Task<string> BuildAsync(CancellationToken cancellationToken = default)
    {
        var allowIndexing = await SettingProvider.IsTrueAsync(SiteSettings.Robots.AllowIndexing);
        var allowAiTraining = await SettingProvider.IsTrueAsync(SiteSettings.Robots.AllowAiTraining);
        var allowAiSearch = await SettingProvider.IsTrueAsync(SiteSettings.Robots.AllowAiSearch);

        var builder = new StringBuilder();

        AppendGroup(builder, "*", allowIndexing);

        builder.AppendLine();
        builder.AppendLine("# AI training crawlers");
        foreach (var userAgent in AiCrawlers.Training)
        {
            // Each token gets its own group. Google-Extended in particular is a Gemini/Vertex training
            // opt-out token rather than a crawler, so it has to be denied without Googlebot ever seeing a
            // rule - folding these into the "*" group would do exactly the opposite.
            AppendGroup(builder, userAgent, allowIndexing && allowAiTraining);
        }

        builder.AppendLine();
        builder.AppendLine("# AI search and RAG crawlers");
        foreach (var userAgent in AiCrawlers.Search)
        {
            AppendGroup(builder, userAgent, allowIndexing && allowAiSearch);
        }

        if (allowIndexing)
        {
            var context = await UrlBuilder.CreateContextAsync(cancellationToken);

            builder.AppendLine();
            builder.AppendLine($"Sitemap: {UrlBuilder.BuildSitemapIndexUrl(context)}");
        }

        return builder.ToString();
    }

    /// <summary>
    /// One <c>User-agent</c> group.
    /// <para>
    /// <b>Group precedence is why <paramref name="allow"/> is pre-combined with the site-wide switch by
    /// every caller above.</b> A crawler obeys only the single most specific group that matches it and
    /// ignores <c>User-agent: *</c> entirely - so a named group left saying <c>Allow: /</c> while the site
    /// is closed would keep that one crawler running over a site the owner believes is sealed. Turning
    /// indexing off has to turn every group off with it.
    /// </para>
    /// </summary>
    protected virtual void AppendGroup(StringBuilder builder, string userAgent, bool allow)
    {
        builder.AppendLine($"User-agent: {userAgent}");
        builder.AppendLine(allow ? "Allow: /" : "Disallow: /");
    }
}

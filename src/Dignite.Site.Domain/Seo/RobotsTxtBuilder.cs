using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement;
using Dignite.Site.Settings;
using Dignite.Site.Themes;
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
/// <para>
/// <b>A site closed to indexing is kept out by <c>noindex</c>, not by this file.</b> Its pages carry
/// <c>noindex</c> (<see cref="HeadMetadataBuilder"/>), and a search engine reads that only on a page it may
/// fetch, so the engines that obey it are let in (<see cref="SearchCrawlers.ReadingNoIndex"/>); every other
/// crawler is still disallowed everything.
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

        if (!allowIndexing)
        {
            builder.AppendLine();
            builder.AppendLine("# Search engines that read noindex");
            foreach (var userAgent in SearchCrawlers.ReadingNoIndex)
            {
                AppendNoIndexReaderGroup(builder, userAgent);
            }
        }

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
    /// indexing off has to turn every group off with it, but for the search engines let in to read
    /// <c>noindex</c> (<see cref="AppendNoIndexReaderGroup"/>).
    /// </para>
    /// </summary>
    protected virtual void AppendGroup(StringBuilder builder, string userAgent, bool allow)
    {
        builder.AppendLine($"User-agent: {userAgent}");
        builder.AppendLine(allow ? "Allow: /" : "Disallow: /");
    }

    /// <summary>
    /// The group of a search engine let into a site closed to indexing so that it reads the pages' <c>noindex</c>
    /// (<see cref="SearchCrawlers"/>): everything but <see cref="GetPathsWithoutNoIndex"/>.
    /// </summary>
    protected virtual void AppendNoIndexReaderGroup(StringBuilder builder, string userAgent)
    {
        builder.AppendLine($"User-agent: {userAgent}");
        builder.AppendLine("Allow: /");
        foreach (var path in GetPathsWithoutNoIndex())
        {
            // The longer path wins over "Allow: /", whatever the order (RFC 9309).
            builder.AppendLine($"Disallow: {path}");
        }
    }

    /// <summary>
    /// What a closed site keeps every crawler out of, because its responses carry no <c>noindex</c> to read: the
    /// theme's static files, which are cached for a year - a <c>noindex</c> header on them would outlive the site's
    /// opening - and the site's files, which Blob Management serves. Disallowing them is what keeps a site's
    /// images, PDFs and other files out of an index while it is closed.
    /// </summary>
    protected virtual IEnumerable<string> GetPathsWithoutNoIndex()
    {
        yield return "/" + SiteThemeConsts.AssetRoutePrefix + "/";
        yield return "/" + BlobManagementPublicUrl.RoutePrefix + "/";
    }
}

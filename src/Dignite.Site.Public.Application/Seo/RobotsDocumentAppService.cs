using System.Threading.Tasks;
using Dignite.Site.Seo;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Serves <c>robots.txt</c> and the optional <c>llms.txt</c> (总体设计 §5.6, GitHub issue #18).
/// <para>
/// Neither document carries a <c>Last-Modified</c>: they are computed from settings rather than from
/// content, so there is no real modification time to report and inventing one would be the same mistake
/// the sitemap's <c>lastmod</c> avoids.
/// </para>
/// </summary>
public class RobotsDocumentAppService : SitePublicAppService, IRobotsDocumentAppService
{
    public const string PlainTextContentType = "text/plain";
    public const string MarkdownContentType = "text/markdown";

    protected RobotsTxtBuilder RobotsTxtBuilder { get; }

    protected LlmsTxtBuilder LlmsTxtBuilder { get; }

    public RobotsDocumentAppService(RobotsTxtBuilder robotsTxtBuilder, LlmsTxtBuilder llmsTxtBuilder)
    {
        RobotsTxtBuilder = robotsTxtBuilder;
        LlmsTxtBuilder = llmsTxtBuilder;
    }

    public virtual async Task<SiteDocument> GetRobotsTxtAsync()
    {
        return new SiteDocument(PlainTextContentType, await RobotsTxtBuilder.BuildAsync());
    }

    public virtual async Task<SiteDocument?> GetLlmsTxtAsync()
    {
        var content = await LlmsTxtBuilder.BuildAsync();

        return content == null ? null : new SiteDocument(MarkdownContentType, content);
    }
}

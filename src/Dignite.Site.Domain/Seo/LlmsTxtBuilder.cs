using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Pages;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Seo;

/// <summary>
/// Builds the <c>/llms.txt</c> index of the site's pages (总体设计 §5.6, GitHub issue #18).
/// <para>
/// <b>Not a ranking mechanism.</b> Adoption sits around 10% and Google has stated it does not support the
/// file. Every site serves it, with no setting to turn it off, because it costs almost nothing once the
/// routing table is in hand and lists only pages the sitemap already makes public - not because it is
/// expected to do anything for search. A site closed to indexing sends it with <c>X-Robots-Tag: noindex</c>
/// like its pages.
/// </para>
/// </summary>
public class LlmsTxtBuilder : DomainService
{
    protected IPageRepository PageRepository { get; }

    protected SiteUrlBuilder UrlBuilder { get; }

    public LlmsTxtBuilder(
        IPageRepository pageRepository,
        SiteUrlBuilder urlBuilder)
    {
        PageRepository = pageRepository;
        UrlBuilder = urlBuilder;
    }

    public virtual async Task<string> BuildAsync(CancellationToken cancellationToken = default)
    {
        var context = await UrlBuilder.CreateContextAsync(cancellationToken);

        var pages = (await PageRepository.GetRoutableListAsync(cancellationToken))
            .OrderBy(p => p.Route, StringComparer.Ordinal)
            .ToList();

        // The page FindHomePageAsync would return, picked from the list already in hand.
        var homePage = pages
            .Where(p => p.IsHomeRoute())
            .OrderBy(p => p.Route, PageRoute.AddressPrecedence)
            .FirstOrDefault();

        var builder = new StringBuilder();
        builder.AppendLine($"# {homePage?.DisplayName ?? CurrentTenant.Name ?? "Site"}");
        builder.AppendLine();
        builder.AppendLine("## Pages");

        foreach (var page in pages)
        {
            builder.AppendLine(
                $"- [{page.DisplayName}]({UrlBuilder.BuildPageUrl(context, page, context.DefaultCultureName)})");
        }

        return builder.ToString();
    }
}

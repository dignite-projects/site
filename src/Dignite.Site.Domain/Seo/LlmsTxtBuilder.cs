using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Pages;
using Dignite.Site.Settings;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;

namespace Dignite.Site.Seo;

/// <summary>
/// Builds the optional <c>/llms.txt</c> index of the site's pages (总体设计 §5.6, GitHub issue #18).
/// <para>
/// <b>Off by default, and not a ranking mechanism.</b> Adoption sits around 10% and Google has stated it
/// does not support the file. It is here because generating it costs almost nothing once the routing table
/// is already in hand - not because it is expected to do anything for search. Presenting it as an SEO
/// lever would be misleading; it is offered as an opt-in convenience for AI clients and nothing more.
/// </para>
/// </summary>
public class LlmsTxtBuilder : DomainService
{
    protected IPageRepository PageRepository { get; }

    protected ISettingProvider SettingProvider { get; }

    protected SiteUrlBuilder UrlBuilder { get; }

    public LlmsTxtBuilder(
        IPageRepository pageRepository,
        ISettingProvider settingProvider,
        SiteUrlBuilder urlBuilder)
    {
        PageRepository = pageRepository;
        SettingProvider = settingProvider;
        UrlBuilder = urlBuilder;
    }

    public virtual async Task<bool> IsEnabledAsync()
    {
        return await SettingProvider.IsTrueAsync(SiteSettings.EnableLlmsTxt);
    }

    /// <summary>
    /// The document, or <see langword="null"/> when the tenant has not opted in - which the endpoint turns
    /// into a 404, the honest answer for a file this site does not publish.
    /// </summary>
    public virtual async Task<string?> BuildAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync())
        {
            return null;
        }

        var context = await UrlBuilder.CreateContextAsync(cancellationToken);

        var pages = (await PageRepository.GetRoutableListAsync(cancellationToken))
            .OrderBy(p => p.Route, StringComparer.Ordinal)
            .ToList();

        var homePage = pages.FirstOrDefault(p => p.IsHomePage);

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

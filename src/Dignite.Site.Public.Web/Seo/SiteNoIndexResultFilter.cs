using System;
using System.Threading.Tasks;
using Dignite.Site.Public.Preview;
using Dignite.Site.Settings;
using Microsoft.AspNetCore.Mvc.Filters;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Sends <c>X-Robots-Tag: noindex</c> with what a site closed to indexing (<c>Site.Robots.AllowIndexing</c> off)
/// serves on the actions that apply it: its rendered pages, its sitemap, its feeds and its <c>llms.txt</c>.
/// <para>
/// The pages already say <c>noindex</c> in their head metadata, but that reaches the page only through a template
/// that writes it (<c>site_head</c>, <c>site-head-metadata</c>); the header does not depend on the template, and it
/// is the only way to say it on a document that is not HTML. It is not sent with <c>robots.txt</c>, the crawl rules
/// themselves, nor with the theme's static files and the site's files, which are cached long enough for the header
/// to outlive the site's opening: <c>robots.txt</c> keeps crawlers out of those instead (<c>RobotsTxtBuilder</c>).
/// </para>
/// <para>
/// Fails open: only a setting that reads <c>false</c> closes the site, so a front end that cannot read the setting
/// never takes an open site out of an index.
/// </para>
/// </summary>
public class SiteNoIndexResultFilter : IAsyncResultFilter, ITransientDependency
{
    public const string HeaderValue = "noindex";

    protected ISettingProvider SettingProvider { get; }

    public SiteNoIndexResultFilter(ISettingProvider settingProvider)
    {
        SettingProvider = settingProvider;
    }

    public virtual async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (await IsClosedToIndexingAsync())
        {
            context.HttpContext.Response.Headers[SitePreviewHttp.RobotsTagHeaderName] = HeaderValue;
        }

        await next();
    }

    protected virtual async Task<bool> IsClosedToIndexingAsync()
    {
        var value = await SettingProvider.GetOrNullAsync(SiteSettings.Robots.AllowIndexing);

        return string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);
    }
}

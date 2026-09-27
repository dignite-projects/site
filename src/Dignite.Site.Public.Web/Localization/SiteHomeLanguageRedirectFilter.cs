using System;
using System.Threading.Tasks;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// Sends a visitor arriving at <c>/</c> - the default language's home page - to their own language's home
/// page (GitHub issue #75). Opt-in: <see cref="SiteLanguageOptions.RedirectHomeToVisitorLanguage"/>.
/// <list type="number">
/// <item>A navigation from within the site itself (<c>Sec-Fetch-Site: same-origin</c>) is never redirected:
/// the visitor followed a link to the default language's home page - a logo on a default-language page, a
/// language switcher - and gets exactly that. Without this, a host that does not write the cookie would send
/// a Japanese browser clicking "English" straight back to <c>/ja</c>.</item>
/// <item>An explicit choice wins next: the <see cref="SiteLanguageOptions.CookieName"/> cookie, written by the
/// host's switcher. Naming the default language, it keeps the visitor on <c>/</c>.</item>
/// <item>Only then does <c>Accept-Language</c> decide (<see cref="SiteLanguageMatcher"/>). The default
/// language, or no match at all, leaves the request alone - which is also what a crawler sending no
/// <c>Accept-Language</c> gets.</item>
/// </list>
/// <para>
/// Only <c>/</c> is touched: every other address, including the prefixed home pages, is a URL someone chose on
/// purpose. 302, not 301 - the target depends on the visitor, so no browser or cache may remember it - and
/// <c>/</c>'s response varies by every header that decided it. A resource filter rather than middleware for
/// the reason <see cref="SiteLanguageContext"/> gives: it runs after tenant resolution by construction.
/// </para>
/// </summary>
public class SiteHomeLanguageRedirectFilter : IAsyncResourceFilter, ITransientDependency
{
    public const string SecFetchSiteHeaderName = "Sec-Fetch-Site";

    protected IOptions<SiteLanguageOptions> Options { get; }

    public SiteHomeLanguageRedirectFilter(IOptions<SiteLanguageOptions> options)
    {
        Options = options;
    }

    public virtual async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        if (!Options.Value.RedirectHomeToVisitorLanguage
            || !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            || !IsHome(request.Path))
        {
            await next();
            return;
        }

        var languages = await context.HttpContext.RequestServices
            .GetRequiredService<SiteLanguageContext>()
            .GetLanguagesAsync();

        if (languages.EnabledCultureNames.Count > 1)
        {
            // Redirected or not, what "/" returns depends on all three.
            context.HttpContext.Response.Headers.Append(
                HeaderNames.Vary, $"{HeaderNames.AcceptLanguage}, {HeaderNames.Cookie}, {SecFetchSiteHeaderName}");

            var target = ResolveTargetCultureName(request, languages);
            if (target != null && target != languages.DefaultCultureName)
            {
                context.Result = new RedirectResult(
                    request.PathBase + languages.ApplyCulturePrefix("/", target) + request.QueryString,
                    permanent: false);
                return;
            }
        }

        await next();
    }

    /// <summary>The language <c>/</c> should be in for this visitor; null to leave it alone.</summary>
    protected virtual string? ResolveTargetCultureName(HttpRequest request, SiteLanguages languages)
    {
        if (string.Equals(request.Headers[SecFetchSiteHeaderName], "same-origin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (request.Cookies.TryGetValue(Options.Value.CookieName, out var chosen)
            && SiteLanguageMatcher.FindServed(languages, chosen) is { } chosenCultureName)
        {
            return chosenCultureName;
        }

        return SiteLanguageMatcher.Match(languages, request.GetTypedHeaders().AcceptLanguage);
    }

    private static bool IsHome(PathString path)
    {
        return !path.HasValue || path == "/";
    }
}

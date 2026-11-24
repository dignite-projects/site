using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace Dignite.Site.Seo;

/// <summary>
/// Resolves the site's base URL from the <see cref="SiteSettings.PrimaryDomain"/> tenant setting, and from
/// the request being served (<see cref="ISiteRequestBaseUrlAccessor"/>) when the setting is blank.
/// <para>
/// <b>The setting always wins when it is set</b>, and that is the point rather than a detail. Serving a
/// custom domain and a platform subdomain both at 200 with self-referencing URLs is the classic
/// multi-tenant SEO failure - the same ranking signals split across two addresses (总体设计 §5). The request
/// fallback exists only for the unconfigured case, where guessing beats failing.
/// </para>
/// </summary>
public class SettingSiteBaseUrlResolver : ISiteBaseUrlResolver, ITransientDependency
{
    protected ISettingProvider SettingProvider { get; }

    protected ISiteRequestBaseUrlAccessor RequestBaseUrlAccessor { get; }

    public ILogger<SettingSiteBaseUrlResolver> Logger { get; set; }

    public SettingSiteBaseUrlResolver(ISettingProvider settingProvider, ISiteRequestBaseUrlAccessor requestBaseUrlAccessor)
    {
        SettingProvider = settingProvider;
        RequestBaseUrlAccessor = requestBaseUrlAccessor;
        Logger = NullLogger<SettingSiteBaseUrlResolver>.Instance;
    }

    public virtual async Task<string?> GetBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        var configured = await SettingProvider.GetOrNullAsync(SiteSettings.PrimaryDomain);

        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (TryNormalize(configured, out var normalized))
            {
                return normalized;
            }

            // Not an exception: a malformed setting must not take the whole site's robots.txt down. The
            // request fallback still produces a usable URL, and the anomaly surfaces here instead.
            Logger.LogWarning(
                "The '{SettingName}' setting is '{Value}', which is not an absolute http(s) URL; ignoring it.",
                SiteSettings.PrimaryDomain, configured);
        }

        return TryNormalize(RequestBaseUrlAccessor.GetBaseUrl(), out var requested) ? requested : null;
    }

    /// <summary>
    /// Accepts an absolute http/https URL, keeps any base path, and strips the trailing slash so callers
    /// can concatenate a leading-slash path without doubling it.
    /// </summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        // GetLeftPart(Path) keeps a base path and drops any query or fragment, neither of which belongs
        // in a site root.
        normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');

        return normalized.Length > 0;
    }
}

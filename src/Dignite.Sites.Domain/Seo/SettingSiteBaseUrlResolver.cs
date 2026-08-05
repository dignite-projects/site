using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Sites.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace Dignite.Sites.Seo;

/// <summary>
/// Resolves the site's base URL from the <see cref="SitesSettings.PrimaryDomain"/> tenant setting.
/// <para>
/// The web layer replaces this with a subclass that falls back to the incoming request, so this class is
/// the "configured answer" half only: it returns <see langword="null"/> rather than guessing when the
/// setting is blank.
/// </para>
/// </summary>
public class SettingSiteBaseUrlResolver : ISiteBaseUrlResolver, ITransientDependency
{
    protected ISettingProvider SettingProvider { get; }

    public ILogger<SettingSiteBaseUrlResolver> Logger { get; set; }

    public SettingSiteBaseUrlResolver(ISettingProvider settingProvider)
    {
        SettingProvider = settingProvider;
        Logger = NullLogger<SettingSiteBaseUrlResolver>.Instance;
    }

    public virtual async Task<string?> GetBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        return await GetConfiguredBaseUrlAsync(cancellationToken);
    }

    /// <summary>
    /// The configured value, normalized, or <see langword="null"/> if unset or unusable. Kept separate
    /// from <see cref="GetBaseUrlAsync"/> so an overriding resolver can ask "is one configured?" without
    /// re-triggering its own fallback.
    /// </summary>
    protected virtual async Task<string?> GetConfiguredBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        var configured = await SettingProvider.GetOrNullAsync(SitesSettings.PrimaryDomain);

        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        if (!TryNormalize(configured, out var normalized))
        {
            // Not an exception: a malformed setting must not take the whole site's robots.txt down. The
            // web layer's fallback still produces a usable URL, and the anomaly surfaces here instead.
            Logger.LogWarning(
                "The '{SettingName}' setting is '{Value}', which is not an absolute http(s) URL; ignoring it.",
                SitesSettings.PrimaryDomain, configured);
            return null;
        }

        return normalized;
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

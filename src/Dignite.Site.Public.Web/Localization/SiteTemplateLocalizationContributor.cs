using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// Supplies <see cref="SiteTemplateResource"/>'s texts from the JSON files next to the current site's
/// templates (GitHub issue #73), following the same tenant rule as <see cref="TenantViewLocationExpander"/>:
/// <list type="bullet">
/// <item>a tenant reads only <c>/Sites/{tenantName}/Localization/</c> - never the host's files, the same way
/// a tenant request never tries the host's <c>/Sites/</c> views;</item>
/// <item>a request without a tenant reads <c>/Sites/Localization/</c>.</item>
/// </list>
/// Within that one folder a text is looked up in the requested culture, then every parent culture
/// (<c>zh-Hant-TW</c>, <c>zh-Hant</c>, <c>zh</c>), then the site's default language
/// (<see cref="SiteTemplateDefaultCulture"/>). Past that ABP reports it not found and shows the key - or a
/// template passes its own fallback through <c>GetOrDefault</c>.
/// <para>
/// The whole order lives here rather than in ABP's own fallback, which climbs one parent level at most and
/// has no notion of a per-site default. ABP caches one localizer per resource and asks its contributors on
/// every lookup, so reading the current tenant here, per call, is what makes one resource serve every
/// tenant; the texts themselves are cached per tenant folder, per culture.
/// </para>
/// </summary>
public class SiteTemplateLocalizationContributor : ILocalizationResourceContributor
{
    public const string LocalizationFolderName = "Localization";

    private readonly ConcurrentDictionary<string, SiteTemplateTextDirectory> _directories = new(StringComparer.Ordinal);

    private LocalizationResourceInitializationContext _context = default!;
    private ICurrentTenant _currentTenant = default!;
    private ILogger _logger = NullLogger.Instance;

    /// <summary>True: what a lookup returns depends on the tenant of the request, not only on the files.</summary>
    public bool IsDynamic => true;

    public virtual void Initialize(LocalizationResourceInitializationContext context)
    {
        _context = context;
        _currentTenant = context.ServiceProvider.GetRequiredService<ICurrentTenant>();
        _logger = context.ServiceProvider.GetService<ILogger<SiteTemplateLocalizationContributor>>()
                  ?? (ILogger)NullLogger.Instance;
    }

    public virtual LocalizedString? GetOrNull(string cultureName, string name)
    {
        var directory = GetCurrentDirectory();
        foreach (var culture in GetCultureChain(cultureName))
        {
            var text = directory.GetOrNull(culture, name);
            if (text != null)
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>Every text a lookup in <paramref name="cultureName"/> would find - the same cultures, highest priority winning.</summary>
    public virtual void Fill(string cultureName, Dictionary<string, LocalizedString> dictionary)
    {
        var directory = GetCurrentDirectory();
        foreach (var culture in GetCultureChain(cultureName).Reverse())
        {
            directory.Fill(culture, dictionary);
        }
    }

    public virtual Task FillAsync(string cultureName, Dictionary<string, LocalizedString> dictionary)
    {
        Fill(cultureName, dictionary);
        return Task.CompletedTask;
    }

    public virtual Task<IEnumerable<string>> GetSupportedCulturesAsync()
    {
        return GetCurrentDirectory().GetSupportedCulturesAsync();
    }

    /// <summary>The folder a tenant named <paramref name="tenantName"/> - or, when null, the host - reads its texts from.</summary>
    public static string GetVirtualPath(string? tenantName)
    {
        return string.IsNullOrEmpty(tenantName)
            ? $"{TenantViewLocationExpander.SitesFolder}/{LocalizationFolderName}"
            : $"{TenantViewLocationExpander.SitesFolder}/{tenantName}/{LocalizationFolderName}";
    }

    protected virtual SiteTemplateTextDirectory GetCurrentDirectory()
    {
        // Name, not Id, and only when there is one - exactly TenantViewLocationExpander's own condition, so
        // the texts are always read from beside the templates actually being rendered.
        var tenantName = _currentTenant.IsAvailable ? _currentTenant.Name : null;
        var virtualPath = GetVirtualPath(tenantName);

        return _directories.GetOrAdd(virtualPath, path =>
        {
            var directory = new SiteTemplateTextDirectory(path, _logger);
            directory.Initialize(_context);
            return directory;
        });
    }

    /// <summary>
    /// <paramref name="cultureName"/> and all its parents, then the site's default language and its
    /// parents, without repeats: <c>zh-Hant-TW</c> with default <c>en-GB</c> gives
    /// <c>zh-Hant-TW, zh-Hant, zh, en-GB, en</c>.
    /// </summary>
    protected virtual IReadOnlyList<string> GetCultureChain(string cultureName)
    {
        var chain = new List<string>();
        AddWithParents(chain, cultureName);

        var defaultCultureName = SiteTemplateDefaultCulture.Current;
        if (!string.IsNullOrWhiteSpace(defaultCultureName))
        {
            AddWithParents(chain, defaultCultureName);
        }

        return chain;
    }

    private static void AddWithParents(List<string> chain, string cultureName)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            if (!chain.Contains(cultureName))
            {
                chain.Add(cultureName);
            }

            return;
        }

        // The invariant culture - the end of every Parent chain - has an empty name and no texts of its own.
        for (; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
        {
            if (!chain.Contains(culture.Name))
            {
                chain.Add(culture.Name);
            }
        }
    }
}

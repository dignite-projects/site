using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Localization;
using Volo.Abp.Localization.VirtualFiles.Json;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// The template texts of one scope - the host's <c>/Sites/Localization/</c>, or one tenant's
/// <c>/Sites/{tenantName}/Localization/</c> - read through ABP's virtual file system, so embedded files,
/// physical ones and dynamic ones all work. ABP's own JSON contributor does the parsing, the per-culture
/// dictionaries and the reloading (a change the file provider reports drops the cached texts); this only
/// changes how a bad file is handled.
/// <para>
/// Each file is ABP's localization format, <c>{"culture": "ja", "texts": {...}}</c>: the culture is read
/// from the content, never from the file name. Name files <c>{culture}.json</c> anyway - with exactly one
/// dot, because a host that embeds them without an embedded-files manifest has its resource names split on
/// every dot to rebuild the path.
/// </para>
/// </summary>
public class SiteTemplateTextDirectory : JsonVirtualFileLocalizationResourceContributor
{
    public string VirtualPath { get; }

    protected ILogger Logger { get; }

    public SiteTemplateTextDirectory(string virtualPath, ILogger logger)
        : base(virtualPath)
    {
        VirtualPath = virtualPath;
        Logger = logger;
    }

    /// <summary>
    /// A file that does not parse, or names no culture, is logged and skipped rather than thrown: ABP's
    /// own handling throws on every lookup, which here would fail every page of the site whose template
    /// folder holds the file. The page shows its keys or its template's fallbacks for those texts instead.
    /// </summary>
    protected override ILocalizationDictionary? CreateDictionaryFromFile(IFileInfo file)
    {
        ILocalizationDictionary? dictionary;
        try
        {
            dictionary = base.CreateDictionaryFromFile(file);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Skipped template text file {File} in {Directory}: it is not a valid localization file.", file.Name, VirtualPath);
            return null;
        }

        if (dictionary == null)
        {
            Logger.LogWarning("Skipped template text file {File} in {Directory}: it has no \"culture\".", file.Name, VirtualPath);
            return null;
        }

        return NormalizeCultureName(dictionary);
    }

    /// <summary>
    /// Lookups ask for canonical culture names (<c>zh-Hans</c>) and ABP keys the dictionaries by the name as
    /// written, case-sensitively - so a file saying <c>"zh-hans"</c> would otherwise never be found.
    /// </summary>
    protected virtual ILocalizationDictionary NormalizeCultureName(ILocalizationDictionary dictionary)
    {
        string canonicalName;
        try
        {
            canonicalName = CultureInfo.GetCultureInfo(dictionary.CultureName).Name;
        }
        catch (CultureNotFoundException)
        {
            return dictionary;
        }

        if (canonicalName == dictionary.CultureName)
        {
            return dictionary;
        }

        var texts = new Dictionary<string, LocalizedString>();
        dictionary.Fill(texts);
        return new StaticLocalizationDictionary(canonicalName, texts);
    }
}

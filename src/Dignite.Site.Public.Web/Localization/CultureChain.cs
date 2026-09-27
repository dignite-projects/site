using System.Collections.Generic;
using System.Globalization;

namespace Dignite.Site.Public.Localization;

internal static class CultureChain
{
    /// <summary>
    /// <paramref name="culture"/> and its parents, most specific first, without the invariant culture - the
    /// end of every <see cref="CultureInfo.Parent"/> chain, which has an empty name and no texts or URLs of
    /// its own: <c>zh-Hant-TW, zh-Hant, zh</c>.
    /// </summary>
    public static IEnumerable<CultureInfo> Of(CultureInfo culture)
    {
        for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
        {
            yield return current;
        }
    }
}

using System;
using System.Globalization;

namespace Dignite.Sites;

/// <summary>
/// Normalizes a language tag to the canonical <see cref="CultureInfo.Name"/> form every write path must
/// store (总体设计 §2.4).
/// <para>
/// This is not cosmetic. The same string is simultaneously a URL prefix, an <c>hreflang</c> attribute
/// value, part of the <c>(TenantId, PageId, CultureName, Slug)</c> unique constraint, and part of the
/// natural key that groups a content's language versions together. Those four tolerate different
/// spellings. Let <c>zh-CN</c> and <c>zh-cn</c> both reach the table and the unique index will not stop
/// them (they compare equal under most collations only by luck), the translation group splits down the
/// middle, and <c>hreflang</c> emits two sets of links that do not reference each other.
/// </para>
/// <para>
/// So every entry point - MCP tools, the admin API, seeding - runs its culture through here before the
/// value goes anywhere near the database.
/// </para>
/// </summary>
public static class CultureNameNormalizer
{
    /// <summary>
    /// Returns the canonical <see cref="CultureInfo.Name"/> for <paramref name="cultureName"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="cultureName"/> is null, empty, or not a culture .NET actually ships. Deliberately
    /// strict: a culture that cannot be resolved is a caller bug, and storing it anyway is exactly the
    /// split-group failure this class exists to prevent.
    /// </exception>
    public static string Normalize(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            throw new ArgumentException("Culture name must not be null or empty.", nameof(cultureName));
        }

        try
        {
            return GetPredefinedCulture(cultureName).Name;
        }
        catch (CultureNotFoundException e)
        {
            throw new ArgumentException(
                $"'{cultureName}' is not a culture name .NET recognizes. Store BCP 47 tags in " +
                $"CultureInfo.Name form, e.g. 'en', 'zh-Hans', 'en-GB'.",
                nameof(cultureName),
                e);
        }
    }

    /// <summary>
    /// Resolves a culture, rejecting anything .NET does not actually ship.
    /// <para>
    /// <c>predefinedOnly: true</c> is load-bearing and must not be dropped. Without it, .NET does not
    /// reject an unrecognized tag - it manufactures a custom culture from whatever prefix of the string
    /// parses, so <c>"not-a-culture-at-all"</c> comes back as the perfectly plausible <c>"not"</c>, and
    /// <c>"xx-YY"</c> comes back as <c>"xx-YY"</c>. Either would then be stored, indexed and served as a
    /// real language: a typo in one MCP call would silently create a phantom translation that splits the
    /// content's language group and emits an hreflang pointing at a language nothing else uses.
    /// </para>
    /// </summary>
    private static CultureInfo GetPredefinedCulture(string cultureName)
    {
        // GetCultureInfo (not the CultureInfo constructor) so the cached, canonical instance is
        // returned; .Name is then the normalized tag regardless of the casing that came in.
        return CultureInfo.GetCultureInfo(cultureName.Trim(), predefinedOnly: true);
    }

    /// <summary>
    /// Non-throwing counterpart, for validating user input before committing to it.
    /// </summary>
    public static bool TryNormalize(string? cultureName, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return false;
        }

        try
        {
            normalized = GetPredefinedCulture(cultureName).Name;
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}

using System.Collections.Generic;
using System.Text;

namespace Dignite.Site.Seo;

/// <summary>
/// How wide a title or a description is in a search result. A result is limited by width, not by characters, and a
/// Chinese, Japanese or Korean character is about twice as wide as a Latin one: so a width is counted in columns,
/// two for such a character and one for any other. The SEO field's limits (<c>Seo.MetaTitleCharLimit</c>,
/// <c>Seo.MetaDescriptionCharLimit</c>) are in columns; the admin UI counts the same way.
/// </summary>
public static class SeoTextWidth
{
    /// <summary>The name of the platform's SEO field - <c>SeoFieldNames.FieldName</c> of <c>Dignite.FlexFields.Site</c>, which this package does not reference.</summary>
    public const string SeoFieldName = "seo";

    /// <summary>The configuration key of the title's limit, as the SEO field type stores it.</summary>
    public const string MetaTitleLimitKey = "Seo.MetaTitleCharLimit";

    /// <summary>The configuration key of the description's limit, as the SEO field type stores it.</summary>
    public const string MetaDescriptionLimitKey = "Seo.MetaDescriptionCharLimit";

    /// <summary>The title's limit when the field sets none.</summary>
    public const int DefaultMetaTitleLimit = 60;

    /// <summary>The description's limit when the field sets none.</summary>
    public const int DefaultMetaDescriptionLimit = 160;

    /// <summary>The columns <paramref name="text"/> takes; 0 for none.</summary>
    public static int Columns(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var columns = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            columns += IsWide(rune.Value) ? 2 : 1;
        }

        return columns;
    }

    /// <summary>
    /// What is too wide in a title and a description, as sentences for an author or a client; empty when nothing is.
    /// Only advice: a search engine cuts an over-long text, it does not refuse it, so nothing here stops a save.
    /// </summary>
    public static IReadOnlyList<string> Check(string? metaTitle, string? metaDescription, int titleLimit, int descriptionLimit)
    {
        var warnings = new List<string>();

        var titleColumns = Columns(metaTitle);
        if (titleColumns > titleLimit)
        {
            warnings.Add(
                $"The SEO title is {titleColumns} columns wide, over the {titleLimit} a search result shows; " +
                "a Chinese, Japanese or Korean character counts as two.");
        }

        var descriptionColumns = Columns(metaDescription);
        if (descriptionColumns > descriptionLimit)
        {
            warnings.Add(
                $"The SEO description is {descriptionColumns} columns wide, over the {descriptionLimit} a search result shows; " +
                "a Chinese, Japanese or Korean character counts as two.");
        }

        return warnings;
    }

    /// <summary>
    /// The East Asian wide and fullwidth blocks: Hangul Jamo, CJK radicals and punctuation, kana, CJK ideographs and
    /// their extensions, Yi, Hangul syllables, compatibility ideographs and forms, and the fullwidth forms.
    /// </summary>
    private static bool IsWide(int c)
    {
        return c is >= 0x1100 and <= 0x115F
            or >= 0x2E80 and <= 0x303E
            or >= 0x3041 and <= 0x33FF
            or >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF
            or >= 0xA000 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE30 and <= 0xFE4F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x20000 and <= 0x2FFFD
            or >= 0x30000 and <= 0x3FFFD;
    }
}

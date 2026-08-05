using System.Text;
using System.Text.RegularExpressions;
using Slugify;

namespace Dignite.Site;

/// <summary>
/// Normalizes a string into the URL-safe <c>Slug</c> a content is routed by.
/// <para>
/// <b>Script-neutral, and no transliteration.</b> Unicode letters and digits are kept as they are, so
/// <c>我的旅行</c>, <c>Мой отпуск</c>, <c>日本語のタイトル</c> and <c>Ελληνικά</c> all produce usable
/// slugs. Percent-encoded UTF-8 paths have been valid, indexable URLs for years - Wikipedia has run on
/// them for two decades - so there is nothing to gain by forcing them into ASCII.
/// </para>
/// <para>
/// Transliteration is deliberately <b>not</b> built in, and that is a platform decision rather than an
/// omission. Any transliteration table is per-language policy: pinyin for Chinese, a romanization scheme
/// for Cyrillic or Japanese, and so on. Baking one language's in would privilege it over every other one
/// this platform serves, while the others kept degrading to nothing. Dignite.Cms reached for
/// <c>Unidecode.NET</c> here; Site does not, both for that reason and for the licence one in 总体设计
/// §8.4. A deployment that wants romanized slugs transliterates the title itself and passes the result
/// in - which is the normal path anyway, since the slug usually arrives from the caller (an MCP client,
/// an editor) rather than being derived from a title here.
/// </para>
/// </summary>
public static class SlugNormalizer
{
    /// <summary>
    /// Keeps Unicode letters (<c>\p{L}</c>), decimal digits (<c>\p{Nd}</c>), hyphen and underscore;
    /// everything else becomes a separator or is dropped.
    /// <para>
    /// This replaces Slugify's default deny-list, which is <c>[^a-zA-Z0-9\-._]</c> - ASCII-only, and
    /// therefore the reason its out-of-the-box output for any non-Latin title is an empty string.
    /// </para>
    /// </summary>
    private static readonly SlugHelper SlugHelper = new(new SlugHelperConfiguration
    {
        DeniedCharactersRegex = new Regex(@"[^\p{L}\p{Nd}\-_]", RegexOptions.Compiled)
    });

    /// <summary>
    /// Normalizes <paramref name="value"/> to a slug, or returns an empty string when nothing usable is
    /// left (empty input, or input made entirely of punctuation).
    /// <para>
    /// Note that an empty slug is <i>meaningful</i> here - it is the slug of the single content of a home
    /// or "about" page, whose URL is the page route itself. So a caller turning a title into a slug should
    /// use <see cref="TryNormalize"/> instead, which distinguishes "normalized to nothing" from "the
    /// caller meant empty"; storing an accidental empty slug would make the content answer the page's own
    /// URL.
    /// </para>
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Recomposed to NFC, which is not cosmetic. Slugify decomposes to NFD internally to strip Latin
        // diacritics, and for scripts whose decomposition yields *letters* rather than combining marks -
        // Hangul, most visibly, where 한 decomposes into three Jamo - nothing puts them back. The result
        // renders identically to what the author typed while being a different sequence of code points,
        // so the stored slug would never match the NFC form a browser sends, and the unique constraint
        // would happily accept both as two separate contents.
        return SlugHelper.GenerateSlug(value).Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Normalizes <paramref name="value"/>, reporting whether anything survived. Returns <c>false</c> for
    /// null, blank, or input that normalizes away entirely (<c>"?!?"</c>) - the cases where silently
    /// accepting the empty result would claim the page's own URL.
    /// </summary>
    public static bool TryNormalize(string? value, out string slug)
    {
        slug = Normalize(value);
        return slug.Length > 0;
    }
}

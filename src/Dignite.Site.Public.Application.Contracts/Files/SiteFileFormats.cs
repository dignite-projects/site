using System;

namespace Dignite.Site.Public.Files;

/// <summary>
/// The values of <see cref="GetFileInput.Format"/>: the format an image is served in.
/// </summary>
public static class SiteFileFormats
{
    /// <summary>
    /// Chosen per request (the default, also when <see cref="GetFileInput.Format"/> is empty or unknown): the HTTP
    /// endpoint serves <see cref="Webp"/> to a request whose <c>Accept</c> lists <c>image/webp</c> and
    /// <see cref="Jpeg"/> otherwise; called in-process, the application service serves <see cref="Original"/>.
    /// </summary>
    public const string Auto = "auto";

    /// <summary>A JPEG as lossy WebP, a PNG as lossless WebP; a WebP and a GIF as they are.</summary>
    public const string Webp = "webp";

    /// <summary>
    /// For a client that cannot read WebP, such as a social network fetching an <c>og:image</c>: a WebP as a JPEG (as a
    /// PNG when it has transparency); a JPEG, a PNG and a GIF as they are.
    /// </summary>
    public const string Jpeg = "jpeg";

    /// <summary>The stored picture's own format.</summary>
    public const string Original = "original";

    /// <summary>One of the formats as asked, or <see cref="Auto"/> for anything else.</summary>
    public static string Normalize(string? format)
    {
        foreach (var known in new[] { Webp, Jpeg, Original })
        {
            if (string.Equals(format, known, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return Auto;
    }
}

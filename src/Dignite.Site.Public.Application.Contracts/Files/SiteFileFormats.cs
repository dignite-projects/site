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
    /// <see cref="Original"/> otherwise; called in-process, the application service serves <see cref="Original"/>.
    /// </summary>
    public const string Auto = "auto";

    /// <summary>A JPEG as lossy WebP, a PNG as lossless WebP; any other picture in its own format.</summary>
    public const string Webp = "webp";

    /// <summary>The stored picture's own format.</summary>
    public const string Original = "original";

    /// <summary><see cref="Webp"/> or <see cref="Original"/> as asked, or <see cref="Auto"/> for anything else.</summary>
    public static string Normalize(string? format)
    {
        if (string.Equals(format, Webp, StringComparison.OrdinalIgnoreCase))
        {
            return Webp;
        }

        return string.Equals(format, Original, StringComparison.OrdinalIgnoreCase) ? Original : Auto;
    }
}

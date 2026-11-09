using SkiaSharp;

namespace Dignite.Site.Common.Files;

/// <summary>
/// Turns a decoded picture upright. A camera writes a photo sideways and records how to turn it in its EXIF
/// orientation, which a browser applies to the stored file. A picture re-encoded by SkiaSharp has no EXIF, and
/// <c>SKBitmap.Decode</c> does not apply the orientation, so whatever re-encodes a picture applies it to the pixels
/// first: the public read endpoint's rendering and the upload resize of the <c>images</c> container.
/// </summary>
public static class ImageOrientation
{
    /// <summary>
    /// <paramref name="source"/> turned as its EXIF <paramref name="origin"/> (<c>SKCodec.EncodedOrigin</c>) says: the
    /// bitmap itself when it is upright already, else a new one the caller disposes. The new bitmap has the source's
    /// <see cref="SKImageInfo"/>, sides swapped for a quarter turn, so the source has to be one a canvas draws into -
    /// 32-bit pixels.
    /// </summary>
    public static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return source;
        }

        float w = source.Width;
        float h = source.Height;

        // x' = ScaleX * x + SkewX * y + TransX, y' = SkewY * x + ScaleY * y + TransY: where a stored pixel lands.
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        if (matrix.IsIdentity)
        {
            return source;
        }

        var swapsSides = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var oriented = new SKBitmap(swapsSides
            ? source.Info.WithSize(source.Height, source.Width)
            : source.Info);
        try
        {
            using var canvas = new SKCanvas(oriented);
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(source, 0, 0);
            return oriented;
        }
        catch
        {
            oriented.Dispose();
            throw;
        }
    }
}

using System;
using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Dignite.Site.Files;

/// <summary>
/// Real file content for upload tests. <c>IFileStorer</c> detects the type from the bytes and refuses an
/// extension the content contradicts, so a test file has to be what its name says it is.
/// </summary>
internal static class TestFiles
{
    /// <summary>
    /// A JPEG of noise rather than a flat colour: a flat image compresses so well that it trips
    /// ImageResizeHandler's decompression-ratio guard (pixels per stored byte) before any resizing happens.
    /// </summary>
    public static byte[] Jpeg(int width, int height, int seed = 42)
    {
        using var image = Noise(width, height, seed);
        using var output = new MemoryStream();
        image.SaveAsJpeg(output);
        return output.ToArray();
    }

    public static byte[] Png(int width, int height, int seed = 42)
    {
        using var image = Noise(width, height, seed);
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }

    public static byte[] Webp(int width, int height, int seed = 42)
    {
        using var image = Noise(width, height, seed);
        using var output = new MemoryStream();
        image.SaveAsWebp(output);
        return output.ToArray();
    }

    /// <summary>Enough of a PDF for signature detection: the header and an end marker.</summary>
    public static byte[] Pdf(string text = "not really a pdf, but not an image either")
    {
        return Encoding.ASCII.GetBytes($"%PDF-1.4\n% {text}\n%%EOF\n");
    }

    public static byte[] Text(string text = "plain text")
    {
        return Encoding.UTF8.GetBytes(text);
    }

    /// <summary>The content a file of this name should have.</summary>
    public static byte[] For(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => Jpeg(16, 16),
            ".png" => Png(16, 16),
            ".webp" => Webp(16, 16),
            ".pdf" => Pdf(),
            _ => Text("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")
        };
    }

    private static Image<Rgb24> Noise(int width, int height, int seed)
    {
        var random = new Random(seed);
        var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                foreach (ref var pixel in rows.GetRowSpan(y))
                {
                    pixel = new Rgb24((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
                }
            }
        });
        return image;
    }
}

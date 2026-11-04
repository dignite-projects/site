using System;
using System.IO;
using System.Text;
using SkiaSharp;

namespace Dignite.Site.Files;

/// <summary>
/// Real file content for upload tests. <c>FileDescriptorManager</c> detects the type from the bytes and refuses
/// an extension the content contradicts, so a test file has to be what its name says it is. Pictures are drawn
/// and measured with SkiaSharp - the imaging provider Site runs on.
/// </summary>
internal static class TestFiles
{
    /// <summary>
    /// A JPEG of noise rather than a flat colour: a flat image compresses so well that it trips the Imaging
    /// package's decode guard on pixels per stored byte (100 by default) before any resizing happens.
    /// </summary>
    public static byte[] Jpeg(int width, int height, int seed = 42)
    {
        return Encode(width, height, seed, SKEncodedImageFormat.Jpeg);
    }

    public static byte[] Png(int width, int height, int seed = 42)
    {
        return Encode(width, height, seed, SKEncodedImageFormat.Png);
    }

    public static byte[] Webp(int width, int height, int seed = 42)
    {
        return Encode(width, height, seed, SKEncodedImageFormat.Webp);
    }

    /// <summary>Enough of a PDF for signature detection: the header and an end marker.</summary>
    public static byte[] Pdf(string text = "not really a pdf, but not an image either")
    {
        return Encoding.ASCII.GetBytes($"%PDF-1.4\n% {text}\n%%EOF\n");
    }

    /// <summary>
    /// The smallest thing the detector's header probe calls a compound file: the 8-byte signature in a
    /// 512-byte header, then one zero sector. Not a readable Word document - the signature engine cannot name it,
    /// so only the <c>.doc</c> extension decides it is <c>application/msword</c>.
    /// </summary>
    public static byte[] CompoundFile()
    {
        var bytes = new byte[1024];
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(bytes, 0);
        return bytes;
    }

    public static byte[] Text(string text = "plain text")
    {
        return Encoding.UTF8.GetBytes(text);
    }

    /// <summary>A one-image .ico: its header and directory entry, with a PNG as the image, the form browsers read.</summary>
    public static byte[] Ico(int size = 16)
    {
        var png = Png(size, size);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);          // reserved
        writer.Write((ushort)1);          // type: icon
        writer.Write((ushort)1);          // one image
        writer.Write((byte)size);         // width
        writer.Write((byte)size);         // height
        writer.Write((byte)0);            // palette colors
        writer.Write((byte)0);            // reserved
        writer.Write((ushort)1);          // color planes
        writer.Write((ushort)32);         // bits per pixel
        writer.Write(png.Length);         // image size
        writer.Write(22);                 // image offset: after the 6 + 16 bytes of header
        writer.Write(png);
        writer.Flush();

        return stream.ToArray();
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
            ".ico" => Ico(),
            _ => Text("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")
        };
    }

    /// <summary>The pixel size and encoded format of a picture, read without decoding its pixels.</summary>
    public static (int Width, int Height, SKEncodedImageFormat Format) Identify(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Not a picture SkiaSharp can read.");
        return (codec.Info.Width, codec.Info.Height, codec.EncodedFormat);
    }

    /// <summary>
    /// <see cref="Identify(byte[])"/> for a stream; reads it to the end.
    /// </summary>
    public static (int Width, int Height, SKEncodedImageFormat Format) Identify(Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Identify(memory.ToArray());
    }

    private static byte[] Encode(int width, int height, int seed, SKEncodedImageFormat format)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new byte[info.BytesSize];
        new Random(seed).NextBytes(pixels);
        for (var alpha = 3; alpha < pixels.Length; alpha += 4)
        {
            pixels[alpha] = 0xFF;
        }

        using var image = SKImage.FromPixelCopy(info, pixels, info.RowBytes);
        // Quality 75: noise barely compresses, so a 3000 x 1000 JPEG has to stay well inside images' 10 MB.
        using var data = image.Encode(format, 75);
        return data.ToArray();
    }
}

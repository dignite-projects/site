using System;
using System.Buffers.Binary;
using System.IO;

namespace Dignite.Site.Public.Files;

/// <summary>
/// Reads a picture's pixel size from its header, without decoding it, so the public read endpoint can refuse
/// to resize an oversized image before the imaging provider allocates memory for it. Covers the raster types
/// Site's containers take (<c>SiteFileTypes.Images</c>) and nothing else: PNG, JPEG, GIF and WebP. Returns
/// <c>null</c> for any other content and for a header that is truncated or does not make sense.
/// <para>
/// Site's own: <c>Dignite.Abp.BlobStoring.Imaging</c> has a header reader for its decode guard, but keeps it
/// internal.
/// </para>
/// </summary>
internal static class ImageDimensionReader
{
    private const int HeaderLength = 30;

    // A JPEG can carry any number of segments before its frame header; give up after this many, so a
    // hostile file cannot keep the walk going.
    private const int MaxJpegSegmentsToSkip = 1024;

    /// <summary>
    /// The width and height declared by the picture in <paramref name="stream"/>, which must be seekable. The
    /// stream is read from position 0 and left at the position it had.
    /// </summary>
    public static (int Width, int Height)? Read(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("The stream must be seekable.", nameof(stream));
        }

        var position = stream.Position;
        try
        {
            stream.Position = 0;
            var header = new byte[HeaderLength];
            var length = stream.ReadAtLeast(header, HeaderLength, throwOnEndOfStream: false);
            var bytes = header.AsSpan(0, length);

            if (IsPng(bytes))
            {
                return FromPng(bytes);
            }

            if (IsGif(bytes))
            {
                return FromGif(bytes);
            }

            if (IsWebp(bytes))
            {
                return FromWebp(bytes);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                return FromJpeg(stream);
            }

            return null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        finally
        {
            stream.Position = position;
        }
    }

    private static bool IsPng(ReadOnlySpan<byte> bytes)
    {
        return bytes.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
    }

    private static bool IsGif(ReadOnlySpan<byte> bytes)
    {
        return bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8);
    }

    private static bool IsWebp(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 16 && bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8);
    }

    /// <summary>The first chunk of a PNG is IHDR: its data starts with the width and height, big-endian.</summary>
    private static (int Width, int Height)? FromPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24 || !bytes[12..16].SequenceEqual("IHDR"u8))
        {
            return null;
        }

        return Size(BinaryPrimitives.ReadUInt32BigEndian(bytes[16..20]), BinaryPrimitives.ReadUInt32BigEndian(bytes[20..24]));
    }

    /// <summary>The logical screen descriptor follows the signature: width and height, little-endian.</summary>
    private static (int Width, int Height)? FromGif(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 10)
        {
            return null;
        }

        return Size(BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..8]), BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..10]));
    }

    /// <summary>The first chunk after the RIFF header says which WebP flavour this is and where its size is.</summary>
    private static (int Width, int Height)? FromWebp(ReadOnlySpan<byte> bytes)
    {
        var fourCc = bytes[12..16];

        if (fourCc.SequenceEqual("VP8X"u8))
        {
            // Extended format: the canvas size, each 24 bits little-endian and stored minus one.
            return bytes.Length < 30
                ? null
                : Size(ReadUInt24(bytes[24..27]) + 1, ReadUInt24(bytes[27..30]) + 1);
        }

        if (fourCc.SequenceEqual("VP8L"u8))
        {
            // Lossless: after the 0x2F signature byte, 14 bits each of width and height, stored minus one.
            if (bytes.Length < 25 || bytes[20] != 0x2F)
            {
                return null;
            }

            var packed = BinaryPrimitives.ReadUInt32LittleEndian(bytes[21..25]);
            return Size((packed & 0x3FFF) + 1, ((packed >> 14) & 0x3FFF) + 1);
        }

        if (fourCc.SequenceEqual("VP8 "u8))
        {
            // Lossy: a key frame's start code 9D 01 2A, then 14-bit width and height (2 bits of scale above).
            if (bytes.Length < 30 || bytes[23] != 0x9D || bytes[24] != 0x01 || bytes[25] != 0x2A)
            {
                return null;
            }

            return Size(
                (uint)(BinaryPrimitives.ReadUInt16LittleEndian(bytes[26..28]) & 0x3FFF),
                (uint)(BinaryPrimitives.ReadUInt16LittleEndian(bytes[28..30]) & 0x3FFF));
        }

        return null;
    }

    /// <summary>
    /// Walks the JPEG's marker segments from just after SOI until the frame header (any SOFn), whose payload
    /// holds the height and then the width, big-endian.
    /// </summary>
    private static (int Width, int Height)? FromJpeg(Stream stream)
    {
        var next = 2L;
        var segment = new byte[7];

        for (var skipped = 0; skipped <= MaxJpegSegmentsToSkip; skipped++)
        {
            stream.Position = next;
            if (stream.ReadByte() != 0xFF)
            {
                return null;
            }

            var marker = stream.ReadByte();
            while (marker == 0xFF)
            {
                marker = stream.ReadByte(); // fill bytes
            }

            switch (marker)
            {
                case < 0:
                case 0xD9: // end of image
                case 0xDA: // start of scan: no frame header before the image data
                    return null;
                case 0x01:
                case 0xD8:
                case >= 0xD0 and <= 0xD7:
                    // Markers without a payload.
                    next = stream.Position;
                    continue;
            }

            stream.ReadExactly(segment, 0, 2);
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(segment.AsSpan(0, 2));
            if (segmentLength < 2)
            {
                return null;
            }

            if (IsStartOfFrame(marker))
            {
                // Precision (1 byte), then the number of lines (height) and samples per line (width).
                stream.ReadExactly(segment, 2, 5);
                return Size(
                    BinaryPrimitives.ReadUInt16BigEndian(segment.AsSpan(5, 2)),
                    BinaryPrimitives.ReadUInt16BigEndian(segment.AsSpan(3, 2)));
            }

            next = stream.Position - 2 + segmentLength;
        }

        return null;
    }

    private static bool IsStartOfFrame(int marker)
    {
        // C0-CF are frame headers except C4 (Huffman tables), C8 (reserved) and CC (arithmetic coding).
        return marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
    }

    private static uint ReadUInt24(ReadOnlySpan<byte> bytes)
    {
        return bytes[0] | (uint)bytes[1] << 8 | (uint)bytes[2] << 16;
    }

    private static (int Width, int Height)? Size(uint width, uint height)
    {
        if (width == 0 || height == 0 || width > int.MaxValue || height > int.MaxValue)
        {
            return null;
        }

        return ((int)width, (int)height);
    }
}

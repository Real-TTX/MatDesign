using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace MatDesign.Tests;

/// <summary>Small files the tests of the brand assets build themselves (no binary file in the repository): a valid PNG, ICO, WOFF and WOFF2 of the right structure, and the SVGs.</summary>
internal static class AssetFixtures
{
    public const string Svg = "http://www.w3.org/2000/svg";

    /// <summary>A valid PNG of w x h px (8 bit RGB, all black): signature, IHDR, one IDAT and IEND, each with its checksum.</summary>
    public static byte[] Png(int width, int height)
    {
        var raw = new byte[height * (1 + (3 * width))];
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw);
        }

        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), (uint)height);
        header[8] = 8;
        header[9] = 2;
        return [.. new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, .. Chunk("IHDR", header), .. Chunk("IDAT", compressed.ToArray()), .. Chunk("IEND", [])];
    }

    /// <summary>A PNG whose picture data is <paramref name="payload"/> bytes of noise (it is not a picture a browser could draw: it has the structure the validator looks at, and a weight).</summary>
    public static byte[] PngWith(int width, int height, int payload, int seed)
    {
        var noise = new byte[payload];
        new Random(seed).NextBytes(noise);
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), (uint)height);
        header[8] = 8;
        header[9] = 2;
        return [.. new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, .. Chunk("IHDR", header), .. Chunk("IDAT", noise), .. Chunk("IEND", [])];
    }

    public static byte[] Chunk(string type, byte[] data)
    {
        var body = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        var chunk = new byte[12 + data.Length];
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(0, 4), (uint)data.Length);
        body.CopyTo(chunk, 4);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length, 4), Crc32(body));
        return chunk;
    }

    public static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>An ICO with one 16 x 16 image that is a PNG.</summary>
    public static byte[] Ico()
    {
        var png = Png(16, 16);
        var file = new byte[22 + png.Length];
        file[2] = 1;
        file[4] = 1;
        file[6] = 16;
        file[7] = 16;
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(10, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(12, 2), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(14, 4), (uint)png.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(18, 4), 22);
        png.CopyTo(file, 22);
        return file;
    }

    /// <summary>A WOFF file with the structure of one (header, one table), not a font a browser would draw: what the validator looks at. <paramref name="extra"/> makes two of them differ.</summary>
    public static byte[] Woff(int extra = 0)
    {
        var file = new byte[44 + 20 + 16 + extra];
        "wOFF"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(4, 4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(8, 4), (uint)file.Length);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(12, 2), 1);
        "head"u8.CopyTo(file.AsSpan(44));
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(48, 4), 64);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(52, 4), 16);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(56, 4), 16);
        return file;
    }

    /// <summary>A WOFF2 file with the header of one.</summary>
    public static byte[] Woff2(int extra = 0)
    {
        var file = new byte[48 + 32 + extra];
        "wOF2"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(4, 4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(8, 4), (uint)file.Length);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(12, 2), 1);
        return file;
    }

    public static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

    /// <summary>A plain SVG of a square mark: a rounded square and a circle.</summary>
    public static string GoodSvg(string extraRoot = "", string inner = "") =>
        $"<svg xmlns=\"{Svg}\" viewBox=\"0 0 64 64\" {extraRoot}><title>Acme</title><rect width=\"64\" height=\"64\" rx=\"14\" fill=\"#0f766e\"/><circle cx=\"32\" cy=\"32\" r=\"14\" fill=\"#ffffff\"/>{inner}</svg>";

    /// <summary>A wide SVG (a logo with its name): 6 to 1.</summary>
    public static string WideSvg() => $"<svg xmlns=\"{Svg}\" viewBox=\"0 0 240 40\"><title>Acme Anvils</title><rect width=\"240\" height=\"40\" fill=\"#0f766e\"/></svg>";
}

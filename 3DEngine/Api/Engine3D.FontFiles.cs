namespace Engine;

// What a font file is checked for before the atlas builder reads it, which stops the program on a
// file it cannot parse, and whether it holds glyphs in color.
public static partial class Engine3D
{
    /// <summary>
    /// Why bytes are not a TrueType or OpenType font the atlas builder can read safely, or null when
    /// they are: a signature it knows, a table directory and every table inside the bytes, and the
    /// tables an outline font has.
    /// </summary>
    internal static string? FontProblem(ReadOnlySpan<byte> data)
    {
        static uint U32(ReadOnlySpan<byte> d, int at) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(d[at..]);
        static ushort U16(ReadOnlySpan<byte> d, int at) => System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(d[at..]);
        if (data.Length < 100) return $"only {data.Length} bytes long";
        var start = 0;
        // A collection names its fonts' offsets, and the first is the one read.
        if (U32(data, 0) == 0x74746366)
        {
            if (data.Length < 16 || U32(data, 8) == 0) return "a collection with no fonts";
            start = (int)Math.Min(U32(data, 12), int.MaxValue);
            if (start > data.Length - 12) return "a collection whose first font is past its end";
        }
        var version = U32(data, start);
        if (version is not (0x00010000 or 0x74727565 or 0x4F54544F)) return "no TrueType or OpenType signature at its start";
        int count = U16(data, start + 4);
        if (count == 0 || start + 12 + count * 16 > data.Length) return "a table directory that runs past its end";
        var tables = new Dictionary<string, (uint Offset, uint Length)>();
        for (int i = 0; i < count; i++)
        {
            int record = start + 12 + i * 16;
            var (offset, length) = (U32(data, record + 8), U32(data, record + 12));
            var tag = System.Text.Encoding.ASCII.GetString(data.Slice(record, 4));
            if ((ulong)offset + length > (ulong)data.Length) return $"its '{tag.TrimEnd()}' table running past its end";
            tables[tag] = (offset, length);
        }
        foreach (var needed in new[] { "cmap", "head", "hhea", "hmtx", "maxp" })
            if (!tables.ContainsKey(needed)) return $"no '{needed}' table";
        // The builder reads a character map of Unicode alone, one of Unicode's platform or of
        // Microsoft's with Unicode's characters, and stops the program on a font with none, as a
        // symbol font of Microsoft's Symbol encoding is.
        var (cmap, cmapLength) = tables["cmap"];
        if (cmapLength < 4) return "a character map with no subtables";
        var unicode = false;
        for (int i = 0; i < U16(data, (int)cmap + 2) && cmap + 4 + i * 8 + 8 <= cmap + cmapLength; i++)
        {
            int record = (int)cmap + 4 + i * 8;
            var (platform, encoding) = (U16(data, record), U16(data, record + 2));
            unicode |= platform == 0 || platform == 3 && encoding is 1 or 10;
        }
        if (!unicode) return "no character map of Unicode, only of a symbol or an older encoding";
        if (!(tables.ContainsKey("glyf") && tables.ContainsKey("loca")) && !tables.ContainsKey("CFF ")
            && !(tables.ContainsKey("CBDT") && tables.ContainsKey("CBLC")) && !tables.ContainsKey("sbix"))
            return tables.ContainsKey("CFF2")
                ? "outlines of CFF2 alone, as a variable OpenType font holds them, which the atlas builder does not read"
                : "no outlines, neither 'glyf' nor 'CFF ', and no color bitmaps";
        return null;
    }

    // Whether a font file holds glyphs in color, bitmaps (CBDT or sbix) or layers (COLR), by its table
    // directory alone.
    private static bool HasColorTables(string path)
    {
        using var file = File.OpenRead(path);
        Span<byte> head = stackalloc byte[16];
        if (file.ReadAtLeast(head, 16, throwOnEndOfStream: false) < 12) return false;
        // A collection's first font, which the atlas builder and the reader read.
        if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(head) == 0x74746366)
        {
            file.Position = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(head[12..]);
            if (file.ReadAtLeast(head[..12], 12, throwOnEndOfStream: false) < 12) return false;
        }
        else file.Position = 12;
        int count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(head[4..]);
        var directory = new byte[count * 16];
        if (file.ReadAtLeast(directory, directory.Length, throwOnEndOfStream: false) < directory.Length) return false;
        for (int i = 0; i < count; i++)
            if (System.Text.Encoding.ASCII.GetString(directory, i * 16, 4) is "CBDT" or "COLR" or "sbix") return true;
        return false;
    }
}

using System.Buffers.Binary;
using System.Numerics;

namespace Engine;

/// <summary>
/// The outlines of a TrueType font's glyphs, read from the file and rasterized as coverage, and
/// the colors of a color font's, for the characters past U+FFFF that Dear ImGui's atlas builder
/// cannot name in its 16 bits and the colored ones it would bake in gray.
/// </summary>
/// <remarks>
/// <para>
/// It reads what an outline needs and nothing more, the character map (format 12, which reaches
/// every plane, or format 4), the glyphs' quadratic contours, simple or made of other glyphs, and
/// their advances. An OpenType font of cubic outlines (CFF) has none of these, and
/// <see cref="Read"/> answers null for it.
/// </para>
/// <para>
/// A color glyph is read from the font's bitmaps (CBDT and CBLC, PNG images at a size or a few,
/// as Noto Color Emoji and most color emoji fonts hold them), scaled from the size nearest above,
/// or from its layers (COLR version 0 with CPAL's first palette, as Segoe UI Emoji holds them),
/// outlines of the font's own each filled in a color, a layer in the text's color drawn white so
/// the text's color tints it, or from its paints (COLR version 1, as Noto Color Emoji holds them),
/// gradients and transforms among them, which <see cref="ColorPaint"/> draws.
/// </para>
/// <para>
/// A sequence the font joins into one glyph, as a family of emoji or a flag, is read from its GSUB
/// table by <see cref="GlyphSubstitution"/>.
/// </para>
/// <para>
/// A glyph is rasterized by accumulating, for each edge, the signed area it covers in each pixel it
/// crosses, then summing along the rows, as font-rs does, which gives exact coverage for the
/// flattened outline and fills by the nonzero rule TrueType draws with.
/// </para>
/// </remarks>
internal sealed class TrueTypeFont
{
    private readonly byte[] _data;
    private readonly int _glyf, _loca, _hmtx, _cmap12, _cmap4, _hMetrics, _glyphs;
    private readonly int _cblc, _cbdt, _colr, _cpal;
    private readonly bool _longLoca;
    private readonly ColorPaint? _paints;

    /// <summary>The units an em is drawn in.</summary>
    public int UnitsPerEm { get; }

    /// <summary>How far above the baseline the font reaches, in its units.</summary>
    public int Ascent { get; }

    /// <summary>How far below the baseline the font reaches, in its units, negative.</summary>
    public int Descent { get; }

    private TrueTypeFont(byte[] data, Dictionary<string, int> tables)
    {
        _data = data;
        var head = tables["head"];
        UnitsPerEm = U16(head + 18);
        _longLoca = S16(head + 50) != 0;
        var hhea = tables["hhea"];
        Ascent = S16(hhea + 4);
        Descent = S16(hhea + 6);
        _hMetrics = U16(hhea + 34);
        _glyphs = U16(tables["maxp"] + 4);
        _hmtx = tables["hmtx"];
        _glyf = tables.GetValueOrDefault("glyf");
        _loca = tables.GetValueOrDefault("loca");
        if (tables.TryGetValue("CBLC", out var cblc) && tables.TryGetValue("CBDT", out var cbdt)) (_cblc, _cbdt) = (cblc, cbdt);
        if (tables.TryGetValue("COLR", out var colr) && tables.TryGetValue("CPAL", out var cpal)) (_colr, _cpal) = (colr, cpal);
        if (tables.TryGetValue("GSUB", out var gsub)) Joins = GlyphSubstitution.Read(data, gsub);
        _paints = ColorPaint.Read(this, data, _colr, _cpal);

        (_cmap12, _cmap4) = Maps(data, tables["cmap"]);
    }

    /// <summary>
    /// The substitutions the font makes to join a sequence of characters into one glyph, as an
    /// emoji font joins a family or a flag, or null where it makes none.
    /// </summary>
    public GlyphSubstitution? Joins { get; }

    /// <summary>Whether the font has TrueType outlines, where one of color bitmaps alone has none.</summary>
    public bool HasOutlines => _glyf != 0 && _loca != 0;

    /// <summary>
    /// Reads a font file's outlines and colors, the first font's of a collection, or null for a
    /// file with neither the tables a TrueType outline needs nor color bitmaps.
    /// </summary>
    public static TrueTypeFont? Read(byte[] data)
    {
        // A collection's first font, the one the atlas builder reads, whose tables are found from
        // the file's start as a lone font's are.
        var tables = Directory(data);
        string[] needed = ["cmap", "head", "hhea", "hmtx", "maxp"];
        var drawable = tables.ContainsKey("glyf") && tables.ContainsKey("loca") || tables.ContainsKey("CBDT") && tables.ContainsKey("CBLC");
        return needed.All(tables.ContainsKey) && drawable ? new TrueTypeFont(data, tables) : null;
    }

    private ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(at));
    private short S16(int at) => BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(at));
    private uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(at));

    /// <summary>The glyph a character is drawn with, 0 for one the font does not have.</summary>
    public int GlyphIndex(int codepoint) => Lookup(_data, _cmap12, _cmap4, codepoint);

    // The richest Unicode map a font has, every plane's (format 12) before the first plane's (format 4).
    private static (int Cmap12, int Cmap4) Maps(byte[] data, int cmap)
    {
        int cmap12 = 0, cmap4 = 0;
        for (int i = 0; i < BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(cmap + 2)); i++)
        {
            int record = cmap + 4 + i * 8, subtable = cmap + (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 4));
            int platform = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(record)), encoding = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(record + 2));
            var format = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(subtable));
            if (format == 12 && (platform == 0 || platform == 3 && encoding == 10)) cmap12 = subtable;
            if (format == 4 && (platform == 0 || platform == 3 && encoding == 1)) cmap4 = subtable;
        }
        return (cmap12, cmap4);
    }

    // A character's glyph by a format 12 or format 4 map, 0 for one the map does not have.
    private static int Lookup(byte[] data, int cmap12, int cmap4, int codepoint)
    {
        ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at));
        short S16(int at) => BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(at));
        uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at));
        if (cmap12 != 0)
        {
            var groups = (int)U32(cmap12 + 12);
            for (int lo = 0, hi = groups - 1; lo <= hi;)
            {
                int mid = (lo + hi) / 2, group = cmap12 + 16 + mid * 12;
                if (codepoint < U32(group)) hi = mid - 1;
                else if (codepoint > U32(group + 4)) lo = mid + 1;
                else return (int)(U32(group + 8) + (uint)(codepoint - U32(group)));
            }
            return 0;
        }
        if (cmap4 == 0 || codepoint > 0xFFFF) return 0;
        int segments = U16(cmap4 + 6) / 2, ends = cmap4 + 14, starts = ends + segments * 2 + 2;
        int deltas = starts + segments * 2, offsets = deltas + segments * 2;
        for (int s = 0; s < segments; s++)
        {
            if (codepoint > U16(ends + s * 2)) continue;
            int start = U16(starts + s * 2);
            if (codepoint < start) return 0;
            int offset = U16(offsets + s * 2);
            if (offset == 0) return (codepoint + S16(deltas + s * 2)) & 0xFFFF;
            int glyph = U16(offsets + s * 2 + offset + (codepoint - start) * 2);
            return glyph == 0 ? 0 : (glyph + S16(deltas + s * 2)) & 0xFFFF;
        }
        return 0;
    }

    // The table directory of a font file's first font, a collection's first, by tag, empty for a
    // file too short to hold one.
    private static Dictionary<string, int> Directory(byte[] data)
    {
        var tables = new Dictionary<string, int>();
        if (data.Length < 12) return tables;
        var start = 0;
        if (BinaryPrimitives.ReadUInt32BigEndian(data) == 0x74746366)
        {
            if (data.Length < 16 || BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8)) == 0) return tables;
            start = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12)), int.MaxValue);
            if (start > data.Length - 12) return tables;
        }
        int count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(start + 4));
        for (int i = 0; i < count && start + 12 + i * 16 + 16 <= data.Length; i++)
        {
            int record = start + 12 + i * 16;
            tables[System.Text.Encoding.ASCII.GetString(data, record, 4)] = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 8));
        }
        return tables;
    }

    /// <summary>
    /// The characters of <paramref name="codepoints"/> a font file maps to a glyph, read from its
    /// character map alone, so a font of any outlines, CFF among them, is asked.
    /// </summary>
    /// <remarks>
    /// The atlas builder stops the program on a font in which it finds none of the characters it is
    /// given, so what it is given is checked against the font first.
    /// </remarks>
    internal static int[] Mapped(byte[] data, IEnumerable<int> codepoints)
    {
        try
        {
            if (!Directory(data).TryGetValue("cmap", out var cmap)) return [];
            var (cmap12, cmap4) = Maps(data, cmap);
            return [.. codepoints.Where(c => Lookup(data, cmap12, cmap4, c) != 0)];
        }
        catch (ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    /// <summary>The first character below U+10000 a font file maps to a glyph, or null where it maps none.</summary>
    internal static int? FirstMapped(byte[] data)
    {
        try
        {
            if (!Directory(data).TryGetValue("cmap", out var cmap)) return null;
            var (cmap12, cmap4) = Maps(data, cmap);
            if (cmap12 != 0)
            {
                // A group's first character, the first group below U+10000 with a glyph.
                for (int g = 0; g < (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(cmap12 + 12)); g++)
                {
                    var first = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(cmap12 + 16 + g * 12));
                    if (first > 0 && first <= 0xFFFF && Lookup(data, cmap12, cmap4, first) != 0) return first;
                }
                return null;
            }
            if (cmap4 == 0) return null;
            int segments = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(cmap4 + 6)) / 2, starts = cmap4 + 14 + segments * 2 + 2;
            for (int s = 0; s < segments; s++)
            {
                int start = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(starts + s * 2)), end = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(cmap4 + 14 + s * 2));
                for (int c = Math.Max(1, start); c <= end && c < 0xFFFF; c++)
                    if (Lookup(data, cmap12, cmap4, c) != 0) return c;
            }
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>How far a glyph moves the pen, in the font's units.</summary>
    public int Advance(int glyph) => U16(_hmtx + Math.Min(glyph, _hMetrics - 1) * 4);

    private (int Start, int Length) GlyphData(int glyph)
    {
        if (glyph < 0 || glyph >= _glyphs || !HasOutlines) return (0, 0);
        int start = _longLoca ? (int)U32(_loca + glyph * 4) : U16(_loca + glyph * 2) * 2;
        int end = _longLoca ? (int)U32(_loca + glyph * 4 + 4) : U16(_loca + glyph * 2 + 2) * 2;
        return (_glyf + start, end - start);
    }

    /// <summary>A glyph's contours in the font's units, each point on the curve or a control point between two.</summary>
    public List<List<(Vector2 At, bool On)>> Contours(int glyph, int depth = 0)
    {
        var contours = new List<List<(Vector2, bool)>>();
        var (at, length) = GlyphData(glyph);
        if (length <= 0 || depth > 8) return contours;
        int count = S16(at);
        if (count < 0)
        {
            // Made of other glyphs, each moved and scaled.
            int p = at + 10;
            const int Words = 1, XyValues = 2, Scale = 8, More = 32, XyScale = 64, TwoByTwo = 128;
            int flags;
            do
            {
                flags = U16(p);
                int part = U16(p + 2);
                p += 4;
                float dx, dy;
                if ((flags & Words) != 0) { dx = S16(p); dy = S16(p + 2); p += 4; }
                else { dx = (sbyte)_data[p]; dy = (sbyte)_data[p + 1]; p += 2; }
                if ((flags & XyValues) == 0) (dx, dy) = (0, 0);
                var m = Matrix3x2.Identity;
                if ((flags & Scale) != 0) { var s = S16(p) / 16384f; m = Matrix3x2.CreateScale(s); p += 2; }
                else if ((flags & XyScale) != 0) { m = Matrix3x2.CreateScale(S16(p) / 16384f, S16(p + 2) / 16384f); p += 4; }
                else if ((flags & TwoByTwo) != 0) { m = new Matrix3x2(S16(p) / 16384f, S16(p + 2) / 16384f, S16(p + 4) / 16384f, S16(p + 6) / 16384f, 0, 0); p += 8; }
                m.Translation = new Vector2(dx, dy);
                foreach (var contour in Contours(part, depth + 1))
                    contours.Add([.. contour.Select(c => (Vector2.Transform(c.At, m), c.On))]);
            }
            while ((flags & More) != 0);
            return contours;
        }

        var endPoints = new int[count];
        for (int c = 0; c < count; c++) endPoints[c] = U16(at + 10 + c * 2);
        int points = count == 0 ? 0 : endPoints[^1] + 1;
        int q = at + 10 + count * 2;
        q += 2 + U16(q);
        var pointFlags = new byte[points];
        for (int i = 0; i < points;)
        {
            var flag = _data[q++];
            pointFlags[i++] = flag;
            if ((flag & 8) != 0)
                for (int repeat = _data[q++]; repeat > 0 && i < points; repeat--) pointFlags[i++] = flag;
        }
        int[] Coordinates(byte shortBit, byte sameBit)
        {
            var values = new int[points];
            int value = 0;
            for (int i = 0; i < points; i++)
            {
                var flag = pointFlags[i];
                if ((flag & shortBit) != 0) value += (flag & sameBit) != 0 ? _data[q++] : -_data[q++];
                else if ((flag & sameBit) == 0) { value += S16(q); q += 2; }
                values[i] = value;
            }
            return values;
        }
        var xs = Coordinates(2, 16);
        var ys = Coordinates(4, 32);
        int first = 0;
        foreach (var end in endPoints)
        {
            var contour = new List<(Vector2, bool)>();
            for (int i = first; i <= end; i++) contour.Add((new Vector2(xs[i], ys[i]), (pointFlags[i] & 1) != 0));
            contours.Add(contour);
            first = end + 1;
        }
        return contours;
    }

    /// <summary>A glyph's contours as straight segments in the font's units, y up, each curve cut in eight.</summary>
    internal List<(Vector2 A, Vector2 B)> Segments(int glyph)
    {
        var lines = new List<(Vector2 A, Vector2 B)>();
        foreach (var contour in Contours(glyph))
        {
            if (contour.Count < 2) continue;
            var path = new List<Vector2>();
            // A contour starts at an on-curve point, or between two control points.
            int start = contour.FindIndex(p => p.On);
            var begin = start >= 0 ? contour[start].At : (contour[0].At + contour[1].At) / 2;
            if (start < 0) start = 0;
            path.Add(begin);
            var control = (Vector2?)null;
            for (int k = 1; k <= contour.Count; k++)
            {
                var (point, on) = contour[(start + k) % contour.Count];
                if (k == contour.Count) (point, on) = (begin, true);
                if (on)
                {
                    if (control is { } c) Curve(path, c, point);
                    else path.Add(point);
                    control = null;
                }
                else
                {
                    if (control is { } c) Curve(path, c, (c + point) / 2);
                    control = point;
                }
            }
            for (int i = 0; i + 1 < path.Count; i++) lines.Add((path[i], path[i + 1]));
        }
        return lines;
    }

    /// <summary>
    /// A glyph drawn at <paramref name="scale"/> pixels a unit as coverage from 0 to 255, its box's
    /// left and top relative to the pen on the baseline, y down, or null for a glyph with no outline.
    /// </summary>
    public (byte[] Alpha, int Width, int Height, int Left, int Top)? Rasterize(int glyph, float scale)
    {
        // Y turned to grow down.
        var lines = Segments(glyph).Select(l => (A: new Vector2(l.A.X, -l.A.Y) * scale, B: new Vector2(l.B.X, -l.B.Y) * scale)).ToList();
        if (lines.Count == 0) return null;

        var min = new Vector2(lines.Min(l => MathF.Min(l.A.X, l.B.X)), lines.Min(l => MathF.Min(l.A.Y, l.B.Y)));
        var max = new Vector2(lines.Max(l => MathF.Max(l.A.X, l.B.X)), lines.Max(l => MathF.Max(l.A.Y, l.B.Y)));
        int left = (int)MathF.Floor(min.X), top = (int)MathF.Floor(min.Y);
        int width = Math.Max(1, (int)MathF.Ceiling(max.X) - left), height = Math.Max(1, (int)MathF.Ceiling(max.Y) - top);
        var accumulated = new float[width * height + 4];
        var origin = new Vector2(left, top);
        foreach (var (a, b) in lines) Line(accumulated, width, height, a - origin, b - origin);

        var alpha = new byte[width * height];
        float sum = 0;
        for (int i = 0; i < alpha.Length; i++)
        {
            sum += accumulated[i];
            alpha[i] = (byte)(Math.Min(1f, MathF.Abs(sum)) * 255 + 0.5f);
        }
        return (alpha, width, height, left, top);
    }

    /// <summary>
    /// A glyph's colors drawn at <paramref name="scale"/> pixels a unit, four bytes a pixel, its
    /// box's left and top relative to the pen on the baseline, y down, or null for a glyph with
    /// no color of its own.
    /// </summary>
    public (byte[] Rgba, int Width, int Height, int Left, int Top)? Color(int glyph, float scale) =>
        Bitmap(glyph, scale) ?? _paints?.Draw(glyph, scale) ?? Layers(glyph, scale);

    /// <summary>Whether a glyph has colors of its own, a bitmap, paints or layers.</summary>
    public bool HasColor(int glyph) => BitmapData(glyph, 0) is not null || _paints?.PaintOf(glyph) is not null || BaseRecord(glyph) is not null;

    // The strike whose size is nearest above pixelsPerEm, or the largest, and in it where a glyph's
    // image is in CBDT, its format and, for an index that holds them, the metrics every glyph of it
    // shares, read from CBLC's index subtables in formats 1 to 5.
    private (int Ppem, int Image, int Length, int Format, int Metrics)? BitmapData(int glyph, float pixelsPerEm)
    {
        if (_cblc == 0) return null;
        int sizes = (int)U32(_cblc + 4), chosen = -1, chosenPpem = 0;
        for (int s = 0; s < sizes; s++)
        {
            int record = _cblc + 8 + s * 48;
            if (glyph < U16(record + 40) || glyph > U16(record + 42)) continue;
            int ppem = _data[record + 44];
            bool better = chosen < 0 || (ppem >= pixelsPerEm ? chosenPpem < pixelsPerEm || ppem < chosenPpem : ppem > chosenPpem);
            if (better) (chosen, chosenPpem) = (record, ppem);
        }
        if (chosen < 0) return null;
        int array = _cblc + (int)U32(chosen), subtables = (int)U32(chosen + 8);
        for (int i = 0; i < subtables; i++)
        {
            int entry = array + i * 8, first = U16(entry), last = U16(entry + 2);
            if (glyph < first || glyph > last) continue;
            int header = array + (int)U32(entry + 4);
            int indexFormat = U16(header), imageFormat = U16(header + 2), data = _cbdt + (int)U32(header + 4);
            switch (indexFormat)
            {
                case 1:
                {
                    int start = (int)U32(header + 8 + (glyph - first) * 4), end = (int)U32(header + 8 + (glyph - first + 1) * 4);
                    return end > start ? (chosenPpem, data + start, end - start, imageFormat, 0) : null;
                }
                case 3:
                {
                    int start = U16(header + 8 + (glyph - first) * 2), end = U16(header + 8 + (glyph - first + 1) * 2);
                    return end > start ? (chosenPpem, data + start, end - start, imageFormat, 0) : null;
                }
                case 2:
                {
                    int size = (int)U32(header + 8);
                    return (chosenPpem, data + size * (glyph - first), size, imageFormat, header + 12);
                }
                case 4:
                {
                    int count = (int)U32(header + 8);
                    for (int g = 0; g < count; g++)
                        if (U16(header + 12 + g * 4) == glyph)
                        {
                            int start = U16(header + 14 + g * 4), end = U16(header + 14 + (g + 1) * 4);
                            return (chosenPpem, data + start, end - start, imageFormat, 0);
                        }
                    return null;
                }
                case 5:
                {
                    int size = (int)U32(header + 8), count = (int)U32(header + 20);
                    for (int g = 0; g < count; g++)
                        if (U16(header + 24 + g * 2) == glyph) return (chosenPpem, data + size * g, size, imageFormat, header + 12);
                    return null;
                }
            }
        }
        return null;
    }

    // A glyph's bitmap from CBDT, its PNG decoded and scaled from its strike's size to scale's,
    // placed by its metrics: small (format 17), big (18), or the index's (19).
    private (byte[] Rgba, int Width, int Height, int Left, int Top)? Bitmap(int glyph, float scale)
    {
        var pixelsPerEm = scale * UnitsPerEm;
        if (BitmapData(glyph, pixelsPerEm) is not { } found) return null;
        var (ppem, at, length, format, metrics) = found;
        int bearingX, bearingY, png, pngLength;
        switch (format)
        {
            case 17:
                (bearingX, bearingY) = ((sbyte)_data[at + 2], (sbyte)_data[at + 3]);
                (png, pngLength) = (at + 9, (int)U32(at + 5));
                break;
            case 18:
                (bearingX, bearingY) = ((sbyte)_data[at + 2], (sbyte)_data[at + 3]);
                (png, pngLength) = (at + 12, (int)U32(at + 8));
                break;
            case 19 when metrics != 0:
                (bearingX, bearingY) = ((sbyte)_data[metrics + 2], (sbyte)_data[metrics + 3]);
                (png, pngLength) = (at + 4, (int)U32(at));
                break;
            default:
                return null;
        }
        if (pngLength <= 0 || png + pngLength > _data.Length || pngLength > length) return null;
        StbImageSharp.ImageResult image;
        try
        {
            image = StbImageSharp.ImageResult.FromMemory(_data[png..(png + pngLength)], StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            return null;
        }
        var factor = ppem > 0 ? pixelsPerEm / ppem : 1;
        int width = Math.Max(1, (int)MathF.Round(image.Width * factor)), height = Math.Max(1, (int)MathF.Round(image.Height * factor));
        return (Resample(image.Data, image.Width, image.Height, width, height), width, height,
            (int)MathF.Round(bearingX * factor), -(int)MathF.Round(bearingY * factor));
    }

    // RGBA pixels scaled to another size, each new pixel the average of the old ones its square
    // covers, weighted by alpha so a clear pixel's color does not bleed into its neighbors.
    private static byte[] Resample(byte[] rgba, int width, int height, int toWidth, int toHeight)
    {
        if (width == toWidth && height == toHeight) return rgba;
        var result = new byte[toWidth * toHeight * 4];
        float sx = (float)width / toWidth, sy = (float)height / toHeight;
        for (int y = 0; y < toHeight; y++)
            for (int x = 0; x < toWidth; x++)
            {
                float x0 = x * sx, x1 = x0 + sx, y0 = y * sy, y1 = y0 + sy;
                float r = 0, g = 0, b = 0, a = 0, area = 0;
                for (int py = (int)y0; py < Math.Min(height, (int)MathF.Ceiling(y1)); py++)
                    for (int px = (int)x0; px < Math.Min(width, (int)MathF.Ceiling(x1)); px++)
                    {
                        float w = (MathF.Min(px + 1, x1) - MathF.Max(px, x0)) * (MathF.Min(py + 1, y1) - MathF.Max(py, y0));
                        int i = (py * width + px) * 4;
                        float alpha = rgba[i + 3] / 255f * w;
                        (r, g, b, a, area) = (r + rgba[i] * alpha, g + rgba[i + 1] * alpha, b + rgba[i + 2] * alpha, a + alpha, area + w);
                    }
                int o = (y * toWidth + x) * 4;
                if (a > 0) (result[o], result[o + 1], result[o + 2]) = ((byte)(r / a + 0.5f), (byte)(g / a + 0.5f), (byte)(b / a + 0.5f));
                result[o + 3] = (byte)(area > 0 ? a / area * 255 + 0.5f : 0);
            }
        return result;
    }

    // A glyph's base record in COLR version 0, the first of its layers and how many, found by the
    // binary search the records' order by glyph allows.
    private (int First, int Count)? BaseRecord(int glyph)
    {
        if (_colr == 0) return null;
        int count = U16(_colr + 2), records = _colr + (int)U32(_colr + 4);
        for (int lo = 0, hi = count - 1; lo <= hi;)
        {
            int mid = (lo + hi) / 2, record = records + mid * 6, id = U16(record);
            if (glyph < id) hi = mid - 1;
            else if (glyph > id) lo = mid + 1;
            else return (U16(record + 2), U16(record + 4));
        }
        return null;
    }

    // A glyph's layers from COLR version 0, each an outline of the font's own filled in its color
    // from CPAL's first palette, laid over the ones before in order, the text's color (palette
    // index 0xFFFF) drawn white for the text's color to tint.
    private (byte[] Rgba, int Width, int Height, int Left, int Top)? Layers(int glyph, float scale)
    {
        if (BaseRecord(glyph) is not { } layers || layers.Count == 0) return null;
        int layerRecords = _colr + (int)U32(_colr + 8);
        int entries = U16(_cpal + 2), colorRecords = _cpal + (int)U32(_cpal + 8), firstColor = U16(_cpal + 12);
        var drawn = new List<((byte[] Alpha, int Width, int Height, int Left, int Top) Shape, (byte R, byte G, byte B, byte A) Color)>();
        for (int i = 0; i < layers.Count; i++)
        {
            int layer = layerRecords + (layers.First + i) * 4, palette = U16(layer + 2);
            if (Rasterize(U16(layer), scale) is not { } shape) continue;
            var paint = palette == 0xFFFF || palette >= entries ? ((byte)255, (byte)255, (byte)255, (byte)255) : ColorRecord(colorRecords + (firstColor + palette) * 4);
            drawn.Add((shape, paint));
        }
        if (drawn.Count == 0) return null;
        int left = drawn.Min(d => d.Shape.Left), top = drawn.Min(d => d.Shape.Top);
        int width = drawn.Max(d => d.Shape.Left + d.Shape.Width) - left, height = drawn.Max(d => d.Shape.Top + d.Shape.Height) - top;
        // Laid over in straight color with alpha, each layer's coverage times its color's alpha.
        var color = new float[width * height * 4];
        foreach (var ((alpha, w, h, l, t), (r, g, b, a)) in drawn)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cover = alpha[y * w + x] / 255f * a / 255f;
                    if (cover <= 0) continue;
                    int o = ((t - top + y) * width + l - left + x) * 4;
                    float under = color[o + 3] * (1 - cover), sum = cover + under;
                    color[o] = (r / 255f * cover + color[o] * under) / sum;
                    color[o + 1] = (g / 255f * cover + color[o + 1] * under) / sum;
                    color[o + 2] = (b / 255f * cover + color[o + 2] * under) / sum;
                    color[o + 3] = sum;
                }
        var rgba = new byte[color.Length];
        for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(Math.Clamp(color[i], 0, 1) * 255 + 0.5f);
        return (rgba, width, height, left, top);
    }

    // A CPAL color record, stored blue, green, red and alpha.
    private (byte R, byte G, byte B, byte A) ColorRecord(int at) => (_data[at + 2], _data[at + 1], _data[at], _data[at + 3]);

    // A quadratic curve from the path's last point through a control point, cut into straight pieces.
    private static void Curve(List<Vector2> path, Vector2 control, Vector2 end)
    {
        var from = path[^1];
        const int Pieces = 8;
        for (int i = 1; i <= Pieces; i++)
        {
            float t = (float)i / Pieces, u = 1 - t;
            path.Add(from * u * u + control * 2 * u * t + end * t * t);
        }
    }

    // Adds the signed area a segment covers to each pixel it crosses, row by row, as font-rs does,
    // so a running sum along the rows gives each pixel's coverage.
    internal static void Line(float[] a, int width, int height, Vector2 p0, Vector2 p1)
    {
        if (p0.Y == p1.Y) return;
        float direction = 1;
        if (p0.Y > p1.Y) { (p0, p1) = (p1, p0); direction = -1; }
        float dxdy = (p1.X - p0.X) / (p1.Y - p0.Y);
        float x = p0.X;
        int y0 = Math.Max(0, (int)p0.Y);
        if (p0.Y < 0) x -= p0.Y * dxdy;
        int yEnd = Math.Min(height, (int)MathF.Ceiling(p1.Y));
        for (int y = y0; y < yEnd; y++)
        {
            int row = y * width;
            float dy = MathF.Min(y + 1, p1.Y) - MathF.Max(y, p0.Y);
            float next = x + dxdy * dy;
            float d = dy * direction;
            var (x0, x1) = x < next ? (x, next) : (next, x);
            float x0Floor = MathF.Floor(x0);
            int x0i = Math.Clamp((int)x0Floor, 0, width);
            float x1Ceil = MathF.Ceiling(x1);
            int x1i = Math.Clamp((int)x1Ceil, 0, width + 1);
            if (x1i <= x0i + 1)
            {
                float xmf = 0.5f * (x + next) - x0Floor;
                a[row + x0i] += d - d * xmf;
                a[row + x0i + 1] += d * xmf;
            }
            else
            {
                float s = 1 / (x1 - x0);
                float x0f = x0 - x0Floor;
                float a0 = 0.5f * s * (1 - x0f) * (1 - x0f);
                float x1f = x1 - x1Ceil + 1;
                float am = 0.5f * s * x1f * x1f;
                a[row + x0i] += d * a0;
                if (x1i == x0i + 2) a[row + x0i + 1] += d * (1 - a0 - am);
                else
                {
                    float a1 = s * (1.5f - x0f);
                    a[row + x0i + 1] += d * (a1 - a0);
                    for (int xi = x0i + 2; xi < x1i - 1; xi++) a[row + xi] += d * s;
                    float a2 = a1 + (x1i - x0i - 3) * s;
                    a[row + x1i - 1] += d * (1 - a2 - am);
                }
                a[row + x1i] += d * am;
            }
            x = next;
        }
    }
}

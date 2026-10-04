using System.Buffers.Binary;
using System.Numerics;

namespace Engine;

/// <summary>
/// The outlines of a TrueType font's glyphs, read from the file and rasterized as coverage, for the
/// characters past U+FFFF that Dear ImGui's atlas builder cannot name in its 16 bits.
/// </summary>
/// <remarks>
/// <para>
/// It reads what an outline needs and nothing more, the character map (format 12, which reaches
/// every plane, or format 4), the glyphs' quadratic contours, simple or made of other glyphs, and
/// their advances. An OpenType font of cubic outlines (CFF) has none of these, and
/// <see cref="Read"/> answers null for it.
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
    private readonly bool _longLoca;

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
        (_glyf, _loca, _hmtx) = (tables["glyf"], tables["loca"], tables["hmtx"]);

        // The richest map the file has, every plane's before the first plane's.
        var cmap = tables["cmap"];
        for (int i = 0; i < U16(cmap + 2); i++)
        {
            int record = cmap + 4 + i * 8, subtable = cmap + (int)U32(record + 4);
            var (platform, encoding) = (U16(record), U16(record + 2));
            if (U16(subtable) == 12 && (platform == 0 || platform == 3 && encoding == 10)) _cmap12 = subtable;
            if (U16(subtable) == 4 && (platform == 0 || platform == 3 && encoding == 1)) _cmap4 = subtable;
        }
    }

    /// <summary>Reads a font file's outlines, or null for a file without the tables a TrueType outline needs.</summary>
    public static TrueTypeFont? Read(byte[] data)
    {
        if (data.Length < 12) return null;
        var tables = new Dictionary<string, int>();
        int count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
        for (int i = 0; i < count && 12 + i * 16 + 16 <= data.Length; i++)
        {
            int record = 12 + i * 16;
            var tag = System.Text.Encoding.ASCII.GetString(data, record, 4);
            tables[tag] = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 8));
        }
        string[] needed = ["cmap", "glyf", "head", "hhea", "hmtx", "loca", "maxp"];
        return needed.All(tables.ContainsKey) ? new TrueTypeFont(data, tables) : null;
    }

    private ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(at));
    private short S16(int at) => BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(at));
    private uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(at));

    /// <summary>The glyph a character is drawn with, 0 for one the font does not have.</summary>
    public int GlyphIndex(int codepoint)
    {
        if (_cmap12 != 0)
        {
            var groups = (int)U32(_cmap12 + 12);
            for (int lo = 0, hi = groups - 1; lo <= hi;)
            {
                int mid = (lo + hi) / 2, group = _cmap12 + 16 + mid * 12;
                if (codepoint < U32(group)) hi = mid - 1;
                else if (codepoint > U32(group + 4)) lo = mid + 1;
                else return (int)(U32(group + 8) + (uint)(codepoint - U32(group)));
            }
            return 0;
        }
        if (_cmap4 == 0 || codepoint > 0xFFFF) return 0;
        int segments = U16(_cmap4 + 6) / 2, ends = _cmap4 + 14, starts = ends + segments * 2 + 2;
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

    /// <summary>How far a glyph moves the pen, in the font's units.</summary>
    public int Advance(int glyph) => U16(_hmtx + Math.Min(glyph, _hMetrics - 1) * 4);

    private (int Start, int Length) GlyphData(int glyph)
    {
        if (glyph < 0 || glyph >= _glyphs) return (0, 0);
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

    /// <summary>
    /// A glyph drawn at <paramref name="scale"/> pixels a unit as coverage from 0 to 255, its box's
    /// left and top relative to the pen on the baseline, y down, or null for a glyph with no outline.
    /// </summary>
    public (byte[] Alpha, int Width, int Height, int Left, int Top)? Rasterize(int glyph, float scale)
    {
        // Each contour as straight segments, its curves cut in eight, y turned to grow down.
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
            for (int i = 0; i + 1 < path.Count; i++)
                lines.Add((new Vector2(path[i].X, -path[i].Y) * scale, new Vector2(path[i + 1].X, -path[i + 1].Y) * scale));
        }
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
    private static void Line(float[] a, int width, int height, Vector2 p0, Vector2 p1)
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

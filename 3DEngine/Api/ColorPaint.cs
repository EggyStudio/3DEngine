using System.Buffers.Binary;
using System.Numerics;

namespace Engine;

/// <summary>
/// The color glyphs of a font's COLR table in version 1, each a graph of paints drawn into an image
/// the size of its clip box: layers laid over each other, an outline filling what is under it with a
/// solid color or a linear, radial or sweep gradient, transforms moving what is under them, another
/// color glyph drawn in place, and two paints composited by a mode.
/// </summary>
/// <remarks>
/// <para>
/// Noto Color Emoji is built this way, and so are the color fonts Google Fonts serves, which a
/// renderer of COLR version 0's layers alone draws in one color. Variations are read at the font's
/// default, so a variable font's color glyphs are drawn as its default instance. A paint's colors
/// are CPAL's first palette's, a color of index 0xFFFF the text's, drawn white for the text's color
/// to tint, as COLR version 0's are.
/// </para>
/// <para>
/// A paint is drawn into a layer of premultiplied color the size of the glyph's clip box, or of the
/// em where the font gives none, and an outline into a mask of coverage that the paint under it is
/// multiplied by, its contours rasterized as <see cref="TrueTypeFont.Rasterize"/> rasterizes them.
/// Gradients are interpolated in premultiplied color between their stops, padded, repeated or
/// reflected past them as their color line says. The Porter and Duff modes and the separable blend
/// modes are composited as the specification gives them, and the four that work on hue,
/// saturation, color and luminosity lay the source over the backdrop.
/// </para>
/// </remarks>
internal sealed class ColorPaint
{
    private readonly TrueTypeFont _font;
    private readonly byte[] _data;
    private readonly int _baseGlyphs, _layers, _clips;
    private readonly int _colorRecords, _firstColor, _entries;

    // How deep paints may nest, past which a font's loop of them is cut.
    private const int MaxDepth = 32;

    private ColorPaint(TrueTypeFont font, byte[] data, int colr, int cpal)
    {
        (_font, _data) = (font, data);
        _baseGlyphs = colr + (int)U32(colr + 14);
        _layers = U32(colr + 18) is var layers and > 0 ? colr + (int)layers : 0;
        _clips = U32(colr + 22) is var clips and > 0 ? colr + (int)clips : 0;
        (_entries, _colorRecords, _firstColor) = (U16(cpal + 2), cpal + (int)U32(cpal + 8), U16(cpal + 12));
    }

    /// <summary>The color glyphs of a COLR table in version 1, or null for one in version 0 or with no list of them.</summary>
    public static ColorPaint? Read(TrueTypeFont font, byte[] data, int colr, int cpal)
    {
        if (colr == 0 || cpal == 0 || colr + 34 > data.Length || BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(colr)) < 1) return null;
        return BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(colr + 14)) == 0 ? null : new ColorPaint(font, data, colr, cpal);
    }

    private ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(at));
    private short S16(int at) => BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(at));
    private uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(at));
    private int U24(int at) => _data[at] << 16 | _data[at + 1] << 8 | _data[at + 2];
    private float F2Dot14(int at) => S16(at) / 16384f;
    private float Fixed(int at) => BinaryPrimitives.ReadInt32BigEndian(_data.AsSpan(at)) / 65536f;

    /// <summary>The paint a glyph is drawn with, or null where the glyph has none.</summary>
    public int? PaintOf(int glyph)
    {
        long count = U32(_baseGlyphs);
        for (long lo = 0, hi = count - 1; lo <= hi;)
        {
            var mid = (lo + hi) / 2;
            var record = _baseGlyphs + 4 + (int)mid * 6;
            var id = U16(record);
            if (glyph < id) hi = mid - 1;
            else if (glyph > id) lo = mid + 1;
            else return _baseGlyphs + (int)U32(record + 2);
        }
        return null;
    }

    // The box a glyph's paints are clipped to, in the font's units, or null where the font gives none.
    private (float XMin, float YMin, float XMax, float YMax)? ClipBox(int glyph)
    {
        if (_clips == 0) return null;
        long count = U32(_clips + 1);
        for (long lo = 0, hi = count - 1; lo <= hi;)
        {
            var mid = (lo + hi) / 2;
            var clip = _clips + 5 + (int)mid * 7;
            if (glyph < U16(clip)) hi = mid - 1;
            else if (glyph > U16(clip + 2)) lo = mid + 1;
            else
            {
                var box = _clips + U24(clip + 4);
                return (S16(box + 1), S16(box + 3), S16(box + 5), S16(box + 7));
            }
        }
        return null;
    }

    /// <summary>
    /// A glyph's paints drawn at <paramref name="scale"/> pixels a unit, four bytes a pixel, its
    /// box's left and top relative to the pen on the baseline, y down, or null for a glyph with none.
    /// </summary>
    public (byte[] Rgba, int Width, int Height, int Left, int Top)? Draw(int glyph, float scale)
    {
        if (PaintOf(glyph) is not { } paint) return null;
        var (xMin, yMin, xMax, yMax) = ClipBox(glyph) ?? (0, _font.Descent, _font.Advance(glyph), _font.Ascent);
        int left = (int)MathF.Floor(xMin * scale), top = (int)MathF.Floor(-yMax * scale);
        int width = Math.Max(1, (int)MathF.Ceiling(xMax * scale) - left), height = Math.Max(1, (int)MathF.Ceiling(-yMin * scale) - top);
        // From the font's units, y up, to the image's pixels, y down.
        var canvas = new Canvas(width, height, new Matrix3x2(scale, 0, 0, -scale, -left, -top));

        var color = Render(paint, Matrix3x2.Identity, null, canvas, 0);
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            var a = color[i * 4 + 3];
            if (a <= 0) continue;
            for (int c = 0; c < 3; c++) rgba[i * 4 + c] = (byte)(Math.Clamp(color[i * 4 + c] / a, 0, 1) * 255 + 0.5f);
            rgba[i * 4 + 3] = (byte)(Math.Clamp(a, 0, 1) * 255 + 0.5f);
        }
        return (rgba, width, height, left, top);
    }

    // The image's size and how the font's units map onto its pixels.
    private readonly record struct Canvas(int Width, int Height, Matrix3x2 FromUnits);

    // A paint drawn into a layer of premultiplied color the canvas's size, through the transform
    // from its own space to the font's units, and multiplied by the coverage of the outlines it is under.
    private float[] Render(int paint, Matrix3x2 transform, float[]? mask, Canvas canvas, int depth)
    {
        if (depth > MaxDepth) return new float[canvas.Width * canvas.Height * 4];
        var format = _data[paint];
        Matrix3x2 Around(Matrix3x2 m, int center) =>
            Matrix3x2.CreateTranslation(-S16(center), -S16(center + 2)) * m * Matrix3x2.CreateTranslation(S16(center), S16(center + 2));
        float[] Child(Matrix3x2 m) => Render(paint + U24(paint + 1), m * transform, mask, canvas, depth + 1);
        switch (format)
        {
            case 1:
            {
                var layers = new float[canvas.Width * canvas.Height * 4];
                if (_layers == 0) return layers;
                int count = _data[paint + 1], first = (int)U32(paint + 2);
                for (int i = 0; i < count; i++)
                    Composite(Render(_layers + (int)U32(_layers + 4 + (first + i) * 4), transform, mask, canvas, depth + 1), layers, 3);
                return layers;
            }
            case 2 or 3:
            {
                var color = PaletteColor(U16(paint + 1), F2Dot14(paint + 3));
                return Fill(canvas, mask, transform, _ => color);
            }
            case 4 or 5:
                return LinearGradient(paint, format == 5, transform, mask, canvas);
            case 6 or 7:
                return RadialGradient(paint, format == 7, transform, mask, canvas);
            case 8 or 9:
                return SweepGradient(paint, format == 9, transform, mask, canvas);
            case 10:
            {
                var coverage = Coverage(U16(paint + 4), transform * canvas.FromUnits, canvas.Width, canvas.Height);
                if (mask is not null) for (int i = 0; i < coverage.Length; i++) coverage[i] *= mask[i];
                return Render(paint + U24(paint + 1), transform, coverage, canvas, depth + 1);
            }
            case 11:
                return PaintOf(U16(paint + 1)) is { } other ? Render(other, transform, mask, canvas, depth + 1) : new float[canvas.Width * canvas.Height * 4];
            case 12 or 13:
            {
                var at = paint + U24(paint + 4);
                return Child(new Matrix3x2(Fixed(at), Fixed(at + 4), Fixed(at + 8), Fixed(at + 12), Fixed(at + 16), Fixed(at + 20)));
            }
            case 14 or 15:
                return Child(Matrix3x2.CreateTranslation(S16(paint + 4), S16(paint + 6)));
            case 16 or 17:
                return Child(Matrix3x2.CreateScale(F2Dot14(paint + 4), F2Dot14(paint + 6)));
            case 18 or 19:
                return Child(Around(Matrix3x2.CreateScale(F2Dot14(paint + 4), F2Dot14(paint + 6)), paint + 8));
            case 20 or 21:
                return Child(Matrix3x2.CreateScale(F2Dot14(paint + 4)));
            case 22 or 23:
                return Child(Around(Matrix3x2.CreateScale(F2Dot14(paint + 4)), paint + 6));
            case 24 or 25:
                return Child(Matrix3x2.CreateRotation(F2Dot14(paint + 4) * MathF.PI));
            case 26 or 27:
                return Child(Around(Matrix3x2.CreateRotation(F2Dot14(paint + 4) * MathF.PI), paint + 6));
            case 28 or 29:
                return Child(Skew(F2Dot14(paint + 4), F2Dot14(paint + 6)));
            case 30 or 31:
                return Child(Around(Skew(F2Dot14(paint + 4), F2Dot14(paint + 6)), paint + 8));
            case 32:
            {
                var backdrop = Render(paint + U24(paint + 5), transform, mask, canvas, depth + 1);
                Composite(Render(paint + U24(paint + 1), transform, mask, canvas, depth + 1), backdrop, _data[paint + 4]);
                return backdrop;
            }
            default:
                return new float[canvas.Width * canvas.Height * 4];
        }
    }

    // A skew by angles in half turns, the x axis's counterclockwise and the y axis's, as fontTools
    // turns COLR's into a transform.
    private static Matrix3x2 Skew(float x, float y) => new(1, MathF.Tan(y * MathF.PI), -MathF.Tan(x * MathF.PI), 1, 0, 0);

    // A color of the first palette with its alpha scaled, premultiplied, the text's color white.
    private Vector4 PaletteColor(int index, float alpha)
    {
        var (r, g, b, a) = index == 0xFFFF || index >= _entries
            ? ((byte)255, (byte)255, (byte)255, (byte)255)
            : (_data[_colorRecords + (_firstColor + index) * 4 + 2], _data[_colorRecords + (_firstColor + index) * 4 + 1],
               _data[_colorRecords + (_firstColor + index) * 4], _data[_colorRecords + (_firstColor + index) * 4 + 3]);
        var opacity = a / 255f * Math.Clamp(alpha, 0, 1);
        return new Vector4(r / 255f * opacity, g / 255f * opacity, b / 255f * opacity, opacity);
    }

    // Each pixel the mask lets through filled with the color a function of the point in the paint's
    // own space gives, times the mask.
    private static float[] Fill(Canvas canvas, float[]? mask, Matrix3x2 transform, Func<Vector2, Vector4> color)
    {
        var layer = new float[canvas.Width * canvas.Height * 4];
        if (!Matrix3x2.Invert(transform * canvas.FromUnits, out var toPaint)) return layer;
        for (int y = 0; y < canvas.Height; y++)
            for (int x = 0; x < canvas.Width; x++)
            {
                int i = y * canvas.Width + x;
                var cover = mask?[i] ?? 1;
                if (cover <= 0) continue;
                var c = color(Vector2.Transform(new Vector2(x + 0.5f, y + 0.5f), toPaint)) * cover;
                (layer[i * 4], layer[i * 4 + 1], layer[i * 4 + 2], layer[i * 4 + 3]) = (c.X, c.Y, c.Z, c.W);
            }
        return layer;
    }

    // A color line's stops in order, and how it extends past them: 0 pad, 1 repeat, 2 reflect.
    private (int Extend, (float Offset, Vector4 Color)[] Stops) ColorLine(int at, bool variable)
    {
        int extend = _data[at], count = U16(at + 1), size = variable ? 10 : 6;
        var stops = new (float, Vector4)[count];
        for (int i = 0; i < count; i++)
        {
            var stop = at + 3 + i * size;
            stops[i] = (F2Dot14(stop), PaletteColor(U16(stop + 2), F2Dot14(stop + 4)));
        }
        return (extend, [.. stops.OrderBy(s => s.Item1)]);
    }

    // The color a color line gives at t, between the stops either side of it, in premultiplied color.
    private static Vector4 Along((int Extend, (float Offset, Vector4 Color)[] Stops) line, float t)
    {
        var stops = line.Stops;
        if (stops.Length == 0) return Vector4.Zero;
        if (stops.Length == 1) return stops[0].Color;
        float first = stops[0].Offset, last = stops[^1].Offset, span = last - first;
        if (span > 1e-6f && line.Extend != 0)
        {
            var u = (t - first) / span;
            if (line.Extend == 1) u -= MathF.Floor(u);
            else
            {
                u = MathF.Abs(u) % 2;
                if (u > 1) u = 2 - u;
            }
            t = first + u * span;
        }
        if (t <= first) return stops[0].Color;
        if (t >= last) return stops[^1].Color;
        for (int i = 1; i < stops.Length; i++)
            if (t <= stops[i].Offset)
            {
                var (o0, c0) = stops[i - 1];
                var (o1, c1) = stops[i];
                return o1 - o0 < 1e-6f ? c1 : Vector4.Lerp(c0, c1, (t - o0) / (o1 - o0));
            }
        return stops[^1].Color;
    }

    // A linear gradient from p0 toward p1, its lines of one color parallel to p0 to p2, so its
    // direction is p1 brought onto the line through p0 square to p0 and p2.
    private float[] LinearGradient(int paint, bool variable, Matrix3x2 transform, float[]? mask, Canvas canvas)
    {
        var line = ColorLine(paint + U24(paint + 1), variable);
        Vector2 p0 = new(S16(paint + 4), S16(paint + 6)), p1 = new(S16(paint + 8), S16(paint + 10)), p2 = new(S16(paint + 12), S16(paint + 14));
        var square = new Vector2(-(p2 - p0).Y, (p2 - p0).X);
        var p3 = square.LengthSquared() < 1e-6f ? p1 : p0 + square * Vector2.Dot(p1 - p0, square) / square.LengthSquared();
        var along = p3 - p0;
        var length = along.LengthSquared();
        return Fill(canvas, mask, transform, p => length < 1e-6f ? line.Stops.LastOrDefault().Color : Along(line, Vector2.Dot(p - p0, along) / length));
    }

    // A radial gradient between two circles, each point colored by the largest t whose circle passes
    // through it with a radius not below zero, as a canvas's two-point conical gradient is.
    private float[] RadialGradient(int paint, bool variable, Matrix3x2 transform, float[]? mask, Canvas canvas)
    {
        var line = ColorLine(paint + U24(paint + 1), variable);
        Vector2 c0 = new(S16(paint + 4), S16(paint + 6)), c1 = new(S16(paint + 10), S16(paint + 12));
        float r0 = U16(paint + 8), r1 = U16(paint + 14);
        Vector2 cd = c1 - c0;
        float dr = r1 - r0, a = Vector2.Dot(cd, cd) - dr * dr;
        return Fill(canvas, mask, transform, p =>
        {
            var pd = p - c0;
            float b = Vector2.Dot(pd, cd) + r0 * dr, c = Vector2.Dot(pd, pd) - r0 * r0;
            float t;
            if (MathF.Abs(a) < 1e-6f)
            {
                if (MathF.Abs(b) < 1e-6f) return Vector4.Zero;
                t = c / (2 * b);
                if (r0 + t * dr < 0) return Vector4.Zero;
            }
            else
            {
                var discriminant = b * b - a * c;
                if (discriminant < 0) return Vector4.Zero;
                var root = MathF.Sqrt(discriminant);
                float t1 = (b + root) / a, t2 = (b - root) / a;
                (t1, t2) = (MathF.Max(t1, t2), MathF.Min(t1, t2));
                if (r0 + t1 * dr >= 0) t = t1;
                else if (r0 + t2 * dr >= 0) t = t2;
                else return Vector4.Zero;
            }
            return Along(line, t);
        });
    }

    // A sweep gradient around a center, from a start angle to an end one counterclockwise, each in
    // half turns.
    private float[] SweepGradient(int paint, bool variable, Matrix3x2 transform, float[]? mask, Canvas canvas)
    {
        var line = ColorLine(paint + U24(paint + 1), variable);
        Vector2 center = new(S16(paint + 4), S16(paint + 6));
        float start = F2Dot14(paint + 8) * 180, end = F2Dot14(paint + 10) * 180;
        return Fill(canvas, mask, transform, p =>
        {
            var angle = MathF.Atan2(p.Y - center.Y, p.X - center.X) * 180 / MathF.PI;
            if (angle < 0) angle += 360;
            return MathF.Abs(end - start) < 1e-6f ? Vector4.Zero : Along(line, (angle - start) / (end - start));
        });
    }

    // A source laid onto a backdrop by a composite mode, into the backdrop, both in premultiplied color.
    private static void Composite(float[] source, float[] backdrop, int mode)
    {
        for (int i = 0; i < backdrop.Length; i += 4)
        {
            float sa = source[i + 3], da = backdrop[i + 3];
            if (mode == 3 && sa <= 0) continue;
            float Porter(float s, float d) => mode switch
            {
                0 => 0,
                1 => s,
                2 => d,
                4 => d + s * (1 - da),
                5 => s * da,
                6 => d * sa,
                7 => s * (1 - da),
                8 => d * (1 - sa),
                9 => s * da + d * (1 - sa),
                10 => d * sa + s * (1 - da),
                11 => s * (1 - da) + d * (1 - sa),
                12 => MathF.Min(1, s + d),
                _ => s + d * (1 - sa),
            };
            if (mode is >= 13 and <= 23)
            {
                // A separable blend of the colors unpremultiplied, where both are, laid over.
                for (int c = 0; c < 3; c++)
                {
                    float cs = sa > 0 ? source[i + c] / sa : 0, cb = da > 0 ? backdrop[i + c] / da : 0;
                    backdrop[i + c] = source[i + c] * (1 - da) + backdrop[i + c] * (1 - sa) + sa * da * Blend(mode, cs, cb);
                }
                backdrop[i + 3] = sa + da - sa * da;
                continue;
            }
            for (int c = 0; c < 4; c++) backdrop[i + c] = Porter(source[i + c], backdrop[i + c]);
        }
    }

    // A separable blend mode of a source color over a backdrop color, each from 0 to 1.
    private static float Blend(int mode, float s, float b) => mode switch
    {
        13 => s + b - s * b,
        14 => b <= 0.5f ? 2 * s * b : 1 - 2 * (1 - s) * (1 - b),
        15 => MathF.Min(s, b),
        16 => MathF.Max(s, b),
        17 => b <= 0 ? 0 : s >= 1 ? 1 : MathF.Min(1, b / (1 - s)),
        18 => b >= 1 ? 1 : s <= 0 ? 0 : 1 - MathF.Min(1, (1 - b) / s),
        19 => s <= 0.5f ? 2 * s * b : 1 - 2 * (1 - s) * (1 - b),
        20 => s <= 0.5f ? b - (1 - 2 * s) * b * (1 - b) : b + (2 * s - 1) * ((b <= 0.25f ? ((16 * b - 12) * b + 4) * b : MathF.Sqrt(b)) - b),
        21 => MathF.Abs(s - b),
        22 => s + b - 2 * s * b,
        23 => s * b,
        _ => s,
    };

    // An outline's coverage on the canvas, its contours moved by a transform onto the pixels and cut
    // at the canvas's sides, the part past one laid along it so each row's area still sums to nothing.
    private float[] Coverage(int glyph, Matrix3x2 toPixels, int width, int height)
    {
        int stride = width + 2;
        var accumulated = new float[stride * height + 4];
        foreach (var (a, b) in _font.Segments(glyph))
            foreach (var (p, q) in ClipSides(Vector2.Transform(a, toPixels), Vector2.Transform(b, toPixels), width + 1))
                TrueTypeFont.Line(accumulated, stride, height, p, q);
        var coverage = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            float sum = 0;
            for (int x = 0; x < width; x++)
            {
                sum += accumulated[y * stride + x];
                coverage[y * width + x] = Math.Min(1f, MathF.Abs(sum));
            }
        }
        return coverage;
    }

    // A segment cut where it crosses x = 0 and x = right, the parts outside laid along those lines.
    private static IEnumerable<(Vector2, Vector2)> ClipSides(Vector2 a, Vector2 b, float right)
    {
        var points = new List<Vector2> { a };
        foreach (var side in new[] { 0f, right })
        {
            if ((a.X - side) * (b.X - side) >= 0 || a.X == b.X) continue;
            var t = (side - a.X) / (b.X - a.X);
            points.Add(Vector2.Lerp(a, b, t));
        }
        points.Add(b);
        // Along the segment, from a to b.
        var ordered = points.OrderBy(p => Vector2.DistanceSquared(p, a)).ToList();
        for (int i = 0; i + 1 < ordered.Count; i++)
        {
            var (p, q) = (ordered[i], ordered[i + 1]);
            yield return (p with { X = Math.Clamp(p.X, 0, right) }, q with { X = Math.Clamp(q.X, 0, right) });
        }
    }
}

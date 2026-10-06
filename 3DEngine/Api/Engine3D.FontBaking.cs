using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using StbImageSharp;

namespace Engine;

public static partial class Engine3D
{
    /// <summary>
    /// Bakes a font file's glyphs as a distance field at <paramref name="fontSize"/> pixels, with
    /// the characters in <paramref name="ranges"/> (pinned pairs ending in zero), or the Latin-1
    /// ones when it is zero.
    /// </summary>
    internal static unsafe (Image Field, Image Coverage, Dictionary<int, Glyph> Glyphs)? BakeDistanceField(string path, int fontSize, IntPtr ranges)
    {
        // Glyphs past 64 pixels have detail enough at a smaller factor, and a smaller atlas.
        var factor = fontSize <= 64 ? 4 : fontSize <= 128 ? 2 : 1;
        var config = ImGuiNative.ImFontConfig_ImFontConfig();
        try
        {
            // One texel of the bake to one pixel of the glyph, so a glyph's corners and its
            // texture coordinates grow by the same padding.
            config->OversampleH = 1;
            config->OversampleV = 1;
            var baked = BakeAtlas(atlas =>
            {
                // Room between glyphs for the distances on both sides of each.
                atlas.TexGlyphPadding = 2 * SdfPadding * factor;
                return atlas.AddFontFromFileTTF(path, fontSize * factor, new ImFontConfigPtr(config),
                    ranges == IntPtr.Zero ? atlas.GetGlyphRangesDefault() : ranges);
            });
            return baked is { } b ? DistanceFieldAtlas(b.Image, b.Glyphs, factor) : null;
        }
        finally
        {
            ImGuiNative.ImFontConfig_destroy(config);
        }
    }

    // How far past each glyph's edge, in pixels of the bake, its distances reach, which raylib's
    // padding of 4 and scale of 32 a pixel also give.
    internal const int SdfPadding = 4;

    /// <summary>
    /// Turns an atlas of glyphs baked <paramref name="factor"/> times too large into a distance
    /// field atlas at the size meant, and an atlas of coverage for drawing into images, with each
    /// glyph grown by <see cref="SdfPadding"/> pixels on every side to hold its distances.
    /// </summary>
    internal static (Image Field, Image Coverage, Dictionary<int, Glyph> Glyphs) DistanceFieldAtlas(Image baked, Dictionary<int, Glyph> glyphs, int factor)
    {
        var alpha = new byte[baked.Width * baked.Height];
        for (int i = 0; i < alpha.Length; i++) alpha[i] = baked.Data[i * 4 + 3];
        var distances = DistanceField.Shrink(DistanceField.Signed(alpha, baked.Width, baked.Height),
            baked.Width, baked.Height, factor, out var width, out var height);

        var field = new byte[width * height * 4];
        var coverage = new byte[width * height * 4];
        for (int i = 0; i < distances.Length; i++)
        {
            field.AsSpan(i * 4, 3).Fill(255);
            coverage.AsSpan(i * 4, 3).Fill(255);
            field[i * 4 + 3] = (byte)Math.Clamp(MathF.Round((0.5f + distances[i] / (2 * SdfPadding)) * 255), 0, 255);
            coverage[i * 4 + 3] = (byte)Math.Clamp(MathF.Round((0.5f + distances[i]) * 255), 0, 255);
        }

        // Texture coordinates stay fractions of the atlas, which the shrink rounded up to whole
        // texels, so they scale by what that rounding added.
        var (su, sv) = ((float)baked.Width / (width * factor), (float)baked.Height / (height * factor));
        var (pu, pv) = ((float)SdfPadding / width, (float)SdfPadding / height);
        var grown = new Dictionary<int, Glyph>(glyphs.Count);
        foreach (var (codepoint, g) in glyphs)
        {
            grown[codepoint] = g.X1 > g.X0 && g.Y1 > g.Y0
                ? new Glyph(g.X0 / factor - SdfPadding, g.Y0 / factor - SdfPadding, g.X1 / factor + SdfPadding, g.Y1 / factor + SdfPadding,
                    g.U0 * su - pu, g.V0 * sv - pv, g.U1 * su + pu, g.V1 * sv + pv, g.Advance / factor)
                : g with { X0 = g.X0 / factor, Y0 = g.Y0 / factor, X1 = g.X1 / factor, Y1 = g.Y1 / factor, Advance = g.Advance / factor };
        }
        return (new Image(field, width, height), new Image(coverage, width, height), grown);
    }

    // Bakes the atlas, then copies its pixels into a texture, so nothing of ImGui's is kept for the font.
    private static Font? Bake(Func<ImFontAtlasPtr, ImFontPtr> add, TextureFilter filter,
        Func<(Image Image, float Size, Dictionary<int, Glyph> Glyphs), (Image Image, float Size, Dictionary<int, Glyph> Glyphs)>? extend = null)
    {
        if (BakeAtlas(add) is not { } baked)
        {
            ApiLogger.Warn("A font could not be baked.");
            return null;
        }
        if (extend is not null) baked = extend(baked);

        var texture = LoadTextureFromImage(baked.Image);
        SetTextureFilter(texture, filter);
        return new Font(texture, baked.Size, baked.Size, baked.Glyphs, baked.Image);
    }

    /// <summary>
    /// An atlas with the characters past U+FFFF a font file has drawn into a strip below it, at the
    /// size and on the baseline the atlas builder puts the rest at, which takes the builder's
    /// scale, a pixel height of ascent to descent, and its ascent rounded up a pixel.
    /// </summary>
    private static (Image Image, float Size, Dictionary<int, Glyph> Glyphs) WithBeyondPlane(
        (Image Image, float Size, Dictionary<int, Glyph> Glyphs) baked, TrueTypeFont outlines, int size, int[] codepoints)
    {
        var scale = size / (float)(outlines.Ascent - outlines.Descent);
        var baseline = MathF.Round(MathF.Floor(outlines.Ascent * scale + 1));
        var drawn = new List<(int Codepoint, byte[] Alpha, int Width, int Height, int Left, int Top, float Advance)>();
        foreach (var codepoint in codepoints)
        {
            var glyph = outlines.GlyphIndex(codepoint);
            if (glyph == 0) continue;
            var advance = outlines.Advance(glyph) * scale;
            if (outlines.Rasterize(glyph, scale) is { } r) drawn.Add((codepoint, r.Alpha, r.Width, r.Height, r.Left, r.Top, advance));
            else drawn.Add((codepoint, [], 0, 0, 0, 0, advance));
        }
        if (drawn.Count == 0) return baked;

        // Packed left to right in rows under the atlas, a texel apart.
        var (atlas, width) = (baked.Image, baked.Image.Width);
        var places = new List<(int X, int Y)>();
        int x = 1, y = 1, row = 0;
        foreach (var g in drawn)
        {
            if (x + g.Width + 1 > width) (x, y, row) = (1, y + row + 1, 0);
            places.Add((x, y));
            x += g.Width + 1;
            row = Math.Max(row, g.Height);
        }
        int strip = y + row + 1, height = atlas.Height + strip;
        var pixels = new byte[width * height * 4];
        Array.Copy(atlas.Data, pixels, Math.Min(atlas.Data.Length, width * atlas.Height * 4));
        var glyphs = new Dictionary<int, Glyph>();
        foreach (var (codepoint, g) in baked.Glyphs)
            glyphs[codepoint] = g with { V0 = g.V0 * atlas.Height / height, V1 = g.V1 * atlas.Height / height };
        for (int i = 0; i < drawn.Count; i++)
        {
            var g = drawn[i];
            var (px, py) = (places[i].X, atlas.Height + places[i].Y);
            for (int gy = 0; gy < g.Height; gy++)
                for (int gx = 0; gx < g.Width; gx++)
                {
                    int at = ((py + gy) * width + px + gx) * 4;
                    (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (255, 255, 255, g.Alpha[gy * g.Width + gx]);
                }
            glyphs[g.Codepoint] = new Glyph(g.Left, baseline + g.Top, g.Left + g.Width, baseline + g.Top + g.Height,
                (float)px / width, (float)py / height, (float)(px + g.Width) / width, (float)(py + g.Height) / height, g.Advance);
        }
        return (new Image(pixels, width, height), baked.Size, glyphs);
    }

    /// <summary>Builds an atlas holding one font and returns its pixels, its size and its glyph table, freeing the atlas.</summary>
    internal static unsafe (Image Image, float Size, Dictionary<int, Glyph> Glyphs)? BakeAtlas(Func<ImFontAtlasPtr, ImFontPtr> add)
    {
        var atlas = new ImFontAtlasPtr(ImGuiNative.ImFontAtlas_ImFontAtlas());
        try
        {
            // ImGui's mouse cursors and lines, drawn into an atlas for its own windows, have no
            // place in a font's, which holds its glyphs as raylib's does.
            atlas.Flags |= ImFontAtlasFlags.NoMouseCursors | ImFontAtlasFlags.NoBakedLines;
            var imFont = add(atlas);
            if (imFont.NativePtr == null || !atlas.Build()) return null;

            atlas.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out _);
            var rgba = new byte[width * height * 4];
            Marshal.Copy(pixels, rgba, 0, rgba.Length);

            // Read in the C layout rather than through ImGui.NET's ImFontGlyph, which maps the
            // Colored:1, Visible:1, Codepoint:30 bitfield as three separate fields and so reads
            // every field after it from the wrong place. The C struct is one uint of bits, then
            // AdvanceX, X0, Y0, X1, Y1, U0, V0, U1, V1, 40 bytes in all.
            // ImGui gives every font a tab four spaces wide, which raylib's fonts have none of, so
            // it is left out and a tab is drawn and measured as a glyph the font lacks, the '?'.
            var glyphs = new Dictionary<int, Glyph>();
            var record = (byte*)imFont.Glyphs.Data;
            for (int i = 0; i < imFont.Glyphs.Size; i++, record += 40)
            {
                var bits = *(uint*)record;
                var f = (float*)(record + 4);
                if ((int)(bits >> 2) != '\t')
                    glyphs[(int)(bits >> 2)] = new Glyph(f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[0]);
            }
            return (new Image(rgba, width, height), imFont.FontSize, glyphs);
        }
        finally
        {
            ImGuiNative.ImFontAtlas_destroy(atlas.NativePtr);
        }
    }
}

using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace Engine;

/// <summary>One character of a <see cref="Font"/>: where it sits relative to the pen, where it is in the atlas, and how far it moves the pen.</summary>
public readonly record struct Glyph(float X0, float Y0, float X1, float Y1, float U0, float V0, float U1, float V1, float Advance);

/// <summary>A font baked into a texture of glyphs at one size.</summary>
/// <remarks>Drawing it at another size scales the glyphs, which blurs a bilinear atlas and blocks a point-filtered one.</remarks>
public sealed class Font
{
    internal Font(Texture2D texture, float baseSize, float lineHeight, Dictionary<int, Glyph> glyphs)
    {
        Texture = texture;
        BaseSize = baseSize;
        LineHeight = lineHeight;
        Glyphs = glyphs;
    }

    /// <summary>The atlas the glyphs are drawn from.</summary>
    public Texture2D Texture { get; }

    /// <summary>The size in pixels the glyphs were baked at.</summary>
    public float BaseSize { get; }

    /// <summary>The distance between lines at <see cref="BaseSize"/>.</summary>
    public float LineHeight { get; }

    /// <summary>The glyphs, by code point.</summary>
    public IReadOnlyDictionary<int, Glyph> Glyphs { get; }

    /// <summary>Whether the font has an atlas to draw from.</summary>
    public bool IsValid => Texture.IsValid && Glyphs.Count > 0;
}

public static partial class Engine3D
{
    private static Font? _defaultFont;
    private static readonly Dictionary<int, Font> DefaultFontSizes = [];

    // -- Fonts. Atlases are baked by Dear ImGui's font builder, which rasterizes TrueType through
    // stb_truetype and embeds a default font (ProggyClean), so fonts need no library of their own.

    // Frees the default font's atlases, which the engine baked for itself, so CloseWindow counts
    // only the textures the program left loaded.
    private static void ForgetDefaultFonts()
    {
        foreach (var font in DefaultFontSizes.Values)
            if (IsTextureValid(font.Texture)) UnloadTexture(font.Texture);
        DefaultFontSizes.Clear();
        if (_defaultFont is { } fallback && IsTextureValid(fallback.Texture)) UnloadTexture(fallback.Texture);
        _defaultFont = null;
    }

    /// <summary>The engine's default font: ProggyClean at 13 pixels, point filtered so it scales as pixels.</summary>
    public static Font GetFontDefault()
    {
        if (_defaultFont is { } font && IsTextureValid(font.Texture)) return font;
        return _defaultFont = Bake(atlas => atlas.AddFontDefault(), TextureFilter.Point) ?? throw new InvalidOperationException("The default font could not be baked.");
    }

    /// <summary>
    /// The default font baked at <paramref name="size"/> pixels, kept for later calls at the same size.
    /// <see cref="DrawText"/> draws with this, so text is rasterized at the size it is drawn rather
    /// than scaled from 13 pixels, which drops rows of a pixel font going down and blocks it going up.
    /// </summary>
    public static unsafe Font GetFontDefault(int size)
    {
        size = Math.Clamp(size, 4, 256);
        if (DefaultFontSizes.TryGetValue(size, out var font) && IsTextureValid(font.Texture)) return font;

        var config = ImGuiNative.ImFontConfig_ImFontConfig();
        try
        {
            config->SizePixels = size;
            config->OversampleH = 2;
            font = Bake(atlas => atlas.AddFontDefault(new ImFontConfigPtr(config)), TextureFilter.Bilinear) ?? GetFontDefault();
        }
        finally
        {
            ImGuiNative.ImFontConfig_destroy(config);
        }

        return DefaultFontSizes[size] = font;
    }

    /// <summary>Loads a TrueType or OpenType font at 32 pixels.</summary>
    public static Font LoadFont(string fileName) => LoadFontEx(fileName, 32);

    /// <summary>Loads a TrueType or OpenType font baked at <paramref name="fontSize"/> pixels, with the Latin-1 characters.</summary>
    /// <returns>The font, or the default font when the file cannot be read, with the reason in the log.</returns>
    public static Font LoadFontEx(string fileName, int fontSize)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadFontEx: '{fileName}' was not found beside the program or in the working directory. Using the default font.");
            return GetFontDefault();
        }

        return Bake(atlas => atlas.AddFontFromFileTTF(path, Math.Max(4, fontSize), null, atlas.GetGlyphRangesDefault()), TextureFilter.Bilinear)
               ?? GetFontDefault();
    }

    /// <summary>
    /// Loads a TrueType or OpenType font baked at <paramref name="fontSize"/> pixels, with exactly
    /// the characters in <paramref name="codepoints"/>, as raylib's does. Greek, Cyrillic, symbols
    /// and the rest of the Basic Multilingual Plane are reached this way.
    /// </summary>
    /// <remarks>
    /// Characters above U+FFFF, such as most emoji, are left out, because the atlas builder names
    /// characters in 16 bits. Characters the font file does not have are skipped when drawn.
    /// </remarks>
    /// <returns>The font, or the default font when the file cannot be read, with the reason in the log.</returns>
    public static unsafe Font LoadFontEx(string fileName, int fontSize, int[] codepoints)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadFontEx: '{fileName}' was not found beside the program or in the working directory. Using the default font.");
            return GetFontDefault();
        }

        var ranges = GlyphRanges(codepoints);
        if (ranges.Length == 1)
        {
            ApiLogger.Warn("LoadFontEx: no code points below U+10000 were given. Using the default font.");
            return GetFontDefault();
        }

        // The atlas reads the ranges when it builds, after AddFontFromFileTTF returns, so they stay
        // pinned until the bake is done.
        fixed (ushort* pinned = ranges)
        {
            var address = (IntPtr)pinned;
            return Bake(atlas => atlas.AddFontFromFileTTF(path, Math.Max(4, fontSize), null, address), TextureFilter.Bilinear)
                   ?? GetFontDefault();
        }
    }

    /// <summary>The distinct code points of <paramref name="text"/>, in order, for <see cref="LoadFontEx(string, int, int[])"/>.</summary>
    public static int[] LoadCodepoints(string text) => text.EnumerateRunes().Select(r => r.Value).Distinct().ToArray();

    /// <summary>
    /// Code points as the pairs of first and last that ImGui's atlas takes, ending in a zero, with
    /// neighbors merged into one pair and anything above U+FFFF or below one dropped.
    /// </summary>
    internal static ushort[] GlyphRanges(IEnumerable<int> codepoints)
    {
        var sorted = codepoints.Where(c => c is > 0 and <= 0xFFFF).Distinct().Order().ToArray();
        var ranges = new List<ushort>();
        for (int i = 0; i < sorted.Length; i++)
        {
            var first = sorted[i];
            while (i + 1 < sorted.Length && sorted[i + 1] == sorted[i] + 1) i++;
            ranges.Add((ushort)first);
            ranges.Add((ushort)sorted[i]);
        }
        ranges.Add(0);
        return [.. ranges];
    }

    /// <summary>Frees a font's atlas. The default fonts are kept.</summary>
    public static void UnloadFont(Font font)
    {
        if (!ReferenceEquals(font, _defaultFont) && !DefaultFontSizes.ContainsValue(font)) UnloadTexture(font.Texture);
    }

    /// <summary>Draws text with a font at <paramref name="position"/>, its top left corner, <paramref name="fontSize"/> pixels high, with <paramref name="spacing"/> pixels between characters.</summary>
    /// <remarks>A newline starts a new line. Characters the font has no glyph for are skipped.</remarks>
    public static void DrawTextEx(Font font, string text, Vector2 position, float fontSize, float spacing, Color tint)
    {
        if (!font.IsValid || string.IsNullOrEmpty(text)) return;

        var scale = fontSize / font.BaseSize;
        var pen = position;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                pen = new Vector2(position.X, pen.Y + font.LineHeight * scale);
                continue;
            }
            if (!font.Glyphs.TryGetValue(rune.Value, out var g)) continue;

            if (g.X1 > g.X0 && g.Y1 > g.Y0)
            {
                // Glyph corners are relative to the top of the line, so the text hangs from position.
                var (x0, y0, x1, y1) = (pen.X + g.X0 * scale, pen.Y + g.Y0 * scale, pen.X + g.X1 * scale, pen.Y + g.Y1 * scale);
                DrawList.TexturedQuad(new(x0, y0, 0), new(x1, y0, 0), new(x1, y1, 0), new(x0, y1, 0),
                    new(g.U0, g.V0), new(g.U1, g.V0), new(g.U1, g.V1), new(g.U0, g.V1), tint, font.Texture.Id);
            }
            pen.X += g.Advance * scale + spacing;
        }
    }

    /// <summary>The width and height <see cref="DrawTextEx"/> would draw <paramref name="text"/> at.</summary>
    public static Vector2 MeasureTextEx(Font font, string text, float fontSize, float spacing)
    {
        if (!font.IsValid || string.IsNullOrEmpty(text)) return Vector2.Zero;
        var scale = fontSize / font.BaseSize;
        float width = 0, line = 0;
        var lines = 1;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                width = Math.Max(width, line);
                line = 0;
                lines++;
                continue;
            }
            if (font.Glyphs.TryGetValue(rune.Value, out var g)) line += g.Advance * scale + spacing;
        }
        return new Vector2(Math.Max(width, line), lines * font.LineHeight * scale);
    }

    // Bakes the atlas, then copies its pixels into a texture, so nothing of ImGui's is kept for the font.
    private static Font? Bake(Func<ImFontAtlasPtr, ImFontPtr> add, TextureFilter filter)
    {
        if (BakeAtlas(add) is not { } baked)
        {
            ApiLogger.Warn("A font could not be baked.");
            return null;
        }

        var texture = LoadTextureFromImage(baked.Image);
        SetTextureFilter(texture, filter);
        return new Font(texture, baked.Size, baked.Size, baked.Glyphs);
    }

    /// <summary>Builds an atlas holding one font and returns its pixels, its size and its glyph table, freeing the atlas.</summary>
    internal static unsafe (Image Image, float Size, Dictionary<int, Glyph> Glyphs)? BakeAtlas(Func<ImFontAtlasPtr, ImFontPtr> add)
    {
        var atlas = new ImFontAtlasPtr(ImGuiNative.ImFontAtlas_ImFontAtlas());
        try
        {
            var imFont = add(atlas);
            if (imFont.NativePtr == null || !atlas.Build()) return null;

            atlas.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out _);
            var rgba = new byte[width * height * 4];
            Marshal.Copy(pixels, rgba, 0, rgba.Length);

            // Read in the C layout rather than through ImGui.NET's ImFontGlyph, which maps the
            // Colored:1, Visible:1, Codepoint:30 bitfield as three separate fields and so reads
            // every field after it from the wrong place. The C struct is one uint of bits, then
            // AdvanceX, X0, Y0, X1, Y1, U0, V0, U1, V1, 40 bytes in all.
            var glyphs = new Dictionary<int, Glyph>();
            var record = (byte*)imFont.Glyphs.Data;
            for (int i = 0; i < imFont.Glyphs.Size; i++, record += 40)
            {
                var bits = *(uint*)record;
                var f = (float*)(record + 4);
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

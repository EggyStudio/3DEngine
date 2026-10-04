using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace Engine;

/// <summary>One character of a <see cref="Font"/>: where it sits relative to the pen, where it is in the atlas, and how far it moves the pen.</summary>
public readonly record struct Glyph(float X0, float Y0, float X1, float Y1, float U0, float V0, float U1, float V1, float Advance);

/// <summary>How a font's glyphs are baked, as raylib's <c>FontType</c>.</summary>
public enum FontType
{
    /// <summary>Glyphs as coverage, smoothed at their edges.</summary>
    Default,

    /// <summary>Glyphs as coverage, which this engine bakes the same as <see cref="Default"/>.</summary>
    Bitmap,

    /// <summary>
    /// Glyphs as signed distance fields, which stay sharp drawn far larger than their bake.
    /// </summary>
    Sdf,
}

/// <summary>A font baked into a texture of glyphs at one size.</summary>
/// <remarks>
/// Drawing it at another size scales the glyphs, which blurs a bilinear atlas and blocks a
/// point-filtered one, unless the font was loaded as <see cref="FontType.Sdf"/>. A font loaded
/// from a file is baked again at a larger size it is drawn at, a quarter or more past its own, so
/// large text stays sharp, and that bake is kept for the next time.
/// </remarks>
public sealed class Font
{
    internal Font(Texture2D texture, float baseSize, float lineHeight, Dictionary<int, Glyph> glyphs, Image atlas = default, FontType type = FontType.Default)
    {
        Type = type;
        Atlas = atlas;
        Texture = texture;
        BaseSize = baseSize;
        LineHeight = lineHeight;
        Glyphs = glyphs;
    }

    /// <summary>The atlas the glyphs are drawn from.</summary>
    public Texture2D Texture { get; }

    // The atlas's pixels, kept for drawing text into an image on the CPU.
    internal Image Atlas { get; }

    /// <summary>The size in pixels the glyphs were baked at.</summary>
    public float BaseSize { get; }

    /// <summary>The distance between lines at <see cref="BaseSize"/>.</summary>
    public float LineHeight { get; }

    /// <summary>The glyphs, by code point.</summary>
    public IReadOnlyDictionary<int, Glyph> Glyphs { get; }

    /// <summary>How the glyphs were baked.</summary>
    /// <remarks>
    /// An <see cref="FontType.Sdf"/> atlas holds in its alpha the distance from each texel to the
    /// glyph's edge, 0.5 on the edge and an eighth more for each pixel of the bake inside it, as
    /// raylib's does, so raylib's <c>sdf.fs</c> reads it unchanged.
    /// </remarks>
    public FontType Type { get; }

    /// <summary>Whether the font has an atlas to draw from.</summary>
    public bool IsValid => Texture.IsValid && Glyphs.Count > 0;

    // Bakes the font's file again at a size, for a font loaded from one, and the bakes made, by size.
    internal Func<int, Font?>? Rebake { get; private set; }

    // The same font, able to bake itself again at larger sizes.
    internal Font WithRebake(Func<int, Font?> rebake)
    {
        Rebake = rebake;
        return this;
    }
    private readonly Dictionary<int, Font> _larger = [];

    // At most this many larger bakes are kept, past which the largest serves bigger text.
    private const int MaxBakes = 8;

    /// <summary>The bakes made at larger sizes, which unloading the font frees with it.</summary>
    internal IEnumerable<Font> Bakes => _larger.Values;

    /// <summary>
    /// The bake text <paramref name="fontSize"/> pixels high is drawn from: this font, or one baked
    /// from its file at the size, rounded up to four pixels, when that is a quarter or more past
    /// this one's.
    /// </summary>
    internal Font ForSize(float fontSize)
    {
        if (Rebake is null || Type == FontType.Sdf || fontSize < BaseSize * 1.25f) return this;
        var size = Math.Min(256, (int)MathF.Ceiling(fontSize / 4) * 4);
        if (_larger.TryGetValue(size, out var known)) return known;
        if (_larger.Count >= MaxBakes) return _larger.Values.MaxBy(f => f.BaseSize)!;
        return Rebake(size) is { IsValid: true } baked ? _larger[size] = baked : this;
    }
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
        // The shader went with the window's shader store.
        _sdfShader = default;
        _sdfShaderFailed = false;
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

        Font? BakeAt(int size) => Bake(atlas => atlas.AddFontFromFileTTF(path, Math.Max(4, size), null, atlas.GetGlyphRangesDefault()), TextureFilter.Bilinear);
        return BakeAt(fontSize) is { } font ? font.WithRebake(BakeAt) : GetFontDefault();
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
        Font? BakeAt(int size)
        {
            fixed (ushort* pinned = ranges)
            {
                var address = (IntPtr)pinned;
                return Bake(atlas => atlas.AddFontFromFileTTF(path, Math.Max(4, size), null, address), TextureFilter.Bilinear);
            }
        }
        return BakeAt(fontSize) is { } font ? font.WithRebake(BakeAt) : GetFontDefault();
    }

    /// <summary>
    /// Loads a TrueType or OpenType font baked at <paramref name="fontSize"/> pixels as
    /// <paramref name="type"/>, with the characters in <paramref name="codepoints"/>, or the
    /// Latin-1 ones when it is null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="FontType.Sdf"/> font is drawn by <see cref="DrawTextEx"/> through the engine's
    /// distance field shader, which keeps its edges a pixel wide at any size, so a font baked at 32
    /// pixels serves text from 16 to several hundred. Inside <see cref="BeginShaderMode"/> the
    /// program's shader draws it instead, as raylib's <c>sdf.fs</c> example does.
    /// </para>
    /// <para>
    /// The glyphs are rasterized at four times the size and their distances measured there, so
    /// thin strokes and corners keep their shape, and loading takes longer than a coverage bake.
    /// </para>
    /// </remarks>
    /// <returns>The font, or the default font when the file cannot be read, with the reason in the log.</returns>
    public static unsafe Font LoadFontEx(string fileName, int fontSize, int[]? codepoints, FontType type)
    {
        if (type != FontType.Sdf)
            return codepoints is null ? LoadFontEx(fileName, fontSize) : LoadFontEx(fileName, fontSize, codepoints);

        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadFontEx: '{fileName}' was not found beside the program or in the working directory. Using the default font.");
            return GetFontDefault();
        }

        var ranges = codepoints is null ? null : GlyphRanges(codepoints);
        if (ranges is { Length: 1 })
        {
            ApiLogger.Warn("LoadFontEx: no code points below U+10000 were given. Using the default font.");
            return GetFontDefault();
        }

        fontSize = Math.Max(4, fontSize);
        fixed (ushort* pinned = ranges)
        {
            if (BakeDistanceField(path, fontSize, ranges is null ? IntPtr.Zero : (IntPtr)pinned) is not { } baked)
            {
                ApiLogger.Warn("A font could not be baked.");
                return GetFontDefault();
            }

            var texture = LoadTextureFromImage(baked.Field);
            SetTextureFilter(texture, TextureFilter.Bilinear);
            return new Font(texture, fontSize, fontSize, baked.Glyphs, baked.Coverage, FontType.Sdf);
        }
    }

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

    /// <summary>Frees a font's atlas, and those it was baked into at larger sizes. The default fonts are kept.</summary>
    public static void UnloadFont(Font font)
    {
        if (ReferenceEquals(font, _defaultFont) || DefaultFontSizes.ContainsValue(font)) return;
        UnloadTexture(font.Texture);
        foreach (var bake in font.Bakes) UnloadTexture(bake.Texture);
    }

    /// <summary>Draws text with a font at <paramref name="position"/>, its top left corner, <paramref name="fontSize"/> pixels high, with <paramref name="spacing"/> pixels between characters.</summary>
    /// <remarks>A newline starts a new line. Characters the font has no glyph for are skipped.</remarks>
    public static void DrawTextEx(Font font, string text, Vector2 position, float fontSize, float spacing, Color tint) =>
        DrawTextPro(font, text, position, Vector2.Zero, 0, fontSize, spacing, tint);

    /// <summary>
    /// Draws text with a font as <see cref="DrawTextEx"/> does, rotated by <paramref name="rotation"/>
    /// degrees around <paramref name="origin"/>, which is relative to the text's top left corner and
    /// lands at <paramref name="position"/>.
    /// </summary>
    public static void DrawTextPro(Font font, string text, Vector2 position, Vector2 origin, float rotation, float fontSize, float spacing, Color tint)
    {
        if (!font.IsValid || string.IsNullOrEmpty(text)) return;
        font = font.ForSize(fontSize);

        // A distance field font is drawn through the engine's shader unless the program has one
        // of its own in place.
        var sdf = font.Type == FontType.Sdf && DrawList.Shader == 0 && SdfShader().IsValid;
        if (sdf) DrawList.SetShader(_sdfShader.Id, default);

        // Glyphs are laid out from the text's top left at zero, then moved to the origin and turned.
        var scale = fontSize / font.BaseSize;
        var turn = rotation == 0 ? Matrix3x2.Identity : Matrix3x2.CreateRotation(float.DegreesToRadians(rotation));
        Vector3 Corner(float x, float y) => rotation == 0
            ? new Vector3(x - origin.X + position.X, y - origin.Y + position.Y, 0)
            : new Vector3(Vector2.Transform(new Vector2(x, y) - origin, turn) + position, 0);
        var pen = Vector2.Zero;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                pen = new Vector2(0, pen.Y + font.LineHeight * scale);
                continue;
            }
            if (!font.Glyphs.TryGetValue(rune.Value, out var g)) continue;

            if (g.X1 > g.X0 && g.Y1 > g.Y0)
            {
                // Glyph corners are relative to the top of the line, so the text hangs from position.
                var (x0, y0, x1, y1) = (pen.X + g.X0 * scale, pen.Y + g.Y0 * scale, pen.X + g.X1 * scale, pen.Y + g.Y1 * scale);
                DrawList.TexturedQuad(Corner(x0, y0), Corner(x1, y0), Corner(x1, y1), Corner(x0, y1),
                    new(g.U0, g.V0), new(g.U1, g.V0), new(g.U1, g.V1), new(g.U0, g.V1), tint, font.Texture.Id);
            }
            pen.X += g.Advance * scale + spacing;
        }

        if (sdf) DrawList.SetShader(0, default);
    }

    private static Shader _sdfShader;
    private static bool _sdfShaderFailed;

    // The engine's distance field shader, compiled once per window from the staged sdf.slang. A
    // compiler missing or failing leaves fonts drawn from their distances as coverage, softer but
    // readable, and says so once.
    private static Shader SdfShader()
    {
        if (_sdfShader.IsValid || _sdfShaderFailed) return _sdfShader;
        var path = Path.Combine(AppContext.BaseDirectory, "source", "shaders", "sdf.slang");
        _sdfShader = File.Exists(path) ? LoadShaderFromMemory(File.ReadAllText(path), "sdf.slang") : default;
        if (!_sdfShader.IsValid)
        {
            _sdfShaderFailed = true;
            ApiLogger.Warn("DrawTextEx: the distance field shader is not available, so SDF fonts are drawn soft.");
        }
        return _sdfShader;
    }

    /// <summary>Draws text into an image in the default font, as <see cref="DrawText"/> draws it on the screen.</summary>
    public static void ImageDrawText(ref Image destination, string text, int x, int y, int fontSize, Color color) =>
        ImageDrawTextEx(ref destination, GetFontDefault(fontSize), text, new Vector2(x, y), fontSize, 0, color);

    /// <summary>
    /// Draws text into an image in a font, as <see cref="DrawTextEx"/> draws it on the screen, each
    /// glyph scaled from its bake by the nearest pixel and blended by its coverage.
    /// </summary>
    public static void ImageDrawTextEx(ref Image destination, Font font, string text, Vector2 position, float fontSize, float spacing, Color tint)
    {
        if (!font.IsValid || !font.Atlas.IsValid || string.IsNullOrEmpty(text)) return;
        font = font.ForSize(fontSize);

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
                var source = new Rectangle(g.U0 * font.Atlas.Width, g.V0 * font.Atlas.Height,
                    (g.U1 - g.U0) * font.Atlas.Width, (g.V1 - g.V0) * font.Atlas.Height);
                var target = new Rectangle(pen.X + g.X0 * scale, pen.Y + g.Y0 * scale, (g.X1 - g.X0) * scale, (g.Y1 - g.Y0) * scale);
                ImageDraw(ref destination, font.Atlas, source, target, tint);
            }
            pen.X += g.Advance * scale + spacing;
        }
    }

    /// <summary>A new image holding text in the default font, as large as the text, clear around it.</summary>
    public static Image ImageText(string text, int fontSize, Color color) =>
        ImageTextEx(GetFontDefault(fontSize), text, fontSize, 0, color);

    /// <summary>A new image holding text in a font, as large as the text, clear around it.</summary>
    public static Image ImageTextEx(Font font, string text, float fontSize, float spacing, Color tint)
    {
        var size = MeasureTextEx(font, text, fontSize, spacing);
        var image = GenImageColor(Math.Max(1, (int)MathF.Ceiling(size.X)), Math.Max(1, (int)MathF.Ceiling(size.Y)), Color.Blank);
        ImageDrawTextEx(ref image, font, text, Vector2.Zero, fontSize, spacing, tint);
        return image;
    }

    /// <summary>The width and height <see cref="DrawTextEx"/> would draw <paramref name="text"/> at.</summary>
    public static Vector2 MeasureTextEx(Font font, string text, float fontSize, float spacing)
    {
        if (!font.IsValid || string.IsNullOrEmpty(text)) return Vector2.Zero;
        // Measured in the bake it is drawn from, whose glyphs advance by their own whole pixels.
        font = font.ForSize(fontSize);
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
        return new Font(texture, baked.Size, baked.Size, baked.Glyphs, baked.Image);
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

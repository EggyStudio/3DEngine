using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using StbImageSharp;

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

    /// <summary>
    /// Loads a TrueType or OpenType font at 32 pixels, a font drawn as an image, a PNG whose
    /// glyphs are separated by magenta from the space on, or a BMFont <c>.fnt</c> file with the
    /// images of its pages beside it, as raylib's <c>LoadFont</c> reads each.
    /// </summary>
    /// <returns>The font, or the default font when the file cannot be read, with the reason in the log.</returns>
    public static Font LoadFont(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" or ".bmp" or ".tga" or ".gif" or ".jpg" => LoadFontFromImage(LoadImage(fileName), Color.Magenta, 32),
        ".fnt" => LoadBMFont(fileName),
        _ => LoadFontEx(fileName, 32),
    };

    // A font AngelCode's BMFont wrote in its text form: a line of the sizes common to every page, a
    // line naming each page's image, beside the file, and a line for each character, where it is in
    // its page and where it sits from the pen. The pages are stacked into one atlas, as raylib
    // stacks them, its height the line height, and a page of gray alone is the glyphs' coverage,
    // drawn white, as raylib reads one.
    private static Font LoadBMFont(string fileName)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadFont: '{fileName}' was not found beside the program or in the working directory. Using the default font.");
            return GetFontDefault();
        }
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ApiLogger.Warn($"LoadFont: '{fileName}' could not be read, {ex.Message}. Using the default font.");
            return GetFontDefault();
        }

        int lineHeight = 0, pageWidth = 0, pageHeight = 0, pageCount = 1;
        var pageFiles = new Dictionary<int, string>();
        var chars = new List<Dictionary<string, string>>();
        foreach (var line in lines)
        {
            var tag = line.Split(' ', 2)[0];
            var fields = BMFontFields(line);
            int Int(string key) => fields.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : 0;
            switch (tag)
            {
                case "common":
                    (lineHeight, pageWidth, pageHeight, pageCount) = (Int("lineHeight"), Int("scaleW"), Int("scaleH"), Math.Max(1, Int("pages")));
                    break;
                case "page" when fields.TryGetValue("file", out var file):
                    pageFiles[Int("id")] = file;
                    break;
                case "char":
                    chars.Add(fields);
                    break;
            }
        }
        if (lineHeight <= 0 || pageWidth <= 0 || pageHeight <= 0 || pageFiles.Count == 0)
        {
            ApiLogger.Warn($"LoadFont: '{fileName}' has no line height, page size or page in its BMFont lines. Using the default font.");
            return GetFontDefault();
        }

        var atlasData = new byte[pageWidth * pageHeight * pageCount * 4];
        var directory = Path.GetDirectoryName(path) ?? "";
        for (int page = 0; page < pageCount; page++)
        {
            if (!pageFiles.TryGetValue(page, out var file)) continue;
            ImageResult image;
            try
            {
                using var stream = File.OpenRead(Path.Combine(directory, file));
                image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                ApiLogger.Warn($"LoadFont: page '{file}' of '{fileName}' could not be read, {ex.Message}. Using the default font.");
                return GetFontDefault();
            }
            var gray = image.SourceComp is ColorComponents.Grey;
            for (int y = 0; y < Math.Min(image.Height, pageHeight); y++)
            for (int x = 0; x < Math.Min(image.Width, pageWidth); x++)
            {
                var from = (y * image.Width + x) * 4;
                var to = ((page * pageHeight + y) * pageWidth + x) * 4;
                if (gray)
                    (atlasData[to], atlasData[to + 1], atlasData[to + 2], atlasData[to + 3]) = (255, 255, 255, image.Data[from]);
                else
                    image.Data.AsSpan(from, 4).CopyTo(atlasData.AsSpan(to));
            }
        }

        float atlasWidth = pageWidth, atlasHeight = pageHeight * pageCount;
        var glyphs = new Dictionary<int, Glyph>();
        foreach (var c in chars)
        {
            int Int(string key) => c.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : 0;
            var (x, y, width, height) = (Int("x"), Int("y") + Int("page") * pageHeight, Int("width"), Int("height"));
            var (offsetX, offsetY) = (Int("xoffset"), Int("yoffset"));
            glyphs[Int("id")] = new Glyph(offsetX, offsetY, offsetX + width, offsetY + height,
                x / atlasWidth, y / atlasHeight, (x + width) / atlasWidth, (y + height) / atlasHeight, Int("xadvance"));
        }

        var atlas = new Image(atlasData, pageWidth, pageHeight * pageCount);
        return new Font(LoadTextureFromImage(atlas), lineHeight, lineHeight, glyphs, atlas);
    }

    // A BMFont line's key=value fields, a value in quotes where it may hold spaces.
    private static Dictionary<string, string> BMFontFields(string line)
    {
        var fields = new Dictionary<string, string>();
        foreach (System.Text.RegularExpressions.Match m in BMFontField().Matches(line))
            fields[m.Groups["key"].Value] = m.Groups["value"].Value.Trim('"');
        return fields;
    }

    [System.Text.RegularExpressions.GeneratedRegex("""(?<key>\w+)=(?<value>"[^"]*"|\S+)""")]
    private static partial System.Text.RegularExpressions.Regex BMFontField();

    /// <summary>
    /// Makes a font from an image of its glyphs, as a pixel-art game draws one, each glyph a run of
    /// pixels on a row, separated from the next and from the rows above and below by
    /// <paramref name="key"/>, the first glyph <paramref name="firstChar"/> and each after the next
    /// character. raylib's rule finds the gaps, the key's width before the first glyph being the space
    /// between glyphs and its height above the first row the space between rows.
    /// </summary>
    /// <remarks>The key's pixels become clear, and the atlas is point filtered, so the glyphs scale as pixels.</remarks>
    /// <returns>The font, or the default font when the image holds no glyphs, with the reason in the log.</returns>
    public static Font LoadFontFromImage(Image image, Color key, int firstChar)
    {
        if (!image.IsValid)
        {
            ApiLogger.Warn("LoadFontFromImage: the image is empty. Using the default font.");
            return GetFontDefault();
        }
        bool Key(int x, int y) => x >= image.Width || y >= image.Height || GetImageColor(image, x, y) == key;

        // The first pixel that is not the key gives the gaps: its column the space between glyphs,
        // its row the space between rows.
        int spacing = -1, lineSpacing = -1;
        for (int y = 0; y < image.Height && spacing < 0; y++)
            for (int x = 0; x < image.Width; x++)
                if (!Key(x, y))
                {
                    (spacing, lineSpacing) = (x, y);
                    break;
                }
        if (spacing < 0)
        {
            ApiLogger.Warn("LoadFontFromImage: every pixel is the key, so there are no glyphs. Using the default font.");
            return GetFontDefault();
        }
        var height = 0;
        while (!Key(spacing, lineSpacing + height)) height++;

        var glyphs = new Dictionary<int, Glyph>();
        var index = 0;
        for (var top = lineSpacing; top < image.Height; top += height + lineSpacing)
        {
            for (var x = spacing; x < image.Width && !Key(x, top);)
            {
                var width = 0;
                while (!Key(x + width, top)) width++;
                glyphs[firstChar + index++] = new Glyph(0, 0, width, height,
                    (float)x / image.Width, (float)top / image.Height, (float)(x + width) / image.Width, (float)(top + height) / image.Height, width);
                x += width + spacing;
            }
        }

        var atlas = ImageCopy(image);
        ImageColorReplace(ref atlas, key, Color.Blank);
        var texture = LoadTextureFromImage(atlas);
        SetTextureFilter(texture, TextureFilter.Point);
        return new Font(texture, height, height, glyphs, atlas);
    }

    // The path of a font file that is there and holds a font the atlas builder reads, or null with
    // the reason logged. The builder is native code that stops the whole process on a file that is
    // too short or whose tables run past its end, so a file is looked over before it is handed on.
    private static string? FontFile(string fileName, string caller)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"{caller}: '{fileName}' was not found beside the program or in the working directory. Using the default font.");
            return null;
        }
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ApiLogger.Warn($"{caller}: '{fileName}' could not be read, {ex.Message}. Using the default font.");
            return null;
        }
        if (FontProblem(data) is { } problem)
        {
            ApiLogger.Warn($"{caller}: '{fileName}' is not a font the engine reads, {problem}. Using the default font.");
            return null;
        }
        return path;
    }

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
        var tables = new HashSet<string>();
        for (int i = 0; i < count; i++)
        {
            int record = start + 12 + i * 16;
            var (offset, length) = (U32(data, record + 8), U32(data, record + 12));
            var tag = System.Text.Encoding.ASCII.GetString(data.Slice(record, 4));
            if ((ulong)offset + length > (ulong)data.Length) return $"its '{tag.TrimEnd()}' table running past its end";
            tables.Add(tag);
        }
        foreach (var needed in new[] { "cmap", "head", "hhea", "hmtx", "maxp" })
            if (!tables.Contains(needed)) return $"no '{needed}' table";
        if (!(tables.Contains("glyf") && tables.Contains("loca")) && !tables.Contains("CFF ") && !tables.Contains("CFF2"))
            return "no outlines, neither 'glyf' nor 'CFF '";
        return null;
    }

    /// <summary>Loads a TrueType or OpenType font baked at <paramref name="fontSize"/> pixels, with the Latin-1 characters.</summary>
    /// <returns>The font, or the default font when the file cannot be read, with the reason in the log.</returns>
    public static Font LoadFontEx(string fileName, int fontSize)
    {
        if (FontFile(fileName, "LoadFontEx") is not { } path) return GetFontDefault();

        Font? BakeAt(int size) => Bake(atlas => atlas.AddFontFromFileTTF(path, Math.Max(4, size), null, atlas.GetGlyphRangesDefault()), TextureFilter.Bilinear);
        return BakeAt(fontSize) is { } font ? font.WithRebake(BakeAt) : GetFontDefault();
    }

    /// <summary>
    /// Loads a TrueType or OpenType font baked at <paramref name="fontSize"/> pixels, with exactly
    /// the characters in <paramref name="codepoints"/>, as raylib's does. Greek, Cyrillic, symbols
    /// and the rest of the Basic Multilingual Plane are reached this way, and characters past U+FFFF,
    /// such as emoji and historic scripts.
    /// </summary>
    /// <remarks>
    /// The atlas builder names characters in 16 bits, so those past U+FFFF are drawn by the engine's
    /// own TrueType reader into a strip of the same atlas, at the same size and on the same
    /// baseline. They need the font's outlines, which a TrueType font has and an OpenType font of
    /// CFF outlines or a color emoji font of bitmaps does not. Characters the font file does not
    /// have are skipped when drawn.
    /// </remarks>
    /// <returns>The font, or the default font when the file cannot be read, with the reason in the log.</returns>
    public static unsafe Font LoadFontEx(string fileName, int fontSize, int[] codepoints)
    {
        if (FontFile(fileName, "LoadFontEx") is not { } path) return GetFontDefault();

        // Characters past U+FFFF, which the atlas builder cannot name, are drawn by the engine's own
        // TrueType reader into the same atlas. The builder bakes at least a space, so the font has
        // its line and baseline whatever was asked for.
        var beyond = codepoints.Where(c => c is > 0xFFFF and <= 0x10FFFF).Distinct().Order().ToArray();
        var ranges = GlyphRanges(codepoints);
        if (ranges.Length == 1 && beyond.Length > 0) ranges = GlyphRanges([' ']);
        if (ranges.Length == 1)
        {
            ApiLogger.Warn("LoadFontEx: no code points were given. Using the default font.");
            return GetFontDefault();
        }
        var outlines = beyond.Length > 0 ? TrueTypeFont.Read(File.ReadAllBytes(path)) : null;
        if (beyond.Length > 0 && outlines is null)
            ApiLogger.Warn($"LoadFontEx: '{fileName}' has no TrueType outlines to draw characters past U+FFFF from, so they are left out.");

        // The atlas reads the ranges when it builds, after AddFontFromFileTTF returns, so they stay
        // pinned until the bake is done.
        Font? BakeAt(int size)
        {
            fixed (ushort* pinned = ranges)
            {
                var address = (IntPtr)pinned;
                return Bake(atlas => atlas.AddFontFromFileTTF(path, Math.Max(4, size), null, address), TextureFilter.Bilinear,
                    outlines is null ? null : baked => WithBeyondPlane(baked, outlines, Math.Max(4, size), beyond));
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

        if (FontFile(fileName, "LoadFontEx") is not { } path) return GetFontDefault();

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

    /// <summary>The code points of <paramref name="text"/>, one for each character in order, for <see cref="LoadFontEx(string, int, int[])"/>.</summary>
    /// <remarks>A character that comes again is counted again, as raylib's are, and the font bakes it once.</remarks>
    public static int[] LoadCodepoints(string text) => text.EnumerateRunes().Select(r => r.Value).ToArray();

    // A character's glyph, or the font's '?' where it lacks the character, as raylib draws one.
    internal static bool TryGetGlyph(Font font, int codepoint, out Glyph glyph) =>
        font.Glyphs.TryGetValue(codepoint, out glyph) || font.Glyphs.TryGetValue('?', out glyph);

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
                pen = new Vector2(0, pen.Y + LineAdvance(font, fontSize));
                continue;
            }
            if (!TryGetGlyph(font, rune.Value, out var g)) continue;

            if (g.X1 > g.X0 && g.Y1 > g.Y0)
            {
                // Glyph corners are relative to the top of the line, so the text hangs from position.
                var (x0, y0, x1, y1) = (pen.X + g.X0 * scale, pen.Y + g.Y0 * scale, pen.X + g.X1 * scale, pen.Y + g.Y1 * scale);
                DrawList.TexturedQuad(Corner(x0, y0), Corner(x0, y1), Corner(x1, y1), Corner(x1, y0),
                    new(g.U0, g.V0), new(g.U0, g.V1), new(g.U1, g.V1), new(g.U1, g.V0), tint, font.Texture.Id);
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
    public static void ImageDrawText(ref Image dst, string text, int x, int y, int fontSize, Color color) =>
        ImageDrawTextEx(ref dst, GetFontDefault(fontSize), text, new Vector2(x, y), fontSize, 0, color);

    /// <summary>
    /// Draws text into an image in a font, as <see cref="DrawTextEx"/> draws it on the screen, each
    /// glyph scaled from its bake by the nearest pixel and blended by its coverage.
    /// </summary>
    public static void ImageDrawTextEx(ref Image dst, Font font, string text, Vector2 position, float fontSize, float spacing, Color tint)
    {
        if (!font.IsValid || !font.Atlas.IsValid || string.IsNullOrEmpty(text)) return;
        font = font.ForSize(fontSize);

        var scale = fontSize / font.BaseSize;
        var pen = position;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                pen = new Vector2(position.X, pen.Y + LineAdvance(font, fontSize));
                continue;
            }
            if (!TryGetGlyph(font, rune.Value, out var g)) continue;
            if (g.X1 > g.X0 && g.Y1 > g.Y0)
            {
                var source = new Rectangle(g.U0 * font.Atlas.Width, g.V0 * font.Atlas.Height,
                    (g.U1 - g.U0) * font.Atlas.Width, (g.V1 - g.V0) * font.Atlas.Height);
                var target = new Rectangle(pen.X + g.X0 * scale, pen.Y + g.Y0 * scale, (g.X1 - g.X0) * scale, (g.Y1 - g.Y0) * scale);
                ImageDraw(ref dst, font.Atlas, source, target, tint);
            }
            pen.X += g.Advance * scale + spacing;
        }
    }

    // The pixels between lines raylib's SetTextLineSpacing set, and the app it was set in, so a
    // window opened afterward starts with each font's own line height.
    private static (App? App, int Spacing)? _textLineSpacing;

    /// <summary>
    /// Sets the pixels between the tops of a text's lines to its size and this much more, for text
    /// drawn and measured after it, in place of each font's own line height.
    /// </summary>
    public static void SetTextLineSpacing(int spacing) => _textLineSpacing = (_app, spacing);

    // How far down a newline moves, at a size: the size and the set spacing, or the font's own height.
    private static float LineAdvance(Font font, float fontSize) =>
        _textLineSpacing is { } set && ReferenceEquals(set.App, _app) ? fontSize + set.Spacing : font.LineHeight * (fontSize / font.BaseSize);

    /// <summary>Draws one character, by its code point, in a font.</summary>
    public static void DrawTextCodepoint(Font font, int codepoint, Vector2 position, float fontSize, Color tint)
    {
        if (System.Text.Rune.IsValid(codepoint)) DrawTextEx(font, char.ConvertFromUtf32(codepoint), position, fontSize, 0, tint);
    }

    /// <summary>Draws characters by their code points in a font, as <see cref="DrawTextEx"/> draws a string.</summary>
    public static void DrawTextCodepoints(Font font, int[] codepoints, Vector2 position, float fontSize, float spacing, Color tint) =>
        DrawTextEx(font, string.Concat(codepoints.Where(System.Text.Rune.IsValid).Select(char.ConvertFromUtf32)), position, fontSize, spacing, tint);

    /// <summary>A character's glyph in a font, where it sits and how far it advances, the font's '?' where it lacks the character, as raylib's is, or null when it lacks both.</summary>
    public static Glyph? GetGlyphInfo(Font font, int codepoint) => TryGetGlyph(font, codepoint, out var glyph) ? glyph : null;

    /// <summary>Where a character's glyph lies in the font's atlas, in pixels, the '?' glyph's where the font lacks the character, or an empty rectangle when it lacks both.</summary>
    public static Rectangle GetGlyphAtlasRec(Font font, int codepoint)
    {
        if (!TryGetGlyph(font, codepoint, out var g)) return default;
        var (w, h) = (font.Texture.Width, font.Texture.Height);
        return new Rectangle(g.U0 * w, g.V0 * h, (g.U1 - g.U0) * w, (g.V1 - g.V0) * h);
    }

    /// <summary>
    /// Loads a TrueType or OpenType font from a file already in memory, by its type, as
    /// <c>".ttf"</c>, baked at <paramref name="fontSize"/> pixels with the characters in
    /// <paramref name="codepoints"/>, or the Latin-1 ones when it is null.
    /// </summary>
    /// <remarks>The bytes are kept with the font, so it is baked again at the larger sizes it is drawn at, as one from a file is.</remarks>
    /// <returns>The font, or the default font when the bytes cannot be read, with the reason in the log.</returns>
    public static unsafe Font LoadFontFromMemory(string fileType, byte[] fileData, int fontSize, int[]? codepoints)
    {
        if (!fileType.TrimStart('.').ToLowerInvariant().Equals("ttf") && !fileType.TrimStart('.').ToLowerInvariant().Equals("otf"))
        {
            ApiLogger.Warn($"LoadFontFromMemory: '{fileType}' is not a font type the engine reads (TTF, OTF). Using the default font.");
            return GetFontDefault();
        }
        if (FontProblem(fileData) is { } problem)
        {
            ApiLogger.Warn($"LoadFontFromMemory: the {fileData.Length} bytes are not a font the engine reads, {problem}. Using the default font.");
            return GetFontDefault();
        }
        var ranges = codepoints is null ? null : GlyphRanges(codepoints);
        if (ranges is { Length: 1 })
        {
            ApiLogger.Warn("LoadFontFromMemory: no code points below U+10000 were given. Using the default font.");
            return GetFontDefault();
        }

        // The atlas reads the bytes and ranges when it builds, after AddFontFromMemoryTTF returns,
        // so both stay pinned until the bake is done, and the atlas is told the bytes are not its
        // own to free.
        Font? BakeAt(int size)
        {
            var config = ImGuiNative.ImFontConfig_ImFontConfig();
            try
            {
                config->FontDataOwnedByAtlas = 0;
                fixed (byte* data = fileData)
                fixed (ushort* pinned = ranges)
                {
                    var (address, length) = ((IntPtr)data, fileData.Length);
                    var glyphs = (IntPtr)pinned;
                    return Bake(atlas => atlas.AddFontFromMemoryTTF(address, length, Math.Max(4, size), new ImFontConfigPtr(config),
                        glyphs == IntPtr.Zero ? atlas.GetGlyphRangesDefault() : glyphs), TextureFilter.Bilinear);
                }
            }
            finally
            {
                ImGuiNative.ImFontConfig_destroy(config);
            }
        }
        return BakeAt(fontSize) is { } font ? font.WithRebake(BakeAt) : GetFontDefault();
    }

    /// <summary>A new image holding text in the default font, as large as the text, clear around it.</summary>
    /// <remarks>A size below 10 is drawn at 10, as <see cref="DrawText"/> draws it.</remarks>
    public static Image ImageText(string text, int fontSize, Color color)
    {
        fontSize = DefaultTextSize(fontSize);
        return ImageTextEx(GetFontDefault(fontSize), text, fontSize, 0, color);
    }

    /// <summary>A new image holding text in a font, as large as the text, clear around it.</summary>
    public static Image ImageTextEx(Font font, string text, float fontSize, float spacing, Color tint)
    {
        var size = MeasureTextEx(font, text, fontSize, spacing);
        var image = GenImageColor(Math.Max(1, (int)MathF.Ceiling(size.X)), Math.Max(1, (int)MathF.Ceiling(size.Y)), Color.Blank);
        ImageDrawTextEx(ref image, font, text, Vector2.Zero, fontSize, spacing, tint);
        return image;
    }

    /// <summary>Whether a font has an atlas and glyphs, as one loaded does.</summary>
    public static bool IsFontValid(Font font) => font.IsValid;

    /// <summary>The size of a line of characters given as codepoints, as <see cref="MeasureTextEx"/> measures text.</summary>
    public static Vector2 MeasureTextCodepoints(Font font, int[] codepoints, float fontSize, float spacing) =>
        MeasureTextEx(font, string.Concat(codepoints.Select(c => char.ConvertFromUtf32(Math.Clamp(c, 0, 0x10FFFF) is >= 0xD800 and <= 0xDFFF ? '?' : c))), fontSize, spacing);

    /// <summary>The width and height <see cref="DrawTextEx"/> would draw <paramref name="text"/> at.</summary>
    public static Vector2 MeasureTextEx(Font font, string text, float fontSize, float spacing)
    {
        if (!font.IsValid || string.IsNullOrEmpty(text)) return Vector2.Zero;
        // Measured in the bake it is drawn from, whose glyphs advance by their own whole pixels.
        font = font.ForSize(fontSize);
        var scale = fontSize / font.BaseSize;
        // As raylib measures it, the widest line's glyphs and the spacing between the characters
        // of the line with the most, one fewer than it has, which the last character is not
        // followed by.
        float width = 0, line = 0;
        int lines = 1, characters = 0, most = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                width = Math.Max(width, line);
                line = 0;
                characters = 0;
                lines++;
                continue;
            }
            most = Math.Max(most, ++characters);
            if (TryGetGlyph(font, rune.Value, out var g)) line += g.Advance;
        }
        return new Vector2(Math.Max(width, line) * scale + (most - 1) * spacing, (lines - 1) * LineAdvance(font, fontSize) + font.LineHeight * scale);
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

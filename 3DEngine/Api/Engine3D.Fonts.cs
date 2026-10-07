using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using StbImageSharp;

namespace Engine;

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

    // Whether a font file holds glyphs in color, bitmaps (CBDT) or layers (COLR), by its table
    // directory alone.
    private static bool HasColorTables(string path)
    {
        using var file = File.OpenRead(path);
        Span<byte> head = stackalloc byte[12];
        if (file.ReadAtLeast(head, 12, throwOnEndOfStream: false) < 12) return false;
        int count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(head[4..]);
        var directory = new byte[count * 16];
        if (file.ReadAtLeast(directory, directory.Length, throwOnEndOfStream: false) < directory.Length) return false;
        for (int i = 0; i < count; i++)
            if (System.Text.Encoding.ASCII.GetString(directory, i * 16, 4) is "CBDT" or "COLR") return true;
        return false;
    }

    // A font baked at fontSize with the characters asked for: those past U+FFFF, which the atlas
    // builder cannot name, and those a color font holds in color, which it would bake in gray,
    // drawn by the engine's own TrueType reader from data into the same atlas, and the rest by the
    // builder through add, given a size and the pinned ranges. The builder bakes at least a space,
    // so the font has its line and baseline whatever was asked for, and a font of color bitmaps
    // alone, which it cannot read, is baked by the reader whole. Null when nothing could be baked.
    private static unsafe Font? LoadAsked(string caller, string name, byte[] data, int fontSize, int[] codepoints,
        Func<int, IntPtr, Func<ImFontAtlasPtr, ImFontPtr>> add)
    {
        var asked = codepoints.Where(c => c is > 0 and <= 0x10FFFF).Distinct().Order().ToArray();
        if (asked.Length == 0)
        {
            ApiLogger.Warn($"{caller}: no code points were given. Using the default font.");
            return null;
        }
        var outlines = TrueTypeFont.Read(data);
        var own = outlines is null ? []
            : outlines.HasOutlines ? [.. asked.Where(c => c > 0xFFFF || outlines.HasColor(outlines.GlyphIndex(c)))]
            : asked;
        if (asked.Any(c => c > 0xFFFF) && outlines is null)
            ApiLogger.Warn($"{caller}: '{name}' has no TrueType outlines to draw characters past U+FFFF from, so they are left out.");
        // The reader and the bytes it holds are kept with the font only where it draws some of it.
        if (own.Length == 0) outlines = null;
        var ranges = GlyphRanges(asked.Except(own));
        if (ranges.Length == 1) ranges = GlyphRanges([' ']);

        // The glyphs the font joins the characters asked for into, as an emoji font joins a family or
        // a flag, drawn by the reader too, and text drawn in the font shaped into them.
        var joined = outlines is null ? [] : JoinedGlyphs(outlines, asked);
        (int Key, int Glyph)[] wanted = outlines is null ? []
            : [.. own.Select(c => (c, outlines.GlyphIndex(c))), .. joined.Select(g => (JoinedKey(g), g))];
        (TrueTypeFont, HashSet<int>)? joining = joined.Length > 0 ? (outlines!, own.ToHashSet()) : null;

        // The atlas builder stops the program on a font in which it finds none of the characters it
        // is given, so a font whose reader draws every character asked for and which has no space
        // is baked by the reader alone, as a font of color bitmaps is.
        var builderFinds = outlines is null || asked.Except(own).Append(' ').Any(c => outlines.GlyphIndex(c) != 0);

        // The atlas reads the ranges when it builds, after the font is added, so they stay pinned
        // until the bake is done.
        Font? BakeAt(int size)
        {
            if (outlines is { HasOutlines: false } || outlines is not null && !builderFinds)
                return BakeOwn(Math.Max(4, size), TextureFilter.Bilinear, baked => WithBeyondPlane(baked, outlines, Math.Max(4, size), wanted))?.WithJoining(joining);
            fixed (ushort* pinned = ranges)
                return Bake(add(Math.Max(4, size), (IntPtr)pinned), TextureFilter.Bilinear,
                    outlines is null ? null : baked => WithBeyondPlane(baked, outlines, Math.Max(4, size), wanted))?.WithJoining(joining);
        }
        return BakeAt(fontSize) is { } font ? font.WithWholeAdvances().WithRebake(BakeAt) : null;
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
        if (!(tables.Contains("glyf") && tables.Contains("loca")) && !tables.Contains("CFF ") && !tables.Contains("CFF2")
            && !(tables.Contains("CBDT") && tables.Contains("CBLC")))
            return "no outlines, neither 'glyf' nor 'CFF ', and no color bitmaps";
        return null;
    }

    /// <summary>Loads a TrueType or OpenType font baked at <paramref name="fontSize"/> pixels, with the Latin-1 characters.</summary>
    /// <returns>The font, or the default font when the file cannot be read, with the reason in the log.</returns>
    public static Font LoadFontEx(string fileName, int fontSize)
    {
        if (FontFile(fileName, "LoadFontEx") is not { } path) return GetFontDefault();
        // A color font's Latin-1, as many of its characters as are colored drawn by the engine's
        // own reader, which the code points' overload does.
        if (HasColorTables(path)) return LoadFontEx(fileName, fontSize, [.. Enumerable.Range(0x20, 0xE0)]);

        Font? BakeAt(int size) => Bake(atlas => atlas.AddFontFromFileTTF(path, Math.Max(4, size), null, atlas.GetGlyphRangesDefault()), TextureFilter.Bilinear);
        return BakeAt(fontSize) is { } font ? font.WithWholeAdvances().WithRebake(BakeAt) : GetFontDefault();
    }

    /// <summary>
    /// Loads a TrueType or OpenType font baked at <paramref name="fontSize"/> pixels, with exactly
    /// the characters in <paramref name="codepoints"/>, as raylib's does. Greek, Cyrillic, symbols
    /// and the rest of the Basic Multilingual Plane are reached this way, and characters past U+FFFF,
    /// such as emoji and historic scripts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The atlas builder names characters in 16 bits, so those past U+FFFF are drawn by the engine's
    /// own TrueType reader into a strip of the same atlas, at the same size and on the same
    /// baseline. They need the font's outlines, which a TrueType font has and an OpenType font of
    /// CFF outlines does not. Characters the font file does not have are skipped when drawn.
    /// </para>
    /// <para>
    /// A color font's colored characters, emoji, are drawn by the reader in their colors, whichever
    /// plane they are in: from its bitmaps, as Noto Color Emoji and Twemoji hold them, scaled from
    /// the size nearest above, or from its layers, as Segoe UI Emoji holds them. A font of bitmaps
    /// alone is baked by the reader whole. Text drawn in white shows them as they are, and another
    /// color tints them. Each is one character, so a sequence a font joins into one picture, a
    /// family, a flag or a skin tone, is drawn as its characters apart, and the gradients of a
    /// COLR version 1 font are not read.
    /// </para>
    /// </remarks>
    /// <returns>The font, or the default font when the file cannot be read, with the reason in the log.</returns>
    public static unsafe Font LoadFontEx(string fileName, int fontSize, int[] codepoints)
    {
        if (FontFile(fileName, "LoadFontEx") is not { } path) return GetFontDefault();
        return LoadAsked("LoadFontEx", fileName, File.ReadAllBytes(path), fontSize, codepoints,
            (size, ranges) => atlas => atlas.AddFontFromFileTTF(path, size, null, ranges)) ?? GetFontDefault();
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
            return new Font(texture, fontSize, fontSize, baked.Glyphs, baked.Coverage, FontType.Sdf).WithWholeAdvances();
        }
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
        foreach (var key in TextKeys(font, text))
        {
            if (key == '\n')
            {
                pen = new Vector2(0, pen.Y + LineAdvance(font, fontSize));
                continue;
            }
            if (!TryGetGlyph(font, key, out var g)) continue;

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
        foreach (var key in TextKeys(font, text))
        {
            if (key == '\n')
            {
                pen = new Vector2(position.X, pen.Y + LineAdvance(font, fontSize));
                continue;
            }
            if (!TryGetGlyph(font, key, out var g)) continue;
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
    /// <remarks>
    /// The bytes are kept with the font, so it is baked again at the larger sizes it is drawn at, as
    /// one from a file is, and characters past U+FFFF and those a color font holds in color are
    /// drawn as <see cref="LoadFontEx(string, int, int[])"/> draws them.
    /// </remarks>
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
        // The atlas is told the bytes are not its own to free, and they stay pinned with the ranges
        // until the bake is done, since it reads both as it builds.
        return LoadAsked("LoadFontFromMemory", $"{fileData.Length} bytes", fileData, fontSize, codepoints ?? [.. Enumerable.Range(0x20, 0xE0)],
            (size, ranges) => atlas =>
            {
                var config = ImGuiNative.ImFontConfig_ImFontConfig();
                try
                {
                    config->FontDataOwnedByAtlas = 0;
                    fixed (byte* bytes = fileData)
                        return atlas.AddFontFromMemoryTTF((IntPtr)bytes, fileData.Length, size, new ImFontConfigPtr(config), ranges);
                }
                finally
                {
                    ImGuiNative.ImFontConfig_destroy(config);
                }
            }) ?? GetFontDefault();
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
        foreach (var key in TextKeys(font, text))
        {
            if (key == '\n')
            {
                width = Math.Max(width, line);
                line = 0;
                characters = 0;
                lines++;
                continue;
            }
            most = Math.Max(most, ++characters);
            if (TryGetGlyph(font, key, out var g)) line += g.Advance;
        }
        return new Vector2(Math.Max(width, line) * scale + (most - 1) * spacing, (lines - 1) * LineAdvance(font, fontSize) + font.LineHeight * scale);
    }
}

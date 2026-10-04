using System.Numerics;
using StbImageSharp;

namespace Engine;

/// <summary>Pixels in memory, four bytes each (red, green, blue, alpha), rows from the top.</summary>
/// <remarks>An image lives in managed memory and is collected like any array, so <see cref="Engine3D.UnloadImage"/> does nothing.</remarks>
public readonly record struct Image(byte[] Data, int Width, int Height)
{
    /// <summary>Whether the image holds pixels.</summary>
    public bool IsValid => Data is { Length: > 0 } && Width > 0 && Height > 0;
}

/// <summary>A texture on the GPU, by its id in the <see cref="TextureStore"/>, with its size.</summary>
/// <remarks>A default texture has id 0 and is not loaded. Drawing it draws nothing.</remarks>
public readonly record struct Texture2D(int Id, int Width, int Height)
{
    /// <summary>Whether this names a texture that was loaded.</summary>
    public bool IsValid => Id > 0;

    /// <summary>How many mip levels the texture has: one until <see cref="Engine3D.GenTextureMipmaps"/> makes the rest.</summary>
    public int Mipmaps { get; init; } = 1;
}

/// <summary>An image drawing can be sent to with <see cref="Engine3D.BeginTextureMode"/>, and drawn afterward through <see cref="Texture"/>.</summary>
public readonly record struct RenderTexture2D(Texture2D Texture)
{
    /// <summary>Whether this names a render texture that was loaded.</summary>
    public bool IsValid => Texture.IsValid;
}

public static partial class Engine3D
{
    private static readonly ILogger ApiLogger = Log.Category("Engine.Api");

    private static TextureStore Textures => Res<TextureStore>();

    // -- Images

    /// <summary>Loads an image file (PNG, JPEG, BMP, TGA, PSD, GIF, HDR) into memory.</summary>
    /// <returns>The image, or an invalid one when the file cannot be read, with the reason in the log.</returns>
    public static Image LoadImage(string fileName)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadImage: '{fileName}' was not found beside the program or in the working directory.");
            return default;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var result = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            return new Image(result.Data, result.Width, result.Height);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            ApiLogger.Warn($"LoadImage: '{fileName}' could not be decoded: {ex.Message}");
            return default;
        }
    }

    /// <summary>Makes an image of one color.</summary>
    public static Image GenImageColor(int width, int height, Color color)
    {
        var data = new byte[width * height * 4];
        for (int i = 0; i < data.Length; i += 4)
        {
            data[i] = color.R;
            data[i + 1] = color.G;
            data[i + 2] = color.B;
            data[i + 3] = color.A;
        }
        return new Image(data, width, height);
    }

    /// <summary>Makes a checkerboard of squares <paramref name="checksX"/> by <paramref name="checksY"/> pixels.</summary>
    public static Image GenImageChecked(int width, int height, int checksX, int checksY, Color first, Color second)
    {
        var data = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var c = ((x / Math.Max(1, checksX)) + (y / Math.Max(1, checksY))) % 2 == 0 ? first : second;
            var i = (y * width + x) * 4;
            data[i] = c.R;
            data[i + 1] = c.G;
            data[i + 2] = c.B;
            data[i + 3] = c.A;
        }
        return new Image(data, width, height);
    }

    /// <summary>The color of one pixel.</summary>
    public static Color GetImageColor(Image image, int x, int y)
    {
        var i = (y * image.Width + x) * 4;
        return new Color(image.Data[i], image.Data[i + 1], image.Data[i + 2], image.Data[i + 3]);
    }

    /// <summary>Does nothing, because an image is managed memory. Kept so raylib programs read the same.</summary>
    public static void UnloadImage(Image image) { }

    // -- Textures

    /// <summary>Loads an image file into a texture.</summary>
    /// <returns>The texture, or an invalid one when the file cannot be read, with the reason in the log.</returns>
    public static Texture2D LoadTexture(string fileName)
    {
        var image = LoadImage(fileName);
        return image.IsValid ? LoadTextureFromImage(image) : default;
    }

    /// <summary>Uploads an image into a texture. The image may be changed or dropped afterward.</summary>
    public static Texture2D LoadTextureFromImage(Image image)
    {
        if (!image.IsValid) return default;
        var id = Textures.Add((byte[])image.Data.Clone(), image.Width, image.Height);
        return new Texture2D(id, image.Width, image.Height);
    }

    /// <summary>Frees a texture. Drawing it afterward draws nothing.</summary>
    public static void UnloadTexture(Texture2D texture)
    {
        if (texture.IsValid) Textures.Remove(texture.Id);
    }

    /// <summary>Whether <paramref name="texture"/> is loaded.</summary>
    public static bool IsTextureValid(Texture2D texture) => texture.IsValid && Textures.Contains(texture.Id);

    /// <summary>Replaces a texture's pixels with an image of the same size.</summary>
    /// <returns>Whether the texture is loaded and the sizes matched.</returns>
    public static bool UpdateTexture(Texture2D texture, Image image) =>
        texture.IsValid && image.Width == texture.Width && image.Height == texture.Height &&
        Textures.Update(texture.Id, (byte[])image.Data.Clone());

    /// <summary>
    /// Gives a texture mip levels, each half the size of the one before, down to one pixel, so it
    /// stays smooth instead of shimmering when drawn smaller than its size.
    /// </summary>
    /// <remarks>
    /// The levels are made on the GPU from the texture's pixels. They are kept when the texture is
    /// updated or its filter changes. A render target cannot have them.
    /// </remarks>
    public static void GenTextureMipmaps(ref Texture2D texture)
    {
        if (texture.IsValid && Textures.GenerateMipmaps(texture.Id))
            texture = texture with { Mipmaps = (int)ImageDesc.FullMipChain((uint)texture.Width, (uint)texture.Height) };
    }

    /// <summary>Sets how a texture is sampled between its pixels. Textures load bilinear.</summary>
    public static void SetTextureFilter(Texture2D texture, TextureFilter filter)
    {
        if (texture.IsValid) Textures.SetFilter(texture.Id, filter);
    }

    // -- Drawing textures

    /// <summary>Draws a texture with its top left corner at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void DrawTexture(Texture2D texture, int x, int y, Color tint) =>
        DrawTextureEx(texture, new Vector2(x, y), 0, 1, tint);

    /// <summary>Draws a texture with its top left corner at <paramref name="position"/>.</summary>
    public static void DrawTextureV(Texture2D texture, Vector2 position, Color tint) =>
        DrawTextureEx(texture, position, 0, 1, tint);

    /// <summary>Draws a texture scaled, and rotated by <paramref name="rotation"/> degrees around its top left corner.</summary>
    public static void DrawTextureEx(Texture2D texture, Vector2 position, float rotation, float scale, Color tint) =>
        DrawTexturePro(texture, new Rectangle(0, 0, texture.Width, texture.Height),
            new Rectangle(position.X, position.Y, texture.Width * scale, texture.Height * scale), Vector2.Zero, rotation, tint);

    /// <summary>Draws the part of a texture <paramref name="source"/> covers. A negative width or height flips it.</summary>
    public static void DrawTextureRec(Texture2D texture, Rectangle source, Vector2 position, Color tint) =>
        DrawTexturePro(texture, source,
            new Rectangle(position.X, position.Y, MathF.Abs(source.Width), MathF.Abs(source.Height)), Vector2.Zero, 0, tint);

    /// <summary>
    /// Draws the part of a texture <paramref name="source"/> covers into <paramref name="dest"/>,
    /// rotated by <paramref name="rotation"/> degrees around <paramref name="origin"/>, which is
    /// relative to the top left of <paramref name="dest"/>.
    /// </summary>
    public static void DrawTexturePro(Texture2D texture, Rectangle source, Rectangle dest, Vector2 origin, float rotation, Color tint)
    {
        if (!texture.IsValid || texture.Width == 0 || texture.Height == 0) return;

        var u0 = source.X / texture.Width;
        var v0 = source.Y / texture.Height;
        var u1 = (source.X + MathF.Abs(source.Width)) / texture.Width;
        var v1 = (source.Y + MathF.Abs(source.Height)) / texture.Height;
        if (source.Width < 0) (u0, u1) = (u1, u0);
        if (source.Height < 0) (v0, v1) = (v1, v0);

        // Unturned, as most sprites are, a corner is an offset, with no sine and cosine to work out.
        var at = new Vector2(dest.X, dest.Y);
        var turn = rotation == 0 ? Matrix3x2.Identity : Matrix3x2.CreateRotation(float.DegreesToRadians(rotation));
        Vector3 Corner(float x, float y) => rotation == 0
            ? new Vector3(x - origin.X + at.X, y - origin.Y + at.Y, 0)
            : new Vector3(Vector2.Transform(new Vector2(x, y) - origin, turn) + at, 0);

        DrawList.TexturedQuad(
            Corner(0, 0), Corner(dest.Width, 0), Corner(dest.Width, dest.Height), Corner(0, dest.Height),
            new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1),
            tint, texture.Id);
    }

    /// <summary>
    /// Draws a texture in 3D facing <paramref name="camera"/>, <paramref name="size"/> world units
    /// high and as wide as the texture's shape makes it. Call it inside <see cref="BeginMode3D"/>.
    /// </summary>
    public static void DrawBillboard(Camera3D camera, Texture2D texture, Vector3 position, float size, Color tint)
    {
        if (!texture.IsValid) return;

        var view = camera.View;
        var right = new Vector3(view.M11, view.M21, view.M31) * (size * texture.Width / texture.Height / 2);
        var up = new Vector3(view.M12, view.M22, view.M32) * (size / 2);

        DrawList.TexturedQuad(
            position - right + up, position + right + up, position + right - up, position - right - up,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
            tint, texture.Id);
    }

    // Finds a file the way a program run from anywhere expects: as given (relative to the working
    // directory), then beside the program, then under source/ beside it, where content items land.
    private static string? ResolveFile(string fileName)
    {
        if (File.Exists(fileName)) return fileName;
        foreach (var root in new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "source") })
        {
            var candidate = Path.Combine(root, fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}

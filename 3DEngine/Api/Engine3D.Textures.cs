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

/// <summary>An image drawing can be sent to with <see cref="Engine3D.BeginTextureMode"/>, and drawn afterward through <see cref="TextureAsset"/>.</summary>
/// <param name="Texture">The color drawn.</param>
/// <param name="Depth">
/// The depth drawn, in red, from 0 at the camera's near plane to 1 at its far one, and 1 where
/// nothing was drawn, for a shader that fogs, outlines or softens by distance.
/// </param>
public readonly record struct RenderTexture2D(Texture2D Texture, Texture2D Depth = default)
{
    /// <summary>Whether this names a render texture that was loaded.</summary>
    public bool IsValid => Texture.IsValid;
}

/// <summary>How <see cref="Engine3D.DrawTextureNPatch"/> cuts a texture: into nine patches, or three across or down.</summary>
public enum NPatchLayout
{
    /// <summary>Corners kept at their size, edges stretched along their length and the middle both ways.</summary>
    NinePatch,
    /// <summary>A top and a bottom kept at their height, and the part between stretched down.</summary>
    ThreePatchVertical,
    /// <summary>A left and a right kept at their width, and the part between stretched across.</summary>
    ThreePatchHorizontal,
}

/// <summary>
/// The part of a texture <see cref="Engine3D.DrawTextureNPatch"/> draws, and how far in from each
/// of its edges the borders that keep their size reach, in pixels.
/// </summary>
public readonly record struct NPatchInfo(Rectangle Source, int Left, int Top, int Right, int Bottom, NPatchLayout Layout = NPatchLayout.NinePatch);

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

    /// <summary>
    /// Makes a grayscale image from text, each of its bytes a pixel as bright as the byte, row by
    /// row from the top, and black past its end, as raylib's <c>GenImageText</c> does.
    /// </summary>
    public static Image GenImageText(int width, int height, string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text ?? "");
        return Generate(width, height, (x, y) =>
        {
            var at = y * width + x;
            var value = at < bytes.Length ? bytes[at] : (byte)0;
            return new Color(value, value, value);
        });
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

    /// <summary>Every pixel of an image as a color, row by row from the top left.</summary>
    public static Color[] LoadImageColors(Image image)
    {
        var colors = new Color[image.Width * image.Height];
        for (int i = 0; i < colors.Length && i * 4 + 3 < image.Data.Length; i++)
            colors[i] = new Color(image.Data[i * 4], image.Data[i * 4 + 1], image.Data[i * 4 + 2], image.Data[i * 4 + 3]);
        return colors;
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
    /// <remarks>
    /// A render texture's pixels are written in the order of the drawing into it, as
    /// <see cref="UpdateTextureRec"/> writes them.
    /// </remarks>
    public static bool UpdateTexture(Texture2D texture, Image image) =>
        texture.IsValid && image.Width == texture.Width && image.Height == texture.Height &&
        (Textures.IsTarget(texture.Id)
            ? WriteIntoTarget(texture, 0, 0, image.Width, image.Height, image.Data)
            : Textures.Update(texture.Id, (byte[])image.Data.Clone()));

    /// <summary>
    /// Replaces the pixels of a rectangle of a texture, the rest kept, as a minimap or a painted
    /// canvas changes a part at a time, from <paramref name="pixels"/> four bytes a pixel, red,
    /// green, blue and alpha, rows from the top, the rectangle's size.
    /// </summary>
    /// <returns>Whether the texture is loaded and the rectangle lies inside it.</returns>
    /// <remarks>
    /// <para>
    /// The rectangle is written into the texture on the GPU in the frame after, once the frames
    /// before have finished drawing with it, and a texture with mip levels has them made again.
    /// </para>
    /// <para>
    /// A render texture's rectangle is written as the frame draws it, in the order of the drawing
    /// into it, as raylib's is, so pixels written after a <see cref="ClearBackground"/> inside
    /// <see cref="BeginTextureMode"/> are kept, and a shape drawn into it afterward lies over them.
    /// </para>
    /// </remarks>
    public static bool UpdateTextureRec(Texture2D texture, Rectangle rec, byte[] pixels) =>
        texture.IsValid && (Textures.IsTarget(texture.Id)
            ? WriteIntoTarget(texture, (int)rec.X, (int)rec.Y, (int)rec.Width, (int)rec.Height, pixels)
            : Textures.UpdateRegion(texture.Id, (byte[])pixels.Clone(), (int)rec.X, (int)rec.Y, (int)rec.Width, (int)rec.Height));

    // The textures holding pixels written into render targets this frame, unloaded once it is drawn.
    private static readonly List<int> TargetWrites = [];

    // Pixels for a render target, drawn into it as a quad that replaces what is there, in the order
    // of the drawing around it. A target is drawn as the frame ends, after the textures' uploads, so
    // an upload into its image would come before a ClearBackground called ahead of it and be lost.
    private static bool WriteIntoTarget(Texture2D target, int x, int y, int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0 || x < 0 || y < 0 || x + width > target.Width || y + height > target.Height
            || pixels.Length != width * height * 4)
            return false;
        var written = Textures.Add((byte[])pixels.Clone(), width, height, TextureFilter.Point);
        TargetWrites.Add(written);

        var (into, transform, depthTest, blend, scissor) = (DrawList.Target, DrawList.Transform, DrawList.DepthTest, DrawList.Blend, DrawList.Scissor);
        var (shader, parameters, uniforms, textures) = (DrawList.Shader, DrawList.Params, DrawList.Uniforms, DrawList.Textures);
        DrawList.SetTarget(target.Id);
        DrawList.SetTransform(Matrix4x4.CreateOrthographicOffCenter(0, target.Width, 0, target.Height, -1, 1), depthTest: false);
        // The engine's shader, told by param 0 to keep the clear pixels it discards elsewhere
        DrawList.SetShader(0, default(ShaderParams).With(0, Vector4.UnitX));
        DrawList.SetBlend(DrawList.Replace);
        DrawList.SetScissor(null);
        DrawList.TexturedQuad(new Vector3(x, y, 0), new Vector3(x, y + height, 0), new Vector3(x + width, y + height, 0), new Vector3(x + width, y, 0),
            Vector2.Zero, Vector2.UnitY, Vector2.One, Vector2.UnitX, Color.White, written);
        DrawList.SetTarget(into);
        DrawList.SetTransform(transform, depthTest);
        DrawList.SetShader(shader, parameters, uniforms, textures);
        DrawList.SetBlend(blend);
        DrawList.SetScissor(scissor);
        return true;
    }

    // Unloads the textures that carried pixels into render targets, once the frame has drawn them.
    private static void ForgetTargetWrites()
    {
        foreach (var written in TargetWrites) Textures.Remove(written);
        TargetWrites.Clear();
    }

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

    /// <summary>Sets what a texture shows past its edges, where a texture coordinate leaves 0 to 1. Textures repeat unless set.</summary>
    public static void SetTextureWrap(Texture2D texture, TextureWrap wrap)
    {
        if (texture.IsValid) Textures.SetWrap(texture.Id, wrap);
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
    /// Draws a texture stretched into <paramref name="dest"/> with its borders kept at their size,
    /// as a panel or button is drawn from a small image of one, rotated by
    /// <paramref name="rotation"/> degrees around <paramref name="origin"/>, which is relative to
    /// the top left of <paramref name="dest"/>.
    /// </summary>
    /// <remarks>
    /// A rectangle narrower or shorter than its two borders leaves out the middle and shrinks the
    /// borders in proportion, as raylib's does.
    /// </remarks>
    public static void DrawTextureNPatch(Texture2D texture, NPatchInfo nPatchInfo, Rectangle dest, Vector2 origin, float rotation, Color tint)
    {
        if (!texture.IsValid || texture.Width == 0 || texture.Height == 0) return;
        float width = texture.Width, height = texture.Height;
        var source = nPatchInfo.Source;
        var layout = nPatchInfo.Layout;
        var patchWidth = (int)dest.Width <= 0 ? 0 : dest.Width;
        var patchHeight = (int)dest.Height <= 0 ? 0 : dest.Height;
        if (source.Width < 0) source = source with { X = source.X - source.Width };
        if (source.Height < 0) source = source with { Y = source.Y - source.Height };
        if (layout == NPatchLayout.ThreePatchHorizontal) patchHeight = source.Height;
        if (layout == NPatchLayout.ThreePatchVertical) patchWidth = source.Width;

        float left = nPatchInfo.Left, top = nPatchInfo.Top, right = nPatchInfo.Right, bottom = nPatchInfo.Bottom;
        bool center = true, middle = true;
        if (patchWidth <= left + right && layout != NPatchLayout.ThreePatchVertical)
        {
            center = false;
            left = left + right > 0 ? left / (left + right) * patchWidth : 0;
            right = patchWidth - left;
        }
        if (patchHeight <= top + bottom && layout != NPatchLayout.ThreePatchHorizontal)
        {
            middle = false;
            top = top + bottom > 0 ? top / (top + bottom) * patchHeight : 0;
            bottom = patchHeight - top;
        }

        // The four lines across and down that cut the rectangle, and where each falls in the texture.
        float[] xs = [0, left, patchWidth - right, patchWidth];
        float[] ys = [0, top, patchHeight - bottom, patchHeight];
        float[] us = [source.X / width, (source.X + left) / width, (source.X + source.Width - right) / width, (source.X + source.Width) / width];
        float[] vs = [source.Y / height, (source.Y + top) / height, (source.Y + source.Height - bottom) / height, (source.Y + source.Height) / height];

        var at = new Vector2(dest.X, dest.Y);
        var turn = Matrix3x2.CreateRotation(float.DegreesToRadians(rotation));
        Vector3 Corner(float x, float y) => new(Vector2.Transform(new Vector2(x, y) - origin, turn) + at, 0);
        // The patch between lines i0 and i1 across and j0 and j1 down.
        void Patch(int i0, int j0, int i1, int j1) =>
            DrawList.TexturedQuad(Corner(xs[i0], ys[j0]), Corner(xs[i1], ys[j0]), Corner(xs[i1], ys[j1]), Corner(xs[i0], ys[j1]),
                new Vector2(us[i0], vs[j0]), new Vector2(us[i1], vs[j0]), new Vector2(us[i1], vs[j1]), new Vector2(us[i0], vs[j1]),
                tint, texture.Id);

        switch (layout)
        {
            case NPatchLayout.NinePatch:
                for (int j = 0; j < 3; j++)
                {
                    if (j == 1 && !middle) continue;
                    for (int i = 0; i < 3; i++)
                        if (i != 1 || center) Patch(i, j, i + 1, j + 1);
                }
                break;
            case NPatchLayout.ThreePatchVertical:
                Patch(0, 0, 3, 1);
                if (middle) Patch(0, 1, 3, 2);
                Patch(0, 2, 3, 3);
                break;
            case NPatchLayout.ThreePatchHorizontal:
                Patch(0, 0, 1, 3);
                if (center) Patch(1, 0, 2, 3);
                Patch(2, 0, 3, 3);
                break;
        }
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

    /// <summary>
    /// Draws the part of a texture <paramref name="source"/> covers in 3D, centered on
    /// <paramref name="position"/>, <paramref name="size"/> world units across and up, turned
    /// toward <paramref name="camera"/> about the world's up axis, as a tree or a sprite standing
    /// on the ground is drawn.
    /// </summary>
    public static void DrawBillboardRec(Camera3D camera, Texture2D texture, Rectangle source, Vector3 position, Vector2 size, Color tint) =>
        DrawBillboardPro(camera, texture, source, position, Vector3.UnitY, size, size / 2, 0, tint);

    /// <summary>
    /// Draws the part of a texture <paramref name="source"/> covers in 3D, <paramref name="size"/>
    /// world units along the camera's right and along <paramref name="up"/>, with
    /// <paramref name="origin"/>, measured from its bottom left corner in the same units, at
    /// <paramref name="position"/>, and turned <paramref name="rotation"/> degrees about it in the
    /// billboard's plane, counterclockwise as the camera sees it.
    /// </summary>
    /// <remarks>A negative size flips the texture along that side, as raylib's does.</remarks>
    public static void DrawBillboardPro(Camera3D camera, Texture2D texture, Rectangle source, Vector3 position, Vector3 up,
        Vector2 size, Vector2 origin, float rotation, Color tint)
    {
        if (!texture.IsValid || texture.Width == 0 || texture.Height == 0) return;

        var view = camera.View;
        var right = new Vector3(view.M11, view.M21, view.M31) * size.X;
        up *= size.Y;
        if (size.X < 0)
        {
            source = source with { X = source.X + size.X, Width = -source.Width };
            right = -right;
            origin.X = -origin.X;
        }
        if (size.Y < 0)
        {
            source = source with { Y = source.Y + size.Y, Height = -source.Height };
            up = -up;
            origin.Y = -origin.Y;
        }

        var pivot = SafeNormalize(right) * origin.X + SafeNormalize(up) * origin.Y;
        var turn = rotation == 0 ? Quaternion.Identity
            : Quaternion.CreateFromAxisAngle(SafeNormalize(Vector3.Cross(right, up)), float.DegreesToRadians(rotation));
        Vector3 Corner(Vector3 offset) => Vector3.Transform(offset - pivot, turn) + position;

        float u0 = source.X / texture.Width, u1 = (source.X + source.Width) / texture.Width;
        float vTop = source.Y / texture.Height, vBottom = (source.Y + source.Height) / texture.Height;
        DrawList.TexturedQuad(
            Corner(up), Corner(up + right), Corner(right), Corner(Vector3.Zero),
            new Vector2(u0, vTop), new Vector2(u1, vTop), new Vector2(u1, vBottom), new Vector2(u0, vBottom),
            tint, texture.Id);
    }

    private static Vector3 SafeNormalize(Vector3 v) => v.LengthSquared() > 0 ? Vector3.Normalize(v) : Vector3.Zero;

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

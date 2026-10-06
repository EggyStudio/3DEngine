using System.Diagnostics;
using System.Numerics;
using ImGuiNET;

namespace Engine;

public static partial class Engine3D
{
    private static bool _inFrame;
    private static long _drawingStart;

    // -- Frame

    /// <summary>
    /// Starts a frame. Runs <see cref="Stage.First"/> through <see cref="Stage.Update"/> and starts
    /// an ImGui frame, so <c>Draw</c> calls and <c>ImGui</c> calls work until <see cref="EndDrawing"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The previous frame was not ended.</exception>
    public static void BeginDrawing()
    {
        if (_inFrame)
            throw new InvalidOperationException("BeginDrawing was called twice without EndDrawing.");

        // The program's own code between frames, which no stage of the schedule times.
        var update = Stopwatch.GetElapsedTime(_lastFrameEnd);
        PumpEvents();
        GetApp().BeginFrame();
        _frameTimeTakenAt = Stopwatch.GetTimestamp();
        _inFrame = true;
        _target = default;
        _shader = default;
        SetRlCamera(Matrix4x4.Identity, ScreenTransform(), depthTest: false);
        ResetRlgl();
        Profile("program.update", update);
        _drawingStart = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Ends the frame. Runs <see cref="Stage.PostUpdate"/> through <see cref="Stage.Last"/>, which
    /// renders and presents it, then waits for the frame rate <see cref="SetTargetFPS"/> set.
    /// </summary>
    /// <exception cref="InvalidOperationException">No frame was begun.</exception>
    public static void EndDrawing()
    {
        if (!_inFrame)
            throw new InvalidOperationException("EndDrawing was called without BeginDrawing.");

        var drawing = Stopwatch.GetElapsedTime(_drawingStart);
        GetApp().EndFrame();
        ForgetTargetWrites();
        // The draw list is cleared as the frame is rendered, and what a program draws before the
        // next BeginDrawing, into a render texture, is culled as it last set.
        ApplyRlCulling();
        // The cameras the frame drew with are forgotten once it is rendered, and not as the next
        // begins, so a 3D camera a program sets for a render texture between frames is kept.
        if (TryRes<Mode3DCamera>(out var mode3D))
        {
            mode3D.ViewProjection = null;
            mode3D.Targets.Clear();
        }
        FeedAudioStreams();
        _inFrame = false;
        _eventsPumped = false;
        var waiting = Stopwatch.GetTimestamp();
        WaitForTargetFrame();
        Profile("program.drawing", drawing);
        Profile("wait", Stopwatch.GetElapsedTime(waiting));
    }

    /// <summary>Sets the color the frame, or the render target inside <see cref="BeginTextureMode"/>, is cleared to.</summary>
    /// <remarks>
    /// The whole frame or target is cleared before anything is drawn into it, wherever in the frame
    /// this is called. A render target not cleared in a frame keeps what it held, as raylib's does,
    /// so a trail or a painting drawn into it a little each frame builds up.
    /// </remarks>
    public static void ClearBackground(Color color)
    {
        if (_target.IsValid)
        {
            DrawList.SetTargetClear(color);
            return;
        }

        var c = color.ToVector4();
        World.InsertResource(new ClearColor(c.X, c.Y, c.Z, c.W));
    }

    // -- Render targets

    private static RenderTexture2D _target;

    /// <summary>
    /// Makes an image of <paramref name="width"/> by <paramref name="height"/> pixels that drawing
    /// can be sent to, with its depth to sample as <see cref="RenderTexture2D.Depth"/>.
    /// </summary>
    public static RenderTexture2D LoadRenderTexture(int width, int height)
    {
        (width, height) = (Math.Max(1, width), Math.Max(1, height));
        var id = Textures.AddTarget(width, height);
        var depth = Textures.AddTargetDepth(id);
        return new RenderTexture2D(new Texture2D(id, width, height), new Texture2D(depth, width, height));
    }

    /// <summary>
    /// Makes an image of <paramref name="width"/> by <paramref name="height"/> pixels for each of
    /// <paramref name="formats"/>, up to four, that drawing is sent to at once, as a G-buffer is
    /// drawn into, with one depth to sample as <see cref="RenderTexture2D.Depth"/>. A shader writes
    /// each from its output of the same index, <c>SV_Target0</c> into the first, and
    /// <see cref="RenderTexture2D.Textures"/> are the images in the order of the formats.
    /// </summary>
    /// <remarks>
    /// raylib builds such a target from rlgl's framebuffers, which are not carried. Each image has
    /// four channels, of eight bits for a format of eight bits a channel or fewer, and of half floats
    /// and floats for one of 16 and 32 bits, so a position or a normal outside 0 to 1 is kept. A
    /// shader with fewer outputs than the images leaves those past them as they are, and a
    /// compressed format is drawn into as eight-bit RGBA, with a warning.
    /// </remarks>
    /// <exception cref="ArgumentException">There are no formats, or more than four.</exception>
    public static RenderTexture2D LoadRenderTexture(int width, int height, params PixelFormat[] formats)
    {
        if (formats.Length is 0 or > 4) throw new ArgumentException($"A render texture draws into 1 to 4 images, not {formats.Length}.", nameof(formats));
        (width, height) = (Math.Max(1, width), Math.Max(1, height));
        var id = Textures.AddTarget(width, height, formats: [.. formats.Select(TargetFormat)]);
        var depth = Textures.AddTargetDepth(id);
        Texture2D[] textures = [new Texture2D(id, width, height), .. Enumerable.Range(1, formats.Length - 1)
            .Select(index => new Texture2D(Textures.AddTargetColor(id, index), width, height))];
        return new RenderTexture2D(textures[0], new Texture2D(depth, width, height)) { Textures = textures };

        static Engine.ImageFormat TargetFormat(PixelFormat format)
        {
            switch (format)
            {
                case PixelFormat.UncompressedR32 or PixelFormat.UncompressedR32G32B32 or PixelFormat.UncompressedR32G32B32A32:
                    return Engine.ImageFormat.R32G32B32A32_Float;
                case PixelFormat.UncompressedR16 or PixelFormat.UncompressedR16G16B16 or PixelFormat.UncompressedR16G16B16A16:
                    return Engine.ImageFormat.R16G16B16A16_Float;
                case >= PixelFormat.CompressedDxt1Rgb:
                    ApiLogger.Warn($"LoadRenderTexture: a render texture is not drawn into in {format}, so it is eight-bit RGBA.");
                    return Engine.ImageFormat.R8G8B8A8_UNorm;
                default:
                    return Engine.ImageFormat.R8G8B8A8_UNorm;
            }
        }
    }

    /// <summary>An image drawing can be sent to, of one <paramref name="format"/>, as raylib's <c>LoadRenderTextureEx</c> makes one.</summary>
    /// <remarks>A format of 16 or 32 bits a channel is drawn into as half floats or floats, so light past white is kept, and the rest as eight-bit RGBA.</remarks>
    public static RenderTexture2D LoadRenderTextureEx(int width, int height, PixelFormat format) => LoadRenderTexture(width, height, format);

    /// <summary>Frees a render texture, each image it draws into and its depth.</summary>
    public static void UnloadRenderTexture(RenderTexture2D target)
    {
        if (target.Depth.IsValid) UnloadTexture(target.Depth);
        foreach (var texture in target.Textures.Skip(1)) UnloadTexture(texture);
        UnloadTexture(target.Texture);
    }

    /// <summary>Whether <paramref name="target"/> is loaded.</summary>
    public static bool IsRenderTextureValid(RenderTexture2D target) => IsTextureValid(target.Texture);

    /// <summary>
    /// Sends the following drawing into <paramref name="target"/> until <see cref="EndTextureMode"/>,
    /// in pixels from its top left corner. Its <see cref="RenderTexture2D.Texture"/> then draws like
    /// any texture.
    /// </summary>
    /// <remarks>
    /// A target is drawn before the window, whatever order the calls come in, and is cleared first
    /// in a frame that draws into it, to the color <see cref="ClearBackground"/> set inside this mode
    /// or to transparent black.
    /// </remarks>
    public static void BeginTextureMode(RenderTexture2D target)
    {
        _target = target;
        DrawList.SetTarget(target.Texture.Id);
        SetRlCamera(Matrix4x4.Identity, ScreenTransform(), depthTest: false);
    }

    /// <summary>Returns drawing to the window.</summary>
    public static void EndTextureMode()
    {
        _target = default;
        DrawList.SetTarget(0);
        SetRlCamera(Matrix4x4.Identity, ScreenTransform(), depthTest: false);
    }

    // -- Blending and scissors

    /// <summary>Lays the following shapes, textures and text over what is there by <paramref name="mode"/> until <see cref="EndBlendMode"/>.</summary>
    public static void BeginBlendMode(BlendMode mode) => DrawList.SetBlend(mode, RlBlendFactorsFor(mode));

    /// <summary>Returns to laying what is drawn over by its alpha.</summary>
    public static void EndBlendMode() => DrawList.SetBlend(BlendMode.Alpha);

    /// <summary>
    /// Keeps the following drawing to a rectangle, in pixels from the top left of the window, or of
    /// the render target inside <see cref="BeginTextureMode"/>, until <see cref="EndScissorMode"/>.
    /// </summary>
    /// <remarks>
    /// It keeps the shapes, textures and text drawn after it, in 2D and 3D, to the rectangle, as a
    /// scrolling panel does. Models and ImGui are not kept to it.
    /// </remarks>
    public static void BeginScissorMode(int x, int y, int width, int height)
    {
        // The window's units are those of mouse positions, which a display with more pixels than
        // that, as a scaled one has, multiplies into the framebuffer's.
        var (scaleX, scaleY) = (1f, 1f);
        if (!_target.IsValid && TryRes<AppWindow>(out var window)
            && SDL3.SDL.GetWindowSizeInPixels(window.Sdl.Window, out var pixelsWide, out var pixelsHigh)
            && GetScreenWidth() > 0 && GetScreenHeight() > 0)
            (scaleX, scaleY) = ((float)pixelsWide / GetScreenWidth(), (float)pixelsHigh / GetScreenHeight());
        DrawList.SetScissor(new ScissorRect((int)(x * scaleX), (int)(y * scaleY), (int)(width * scaleX), (int)(height * scaleY)));
    }

    /// <summary>Returns to drawing over the whole window or render target.</summary>
    public static void EndScissorMode() => DrawList.SetScissor(null);

    // -- Cameras

    /// <summary>Draws the following shapes through <paramref name="camera"/>, depth tested, until <see cref="EndMode3D"/>.</summary>
    /// <remarks>
    /// The first camera of a frame begun in the window also draws the ECS's mesh entities, the models
    /// a loaded scene places, when no <see cref="Camera"/> entity draws the window.
    /// </remarks>
    public static void BeginMode3D(Camera3D camera)
    {
        var (width, height) = DrawingSize();
        var aspect = (float)width / Math.Max(1, height);
        var projection = camera.ProjectionMatrix(aspect);
        var viewProjection = camera.View * projection;
        SetRlCamera(camera.View, projection, depthTest: true);
        ResetRlglUnlessPushed();
        _camera3D = camera;
        var mode3D = World.GetOrInsertResource(static () => new Mode3DCamera());
        if (_target.IsValid) mode3D.Targets.TryAdd(_target.Texture.Id, (viewProjection, camera.Position));
        else if (mode3D.ViewProjection is null) (mode3D.ViewProjection, mode3D.Eye) = (viewProjection, camera.Position);
    }

    // The camera of the BeginMode3D in effect, which DrawSkybox centers its cube on.
    private static Camera3D? _camera3D;

    /// <summary>Returns to drawing in screen space, in pixels from the top left corner.</summary>
    public static void EndMode3D()
    {
        _camera3D = null;
        SetRlCamera(Matrix4x4.Identity, ScreenTransform(), depthTest: false);
        ResetRlglUnlessPushed();
    }

    // Pixels from the top left corner to clip space. Vulkan's clip space points down, so the top
    // of the window maps to -1.
    private static Matrix4x4 ScreenTransform()
    {
        var (width, height) = DrawingSize();
        return Matrix4x4.CreateOrthographicOffCenter(0, width, 0, height, -1, 1);
    }

    // The size of what is being drawn into: the render target inside BeginTextureMode, the window otherwise.
    private static (int Width, int Height) DrawingSize() =>
        _target.IsValid ? (_target.Texture.Width, _target.Texture.Height) : (GetScreenWidth(), GetScreenHeight());
}

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
        _inFrame = true;
        _target = default;
        _shader = default;
        DrawList.SetTransform(ScreenTransform(), depthTest: false);
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
        FeedAudioStreams();
        _inFrame = false;
        _eventsPumped = false;
        var waiting = Stopwatch.GetTimestamp();
        WaitForTargetFrame();
        Profile("program.drawing", drawing);
        Profile("wait", Stopwatch.GetElapsedTime(waiting));
    }

    /// <summary>Sets the color the frame, or the render target inside <see cref="BeginTextureMode"/>, is cleared to.</summary>
    /// <remarks>The whole frame or target is cleared before anything is drawn into it, wherever in the frame this is called.</remarks>
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

    /// <summary>Frees a render texture and its depth.</summary>
    public static void UnloadRenderTexture(RenderTexture2D target)
    {
        if (target.Depth.IsValid) UnloadTexture(target.Depth);
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
        DrawList.SetTransform(ScreenTransform(), depthTest: false);
    }

    /// <summary>Returns drawing to the window.</summary>
    public static void EndTextureMode()
    {
        _target = default;
        DrawList.SetTarget(0);
        DrawList.SetTransform(ScreenTransform(), depthTest: false);
    }

    // -- Blending and scissors

    /// <summary>Lays the following shapes, textures and text over what is there by <paramref name="mode"/> until <see cref="EndBlendMode"/>.</summary>
    public static void BeginBlendMode(BlendMode mode) => DrawList.SetBlend(mode);

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
    public static void BeginMode3D(Camera3D camera)
    {
        var (width, height) = DrawingSize();
        var aspect = (float)width / Math.Max(1, height);
        DrawList.SetTransform(camera.View * camera.ProjectionMatrix(aspect), depthTest: true);
        _camera3D = camera;
    }

    // The camera of the BeginMode3D in effect, which DrawSkybox centers its cube on.
    private static Camera3D? _camera3D;

    /// <summary>Returns to drawing in screen space, in pixels from the top left corner.</summary>
    public static void EndMode3D()
    {
        _camera3D = null;
        DrawList.SetTransform(ScreenTransform(), depthTest: false);
    }

    /// <summary>
    /// Moves and turns a camera by amounts the program works out, as raylib's does. Movement's x is
    /// forward, y right and z up, forward and right kept level, rotation's x turns it right, y down
    /// and z rolls it, in degrees, and <paramref name="zoom"/> moves it so many units farther from its
    /// target, or nearer when negative, the target staying where it is.
    /// </summary>
    public static void UpdateCameraPro(ref Camera3D camera, Vector3 movement, Vector3 rotation, float zoom)
    {
        var up = Vector3.Normalize(camera.Up);
        var toTarget = camera.Target - camera.Position;
        var forward = Vector3.Normalize(toTarget);
        var level = Vector3.Normalize(forward - up * Vector3.Dot(forward, up));
        var right = Vector3.Normalize(Vector3.Cross(forward, up));

        // Turned about the camera, right about its up, down about its right, and rolled about the way it looks.
        var turn = Quaternion.CreateFromAxisAngle(up, -float.DegreesToRadians(rotation.X))
                   * Quaternion.CreateFromAxisAngle(right, -float.DegreesToRadians(rotation.Y));
        toTarget = Vector3.Transform(toTarget, turn);
        if (rotation.Z != 0) camera.Up = Vector3.Transform(up, Quaternion.CreateFromAxisAngle(Vector3.Normalize(toTarget), float.DegreesToRadians(rotation.Z)));

        var moved = level * movement.X + right * movement.Y + up * movement.Z;
        camera.Position += moved;
        camera.Target = camera.Position + toTarget;
        var distance = MathF.Max(0.001f, toTarget.Length() + zoom);
        camera.Position = camera.Target - Vector3.Normalize(toTarget) * distance;
    }

    /// <summary>Moves <paramref name="camera"/> from this frame's input, as <paramref name="mode"/> describes.</summary>
    /// <remarks>Typing into an ImGui field, or using the mouse over an ImGui window, does not move the camera.</remarks>
    public static void UpdateCamera(ref Camera3D camera, CameraMode mode)
    {
        var dt = GetFrameTime();
        var input = Res<Input>();
        var io = ImGui.GetIO();
        // WantTextInput rather than WantCaptureKeyboard, because with keyboard navigation on ImGui
        // claims the keyboard whenever any of its windows has focus, which is nearly always.
        var keys = !io.WantTextInput;
        var mouse = !io.WantCaptureMouse;

        var offset = camera.Position - camera.Target;
        var distance = offset.Length();
        var up = Vector3.Normalize(camera.Up);

        if (mode == CameraMode.Orbital)
        {
            var spin = Quaternion.CreateFromAxisAngle(up, 0.5f * dt);
            offset = Vector3.Transform(offset, spin);
            if (mouse && input.WheelY != 0)
                offset *= MathF.Pow(0.9f, input.WheelY);
            camera.Position = camera.Target + offset;
            return;
        }

        var forward = Vector3.Normalize(-offset);
        // The first- and third-person cameras turn with the mouse as it moves, the free camera
        // only while the right button is held.
        var turns = mode is CameraMode.FirstPerson or CameraMode.ThirdPerson || input.MouseDown(MouseButton.Right);
        if (mouse && turns)
            forward = Turn(forward, up, input.MouseDeltaX, input.MouseDeltaY);

        if (mode is CameraMode.FirstPerson or CameraMode.ThirdPerson)
        {
            // Walking stays on the ground, whatever the camera looks at.
            var ahead = forward - Vector3.Dot(forward, up) * up;
            ahead = ahead.LengthSquared() > 1e-6f ? Vector3.Normalize(ahead) : forward;
            var aside = Vector3.Normalize(Vector3.Cross(ahead, up));
            var walk = Vector3.Zero;
            if (keys)
            {
                if (input.KeyDown(Key.W)) walk += ahead;
                if (input.KeyDown(Key.S)) walk -= ahead;
                if (input.KeyDown(Key.D)) walk += aside;
                if (input.KeyDown(Key.A)) walk -= aside;
            }
            var pace = keys && (input.KeyDown(Key.LShift) || input.KeyDown(Key.RShift)) ? 10f : 5f;
            if (walk != Vector3.Zero) walk = Vector3.Normalize(walk) * pace * dt;

            if (mode == CameraMode.FirstPerson)
            {
                camera.Position += walk;
                camera.Target = camera.Position + forward * MathF.Max(distance, 0.001f);
            }
            else
            {
                if (mouse && input.WheelY != 0) distance = Math.Clamp(distance * MathF.Pow(0.9f, input.WheelY), 0.5f, 1000f);
                camera.Target += walk;
                camera.Position = camera.Target - forward * distance;
            }
            return;
        }

        var side = Vector3.Normalize(Vector3.Cross(forward, up));
        var move = Vector3.Zero;
        if (keys)
        {
            if (input.KeyDown(Key.W)) move += forward;
            if (input.KeyDown(Key.S)) move -= forward;
            if (input.KeyDown(Key.D)) move += side;
            if (input.KeyDown(Key.A)) move -= side;
            if (input.KeyDown(Key.E)) move += up;
            if (input.KeyDown(Key.Q)) move -= up;
        }

        var speed = keys && (input.KeyDown(Key.LShift) || input.KeyDown(Key.RShift)) ? 15f : 5f;
        if (move != Vector3.Zero) move = Vector3.Normalize(move) * speed * dt;
        if (mouse && input.WheelY != 0) move += forward * input.WheelY * 0.5f;

        camera.Position += move;
        camera.Target = camera.Position + forward * MathF.Max(distance, 0.001f);
    }

    // Turns a direction by the mouse's movement: around the up axis for sideways movement, and up
    // or down for vertical movement, stopping short of straight up or down, where the look-at
    // basis has no defined right.
    private static Vector3 Turn(Vector3 forward, Vector3 up, int dx, int dy)
    {
        const float sensitivity = 0.003f;
        var right = Vector3.Normalize(Vector3.Cross(forward, up));
        var turned = Vector3.Transform(forward, Quaternion.CreateFromAxisAngle(up, -dx * sensitivity));
        var pitched = Vector3.Transform(turned, Quaternion.CreateFromAxisAngle(right, -dy * sensitivity));
        if (MathF.Abs(Vector3.Dot(pitched, up)) < 0.99f) turned = pitched;
        return Vector3.Normalize(turned);
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

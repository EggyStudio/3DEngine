using System.Numerics;
using ImGuiNET;

namespace Engine;

public static partial class Engine3D
{
    private static bool _inFrame;

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

        PumpEvents();
        GetApp().BeginFrame();
        _inFrame = true;
        _target = default;
        DrawList.SetTransform(ScreenTransform(), depthTest: false);
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

        GetApp().EndFrame();
        _inFrame = false;
        _eventsPumped = false;
        WaitForTargetFrame();
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

    /// <summary>Makes an image of <paramref name="width"/> by <paramref name="height"/> pixels that drawing can be sent to.</summary>
    public static RenderTexture2D LoadRenderTexture(int width, int height)
    {
        var id = Textures.AddTarget(Math.Max(1, width), Math.Max(1, height));
        return new RenderTexture2D(new Texture2D(id, Math.Max(1, width), Math.Max(1, height)));
    }

    /// <summary>Frees a render texture.</summary>
    public static void UnloadRenderTexture(RenderTexture2D target) => UnloadTexture(target.Texture);

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

    // -- Cameras

    /// <summary>Draws the following shapes through <paramref name="camera"/>, depth tested, until <see cref="EndMode3D"/>.</summary>
    public static void BeginMode3D(Camera3D camera)
    {
        var (width, height) = DrawingSize();
        var aspect = (float)width / Math.Max(1, height);
        DrawList.SetTransform(camera.View * camera.ProjectionMatrix(aspect), depthTest: true);
    }

    /// <summary>Returns to drawing in screen space, in pixels from the top left corner.</summary>
    public static void EndMode3D() => DrawList.SetTransform(ScreenTransform(), depthTest: false);

    /// <summary>Moves <paramref name="camera"/> from this frame's input, as <paramref name="mode"/> describes.</summary>
    /// <remarks>Typing into an ImGui field, or using the mouse over an ImGui window, does not move the camera.</remarks>
    public static void UpdateCamera(ref Camera3D camera, CameraMode mode)
    {
        var dt = GetFrameTime();
        var input = World.Resource<Input>();
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
        if (mouse && input.MouseDown(MouseButton.Right))
        {
            const float sensitivity = 0.003f;
            var right = Vector3.Normalize(Vector3.Cross(forward, up));
            var yaw = Quaternion.CreateFromAxisAngle(up, -input.MouseDeltaX * sensitivity);
            var turned = Vector3.Transform(forward, yaw);
            var pitched = Vector3.Transform(turned, Quaternion.CreateFromAxisAngle(right, -input.MouseDeltaY * sensitivity));
            // Stops short of straight up or down, where the look-at basis has no defined right.
            if (MathF.Abs(Vector3.Dot(pitched, up)) < 0.99f) turned = pitched;
            forward = Vector3.Normalize(turned);
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

using System.Numerics;
using ImGuiNET;

namespace Engine;

public static partial class Engine3D
{
    // rcamera's speeds, so a camera moved by UpdateCamera feels as raylib's does.
    private const float CameraMoveSpeed = 5.4f;
    private const float CameraRotationSpeed = 0.03f;
    private const float CameraPanSpeed = 2.0f;
    private const float CameraMouseMoveSensitivity = 0.003f;
    private const float CameraOrbitalSpeed = 0.5f;

    /// <summary>The way <paramref name="camera"/> looks, from its position to its target, of length one.</summary>
    public static Vector3 GetCameraForward(Camera3D camera) => Vector3.Normalize(camera.Target - camera.Position);

    /// <summary>The camera's up, of length one, which need not be square to the way it looks.</summary>
    public static Vector3 GetCameraUp(Camera3D camera) => Vector3.Normalize(camera.Up);

    /// <summary>The camera's right, square to the way it looks and its up, of length one.</summary>
    public static Vector3 GetCameraRight(Camera3D camera) =>
        Vector3.Normalize(Vector3.Cross(GetCameraForward(camera), GetCameraUp(camera)));

    /// <summary>
    /// Moves a camera and its target the way it looks by <paramref name="distance"/>, kept level on
    /// the plane its up stands on when <paramref name="moveInWorldPlane"/>, as one walks.
    /// </summary>
    public static void CameraMoveForward(ref Camera3D camera, float distance, bool moveInWorldPlane)
    {
        var forward = GetCameraForward(camera);
        if (moveInWorldPlane) forward = OnWorldPlane(camera, forward);
        camera.Position += forward * distance;
        camera.Target += forward * distance;
    }

    /// <summary>Moves a camera and its target along its up by <paramref name="distance"/>.</summary>
    public static void CameraMoveUp(ref Camera3D camera, float distance)
    {
        var up = GetCameraUp(camera) * distance;
        camera.Position += up;
        camera.Target += up;
    }

    /// <summary>
    /// Moves a camera and its target to its right by <paramref name="distance"/>, kept level when
    /// <paramref name="moveInWorldPlane"/>.
    /// </summary>
    public static void CameraMoveRight(ref Camera3D camera, float distance, bool moveInWorldPlane)
    {
        var right = GetCameraRight(camera);
        if (moveInWorldPlane) right = OnWorldPlane(camera, right);
        camera.Position += right * distance;
        camera.Target += right * distance;
    }

    /// <summary>Moves a camera <paramref name="delta"/> farther from its target, or nearer when negative, no nearer than a thousandth.</summary>
    public static void CameraMoveToTarget(ref Camera3D camera, float delta)
    {
        var distance = Vector3.Distance(camera.Position, camera.Target) + delta;
        if (distance <= 0) distance = 0.001f;
        camera.Position = camera.Target - GetCameraForward(camera) * distance;
    }

    /// <summary>
    /// Turns a camera left by <paramref name="angle"/> radians about its up, or right when negative,
    /// about its target when <paramref name="rotateAroundTarget"/> and about itself otherwise.
    /// </summary>
    public static void CameraYaw(ref Camera3D camera, float angle, bool rotateAroundTarget)
    {
        var view = Vector3.Transform(camera.Target - camera.Position, Quaternion.CreateFromAxisAngle(GetCameraUp(camera), angle));
        if (rotateAroundTarget) camera.Position = camera.Target - view;
        else camera.Target = camera.Position + view;
    }

    /// <summary>
    /// Tips a camera up by <paramref name="angle"/> radians about its right, or down when negative.
    /// </summary>
    /// <remarks>
    /// <paramref name="lockView"/> stops it a thousandth short of straight up or down, so it never
    /// turns over, <paramref name="rotateAroundTarget"/> turns it about its target rather than
    /// itself, and <paramref name="rotateUp"/> tips its up with it, as a free camera's is.
    /// </remarks>
    public static void CameraPitch(ref Camera3D camera, float angle, bool lockView, bool rotateAroundTarget, bool rotateUp)
    {
        var up = GetCameraUp(camera);
        var view = camera.Target - camera.Position;

        if (lockView)
        {
            var maxAngleUp = Angle(up, view) - 0.001f;
            if (angle > maxAngleUp) angle = maxAngleUp;
            var maxAngleDown = -Angle(-up, view) + 0.001f;
            if (angle < maxAngleDown) angle = maxAngleDown;
        }

        var right = GetCameraRight(camera);
        var turn = Quaternion.CreateFromAxisAngle(right, angle);
        view = Vector3.Transform(view, turn);

        if (rotateAroundTarget) camera.Position = camera.Target - view;
        else camera.Target = camera.Position + view;

        if (rotateUp) camera.Up = Vector3.Transform(camera.Up, turn);
    }

    /// <summary>Rolls a camera by <paramref name="angle"/> radians about the way it looks, as a head tilts.</summary>
    public static void CameraRoll(ref Camera3D camera, float angle) =>
        camera.Up = Vector3.Transform(camera.Up, Quaternion.CreateFromAxisAngle(GetCameraForward(camera), angle));

    /// <summary>The camera's view, the world to the camera, as <see cref="GetCameraMatrix"/> gives it.</summary>
    public static Matrix4x4 GetCameraViewMatrix(Camera3D camera) => camera.View;

    /// <summary>The camera's projection for a picture of <paramref name="aspect"/>, its width over its height, as the engine draws with it.</summary>
    public static Matrix4x4 GetCameraProjectionMatrix(Camera3D camera, float aspect) => camera.ProjectionMatrix(aspect);

    /// <summary>
    /// Moves and turns a camera by amounts the program works out, as raylib's does. Movement's x is
    /// forward, y right and z up, forward and right kept level, rotation's x turns it right, y down
    /// and z rolls it, in degrees, and <paramref name="zoom"/> moves it so many units farther from its
    /// target, or nearer when negative.
    /// </summary>
    public static void UpdateCameraPro(ref Camera3D camera, Vector3 movement, Vector3 rotation, float zoom)
    {
        CameraPitch(ref camera, -float.DegreesToRadians(rotation.Y), lockView: true, rotateAroundTarget: false, rotateUp: false);
        CameraYaw(ref camera, -float.DegreesToRadians(rotation.X), rotateAroundTarget: false);
        CameraRoll(ref camera, float.DegreesToRadians(rotation.Z));

        CameraMoveForward(ref camera, movement.X, moveInWorldPlane: true);
        CameraMoveRight(ref camera, movement.Y, moveInWorldPlane: true);
        CameraMoveUp(ref camera, movement.Z);

        CameraMoveToTarget(ref camera, zoom);
    }

    /// <summary>Moves <paramref name="camera"/> from this frame's input, as raylib's does in <paramref name="mode"/>.</summary>
    /// <remarks>
    /// <para>
    /// Every mode but the orbital one and <see cref="CameraMode.Custom"/> turns with the mouse, the
    /// arrow keys and the gamepad's right stick, rolls with Q and E, and moves with W, A, S and D and
    /// the left stick, the first- and third-person cameras keeping level. The free camera rises with
    /// Space, sinks with left Ctrl and pans while the middle button is held. The orbital camera
    /// turns about its target half a radian a second. The free, third-person and orbital cameras
    /// move nearer and farther with the wheel and the keypad's plus and minus.
    /// </para>
    /// <para>
    /// Typing into an ImGui field, or using the mouse over an ImGui window, does not move the camera.
    /// </para>
    /// </remarks>
    public static void UpdateCamera(ref Camera3D camera, CameraMode mode)
    {
        var io = ImGui.GetIO();
        // WantTextInput rather than WantCaptureKeyboard, because with keyboard navigation on ImGui
        // claims the keyboard whenever any of its windows has focus, which is nearly always.
        var keys = !io.WantTextInput;
        var mouse = !io.WantCaptureMouse;
        bool KeyDown(Key key) => keys && IsKeyDown(key);
        bool KeyPressed(Key key) => keys && IsKeyPressed(key);

        var mouseDelta = mouse ? GetMouseDelta() : Vector2.Zero;

        var moveInWorldPlane = mode is CameraMode.FirstPerson or CameraMode.ThirdPerson;
        var rotateAroundTarget = mode is CameraMode.ThirdPerson or CameraMode.Orbital;
        var lockView = mode is CameraMode.Free or CameraMode.FirstPerson or CameraMode.ThirdPerson or CameraMode.Orbital;
        const bool rotateUp = false;

        var dt = GetFrameTime();
        var moveSpeed = CameraMoveSpeed * dt;
        var rotationSpeed = CameraRotationSpeed * dt;
        var panSpeed = CameraPanSpeed * dt;
        var orbitalSpeed = CameraOrbitalSpeed * dt;

        if (mode == CameraMode.Custom) { }
        else if (mode == CameraMode.Orbital)
        {
            var view = Vector3.Transform(camera.Position - camera.Target, Quaternion.CreateFromAxisAngle(GetCameraUp(camera), orbitalSpeed));
            camera.Position = camera.Target + view;
        }
        else
        {
            if (KeyDown(Key.Down)) CameraPitch(ref camera, -rotationSpeed, lockView, rotateAroundTarget, rotateUp);
            if (KeyDown(Key.Up)) CameraPitch(ref camera, rotationSpeed, lockView, rotateAroundTarget, rotateUp);
            if (KeyDown(Key.Right)) CameraYaw(ref camera, -rotationSpeed, rotateAroundTarget);
            if (KeyDown(Key.Left)) CameraYaw(ref camera, rotationSpeed, rotateAroundTarget);
            if (KeyDown(Key.Q)) CameraRoll(ref camera, -rotationSpeed);
            if (KeyDown(Key.E)) CameraRoll(ref camera, rotationSpeed);

            if (mode == CameraMode.Free && mouse && IsMouseButtonDown(MouseButton.Middle))
            {
                if (mouseDelta.X > 0) CameraMoveRight(ref camera, panSpeed, moveInWorldPlane);
                if (mouseDelta.X < 0) CameraMoveRight(ref camera, -panSpeed, moveInWorldPlane);
                if (mouseDelta.Y > 0) CameraMoveUp(ref camera, -panSpeed);
                if (mouseDelta.Y < 0) CameraMoveUp(ref camera, panSpeed);
            }
            else
            {
                CameraYaw(ref camera, -mouseDelta.X * CameraMouseMoveSensitivity, rotateAroundTarget);
                CameraPitch(ref camera, -mouseDelta.Y * CameraMouseMoveSensitivity, lockView, rotateAroundTarget, rotateUp);
            }

            if (KeyDown(Key.W)) CameraMoveForward(ref camera, moveSpeed, moveInWorldPlane);
            if (KeyDown(Key.A)) CameraMoveRight(ref camera, -moveSpeed, moveInWorldPlane);
            if (KeyDown(Key.S)) CameraMoveForward(ref camera, -moveSpeed, moveInWorldPlane);
            if (KeyDown(Key.D)) CameraMoveRight(ref camera, moveSpeed, moveInWorldPlane);

            if (IsGamepadAvailable(0))
            {
                CameraYaw(ref camera, -(GetGamepadAxisMovement(0, GamepadAxis.RightX) * 2) * CameraMouseMoveSensitivity, rotateAroundTarget);
                CameraPitch(ref camera, -(GetGamepadAxisMovement(0, GamepadAxis.RightY) * 2) * CameraMouseMoveSensitivity, lockView, rotateAroundTarget, rotateUp);

                if (GetGamepadAxisMovement(0, GamepadAxis.LeftY) <= -0.25f) CameraMoveForward(ref camera, moveSpeed, moveInWorldPlane);
                if (GetGamepadAxisMovement(0, GamepadAxis.LeftX) <= -0.25f) CameraMoveRight(ref camera, -moveSpeed, moveInWorldPlane);
                if (GetGamepadAxisMovement(0, GamepadAxis.LeftY) >= 0.25f) CameraMoveForward(ref camera, -moveSpeed, moveInWorldPlane);
                if (GetGamepadAxisMovement(0, GamepadAxis.LeftX) >= 0.25f) CameraMoveRight(ref camera, moveSpeed, moveInWorldPlane);
            }

            if (mode == CameraMode.Free)
            {
                if (KeyDown(Key.Space)) CameraMoveUp(ref camera, moveSpeed);
                if (KeyDown(Key.LCtrl)) CameraMoveUp(ref camera, -moveSpeed);
            }
        }

        if (mode is CameraMode.ThirdPerson or CameraMode.Orbital or CameraMode.Free)
        {
            if (mouse) CameraMoveToTarget(ref camera, -GetMouseWheelMove());
            if (KeyPressed(Key.KpMinus)) CameraMoveToTarget(ref camera, 2.0f);
            if (KeyPressed(Key.KpPlus)) CameraMoveToTarget(ref camera, -2.0f);
        }
    }

    // A direction laid on the plane the camera's up stands on, the axis nearest that up taken out.
    private static Vector3 OnWorldPlane(Camera3D camera, Vector3 direction)
    {
        if (MathF.Abs(camera.Up.Z) > 0.7071f) direction.Z = 0;
        else if (MathF.Abs(camera.Up.X) > 0.7071f) direction.X = 0;
        else direction.Y = 0;
        return Vector3.Normalize(direction);
    }

    // The angle between two directions, in radians, as raymath's Vector3Angle measures it.
    private static float Angle(Vector3 a, Vector3 b) => MathF.Atan2(Vector3.Cross(a, b).Length(), Vector3.Dot(a, b));
}

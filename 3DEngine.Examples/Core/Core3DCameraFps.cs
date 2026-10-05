// raylib's core_3d_camera_fps example, Copyright (c) 2025 Agnis Aldiņš (@nezvers), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Core3DCameraFps
{
    private const float GRAVITY = 32.0f;
    private const float MAX_SPEED = 20.0f;
    private const float CROUCH_SPEED = 5.0f;
    private const float JUMP_FORCE = 12.0f;
    private const float MAX_ACCEL = 150.0f;
    private const float FRICTION = 0.86f;
    private const float AIR_DRAG = 0.98f;
    private const float CONTROL = 15.0f;
    private const float CROUCH_HEIGHT = 0.0f;
    private const float STAND_HEIGHT = 1.0f;
    private const float BOTTOM_HEIGHT = 0.5f;

    private struct Body
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Dir;
        public bool IsGrounded;
    }

    private static Vector2 sensitivity = new(0.001f, 0.001f);

    private static Body player;
    private static Vector2 lookRotation;
    private static float headTimer;
    private static float walkLerp;
    private static float headLerp = STAND_HEIGHT;
    private static Vector2 lean;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] 3d camera fps");

        Camera3D camera = default;
        camera.FovY = 60.0f;
        camera.Projection = CameraProjection.Perspective;
        camera.Position = new Vector3(player.Position.X, player.Position.Y + (BOTTOM_HEIGHT + headLerp), player.Position.Z);

        UpdateCameraFPS(ref camera);

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            Vector2 mouseDelta = GetMouseDelta();
            lookRotation.X -= mouseDelta.X*sensitivity.X;
            lookRotation.Y += mouseDelta.Y*sensitivity.Y;

            int sideway = (IsKeyDown(Key.D) ? 1 : 0) - (IsKeyDown(Key.A) ? 1 : 0);
            int forward = (IsKeyDown(Key.W) ? 1 : 0) - (IsKeyDown(Key.S) ? 1 : 0);
            bool crouching = IsKeyDown(Key.LCtrl);
            UpdateBody(ref player, lookRotation.X, sideway, forward, IsKeyPressed(Key.Space), crouching);

            float delta = GetFrameTime();
            headLerp = float.Lerp(headLerp, crouching ? CROUCH_HEIGHT : STAND_HEIGHT, 20.0f*delta);
            camera.Position = new Vector3(player.Position.X, player.Position.Y + (BOTTOM_HEIGHT + headLerp), player.Position.Z);

            if (player.IsGrounded && ((forward != 0) || (sideway != 0)))
            {
                headTimer += delta*3.0f;
                walkLerp = float.Lerp(walkLerp, 1.0f, 10.0f*delta);
                camera.FovY = float.Lerp(camera.FovY, 55.0f, 5.0f*delta);
            }
            else
            {
                walkLerp = float.Lerp(walkLerp, 0.0f, 10.0f*delta);
                camera.FovY = float.Lerp(camera.FovY, 60.0f, 5.0f*delta);
            }

            lean.X = float.Lerp(lean.X, sideway*0.02f, 10.0f*delta);
            lean.Y = float.Lerp(lean.Y, forward*0.015f, 10.0f*delta);

            UpdateCameraFPS(ref camera);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    DrawLevel();
                EndMode3D();

                DrawRectangle(5, 5, 330, 75, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines(5, 5, 330, 75, Color.Blue);

                DrawText("Camera controls:", 15, 15, 10, Color.Black);
                DrawText("- Move keys: W, A, S, D, Space, Left-Ctrl", 15, 30, 10, Color.Black);
                DrawText("- Look around: arrow keys or mouse", 15, 45, 10, Color.Black);
                DrawText($"- Velocity Len: ({new Vector2(player.Velocity.X, player.Velocity.Z).Length():00.000})", 15, 60, 10, Color.Black);

            EndDrawing();
        }

        CloseWindow();
    }

    private static void UpdateBody(ref Body body, float rot, int side, int forward, bool jumpPressed, bool crouchHold)
    {
        Vector2 input = new(side, -forward);

        // raylib's example defines NORMALIZE_INPUT, as 0, and asks only whether it is defined.
        if ((side != 0) && (forward != 0)) input = Vector2.Normalize(input);

        float delta = GetFrameTime();

        if (!body.IsGrounded) body.Velocity.Y -= GRAVITY*delta;

        if (body.IsGrounded && jumpPressed)
        {
            body.Velocity.Y = JUMP_FORCE;
            body.IsGrounded = false;
        }

        Vector3 front = new(MathF.Sin(rot), 0.0f, MathF.Cos(rot));
        Vector3 right = new(MathF.Cos(-rot), 0.0f, MathF.Sin(-rot));

        Vector3 desiredDir = new(input.X*right.X + input.Y*front.X, 0.0f, input.X*right.Z + input.Y*front.Z);
        body.Dir = Vector3.Lerp(body.Dir, desiredDir, CONTROL*delta);

        float decel = body.IsGrounded ? FRICTION : AIR_DRAG;
        Vector3 hvel = new(body.Velocity.X*decel, 0.0f, body.Velocity.Z*decel);

        if (hvel.Length() < (MAX_SPEED*0.01f)) hvel = Vector3.Zero;

        float speed = Vector3.Dot(hvel, body.Dir);

        float maxSpeed = crouchHold ? CROUCH_SPEED : MAX_SPEED;
        float accel = Math.Clamp(maxSpeed - speed, 0.0f, MAX_ACCEL*delta);
        hvel.X += body.Dir.X*accel;
        hvel.Z += body.Dir.Z*accel;

        body.Velocity.X = hvel.X;
        body.Velocity.Z = hvel.Z;

        body.Position += body.Velocity*delta;

        if (body.Position.Y <= 0.0f)
        {
            body.Position.Y = 0.0f;
            body.Velocity.Y = 0.0f;
            body.IsGrounded = true;
        }
    }

    // raymath's Vector3RotateByAxisAngle and Vector3Angle.
    private static Vector3 Rotate(Vector3 v, Vector3 axis, float angle) =>
        Vector3.Transform(v, Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), angle));

    private static float Angle(Vector3 a, Vector3 b) => MathF.Atan2(Vector3.Cross(a, b).Length(), Vector3.Dot(a, b));

    private static void UpdateCameraFPS(ref Camera3D camera)
    {
        Vector3 up = new(0.0f, 1.0f, 0.0f);
        Vector3 targetOffset = new(0.0f, 0.0f, -1.0f);

        Vector3 yaw = Rotate(targetOffset, up, lookRotation.X);

        // The view is held a thousandth short of straight up and down.
        float maxAngleUp = Angle(up, yaw) - 0.001f;
        if (-lookRotation.Y > maxAngleUp) lookRotation.Y = -maxAngleUp;

        float maxAngleDown = -Angle(-up, yaw) + 0.001f;
        if (-lookRotation.Y < maxAngleDown) lookRotation.Y = -maxAngleDown;

        Vector3 right = Vector3.Normalize(Vector3.Cross(yaw, up));

        float pitchAngle = -lookRotation.Y - lean.Y;
        pitchAngle = Math.Clamp(pitchAngle, -MathF.PI/2 + 0.0001f, MathF.PI/2 - 0.0001f);
        Vector3 pitch = Rotate(yaw, right, pitchAngle);

        float headSin = MathF.Sin(headTimer*MathF.PI);
        float headCos = MathF.Cos(headTimer*MathF.PI);
        const float stepRotation = 0.01f;
        camera.Up = Rotate(up, pitch, headSin*stepRotation + lean.X);

        const float bobSide = 0.1f;
        const float bobUp = 0.15f;
        Vector3 bobbing = right*(headSin*bobSide);
        bobbing.Y = MathF.Abs(headCos*bobUp);

        camera.Position += bobbing*walkLerp;
        camera.Target = camera.Position + pitch;
    }

    private static void DrawLevel()
    {
        const int floorExtent = 25;
        const float tileSize = 5.0f;
        Color tileColor1 = new(150, 200, 200, 255);

        for (int y = -floorExtent; y < floorExtent; y++)
        {
            for (int x = -floorExtent; x < floorExtent; x++)
            {
                if ((y & 1) != 0 && (x & 1) != 0)
                    DrawPlane(new Vector3(x*tileSize, 0.0f, y*tileSize), new Vector2(tileSize, tileSize), tileColor1);
                else if ((y & 1) == 0 && (x & 1) == 0)
                    DrawPlane(new Vector3(x*tileSize, 0.0f, y*tileSize), new Vector2(tileSize, tileSize), Color.LightGray);
            }
        }

        Vector3 towerSize = new(16.0f, 32.0f, 16.0f);
        Color towerColor = new(150, 200, 200, 255);

        Vector3 towerPos = new(16.0f, 16.0f, 16.0f);
        DrawCubeV(towerPos, towerSize, towerColor);
        DrawCubeWiresV(towerPos, towerSize, Color.DarkBlue);

        towerPos.X *= -1;
        DrawCubeV(towerPos, towerSize, towerColor);
        DrawCubeWiresV(towerPos, towerSize, Color.DarkBlue);

        towerPos.Z *= -1;
        DrawCubeV(towerPos, towerSize, towerColor);
        DrawCubeWiresV(towerPos, towerSize, Color.DarkBlue);

        towerPos.X *= -1;
        DrawCubeV(towerPos, towerSize, towerColor);
        DrawCubeWiresV(towerPos, towerSize, Color.DarkBlue);

        DrawSphere(new Vector3(300.0f, 300.0f, 0.0f), 100.0f, new Color(255, 0, 0, 255));
    }
}

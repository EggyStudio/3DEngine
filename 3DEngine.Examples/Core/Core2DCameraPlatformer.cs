// raylib's core_2d_camera_platformer example, Copyright (c) 2019-2025 arvyy (@arvyy), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Core2DCameraPlatformer
{
    private const int G = 400;
    private const float PLAYER_JUMP_SPD = 350.0f;
    private const float PLAYER_HOR_SPD = 200.0f;

    private struct Player
    {
        public Vector2 position;
        public float speed;
        public bool canJump;
    }

    private readonly record struct EnvItem(Rectangle rect, int blocking, Color color);

    private delegate void CameraUpdater(ref Camera2D camera, ref Player player, EnvItem[] envItems, int envItemsLength, float delta, int width, int height);

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] 2d camera platformer");

        Player player = default;
        player.position = new Vector2(400, 280);
        player.speed = 0;
        player.canJump = false;
        EnvItem[] envItems =
        [
            new(new Rectangle(0, 0, 1000, 400), 0, Color.LightGray),
            new(new Rectangle(0, 400, 1000, 200), 1, Color.Gray),
            new(new Rectangle(300, 200, 400, 10), 1, Color.Gray),
            new(new Rectangle(250, 300, 100, 10), 1, Color.Gray),
            new(new Rectangle(650, 300, 100, 10), 1, Color.Gray),
        ];

        int envItemsLength = envItems.Length;

        Camera2D camera = default;
        camera.Target = player.position;
        camera.Offset = new Vector2(screenWidth/2.0f, screenHeight/2.0f);
        camera.Rotation = 0.0f;
        camera.Zoom = 1.0f;

        CameraUpdater[] cameraUpdaters =
        [
            UpdateCameraCenter,
            UpdateCameraCenterInsideMap,
            UpdateCameraCenterSmoothFollow,
            UpdateCameraEvenOutOnLanding,
            UpdateCameraPlayerBoundsPush,
        ];

        int cameraOption = 0;
        int cameraUpdatersLength = cameraUpdaters.Length;

        string[] cameraDescriptions =
        [
            "Follow player center",
            "Follow player center, but clamp to map edges",
            "Follow player center; smoothed",
            "Follow player center horizontally; update player center vertically after landing",
            "Player push camera on getting too close to screen edge",
        ];

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float deltaTime = GetFrameTime();

            UpdatePlayer(ref player, envItems, envItemsLength, deltaTime);

            camera.Zoom += ((float)GetMouseWheelMove()*0.05f);

            if (camera.Zoom > 3.0f) camera.Zoom = 3.0f;
            else if (camera.Zoom < 0.25f) camera.Zoom = 0.25f;

            if (IsKeyPressed(Key.R))
            {
                camera.Zoom = 1.0f;
                player.position = new Vector2(400, 280);
            }

            if (IsKeyPressed(Key.C)) cameraOption = (cameraOption + 1)%cameraUpdatersLength;

            cameraUpdaters[cameraOption](ref camera, ref player, envItems, envItemsLength, deltaTime, screenWidth, screenHeight);

            BeginDrawing();

                ClearBackground(Color.LightGray);

                BeginMode2D(camera);

                    for (int i = 0; i < envItemsLength; i++) DrawRectangleRec(envItems[i].rect, envItems[i].color);

                    Rectangle playerRect = new(player.position.X - 20, player.position.Y - 40, 40.0f, 40.0f);
                    DrawRectangleRec(playerRect, Color.Red);

                    DrawCircleV(player.position, 5.0f, Color.Gold);

                EndMode2D();

                DrawText("Controls:", 20, 20, 10, Color.Black);
                DrawText("- Right/Left to move", 40, 40, 10, Color.DarkGray);
                DrawText("- Space to jump", 40, 60, 10, Color.DarkGray);
                DrawText("- Mouse Wheel to Zoom in-out", 40, 80, 10, Color.DarkGray);
                DrawText("- R to reset position + zoom", 40, 100, 10, Color.DarkGray);
                DrawText("- C to change camera mode", 40, 120, 10, Color.DarkGray);
                DrawText("Current camera mode:", 20, 140, 10, Color.Black);
                DrawText(cameraDescriptions[cameraOption], 40, 160, 10, Color.DarkGray);

            EndDrawing();
        }

        CloseWindow();
    }

    private static void UpdatePlayer(ref Player player, EnvItem[] envItems, int envItemsLength, float delta)
    {
        if (IsKeyDown(Key.Left)) player.position.X -= PLAYER_HOR_SPD*delta;
        if (IsKeyDown(Key.Right)) player.position.X += PLAYER_HOR_SPD*delta;
        if (IsKeyDown(Key.Space) && player.canJump)
        {
            player.speed = -PLAYER_JUMP_SPD;
            player.canJump = false;
        }

        bool hitObstacle = false;
        for (int i = 0; i < envItemsLength; i++)
        {
            EnvItem ei = envItems[i];
            ref Vector2 p = ref player.position;
            if (ei.blocking != 0 &&
                ei.rect.X <= p.X &&
                ei.rect.X + ei.rect.Width >= p.X &&
                ei.rect.Y >= p.Y &&
                ei.rect.Y <= p.Y + player.speed*delta)
            {
                hitObstacle = true;
                player.speed = 0.0f;
                p.Y = ei.rect.Y;
                break;
            }
        }

        if (!hitObstacle)
        {
            player.position.Y += player.speed*delta;
            player.speed += G*delta;
            player.canJump = false;
        }
        else player.canJump = true;
    }

    private static void UpdateCameraCenter(ref Camera2D camera, ref Player player, EnvItem[] envItems, int envItemsLength, float delta, int width, int height)
    {
        camera.Offset = new Vector2(width/2.0f, height/2.0f);
        camera.Target = player.position;
    }

    private static void UpdateCameraCenterInsideMap(ref Camera2D camera, ref Player player, EnvItem[] envItems, int envItemsLength, float delta, int width, int height)
    {
        camera.Target = player.position;
        camera.Offset = new Vector2(width/2.0f, height/2.0f);
        float minX = 1000, minY = 1000, maxX = -1000, maxY = -1000;

        for (int i = 0; i < envItemsLength; i++)
        {
            EnvItem ei = envItems[i];
            minX = MathF.Min(ei.rect.X, minX);
            maxX = MathF.Max(ei.rect.X + ei.rect.Width, maxX);
            minY = MathF.Min(ei.rect.Y, minY);
            maxY = MathF.Max(ei.rect.Y + ei.rect.Height, maxY);
        }

        Vector2 max = GetWorldToScreen2D(new Vector2(maxX, maxY), camera);
        Vector2 min = GetWorldToScreen2D(new Vector2(minX, minY), camera);

        if (max.X < width) camera.Offset = camera.Offset with { X = width - (max.X - (float)width/2) };
        if (max.Y < height) camera.Offset = camera.Offset with { Y = height - (max.Y - (float)height/2) };
        if (min.X > 0) camera.Offset = camera.Offset with { X = (float)width/2 - min.X };
        if (min.Y > 0) camera.Offset = camera.Offset with { Y = (float)height/2 - min.Y };
    }

    private static void UpdateCameraCenterSmoothFollow(ref Camera2D camera, ref Player player, EnvItem[] envItems, int envItemsLength, float delta, int width, int height)
    {
        const float minSpeed = 30;
        const float minEffectLength = 10;
        const float fractionSpeed = 0.8f;

        camera.Offset = new Vector2(width/2.0f, height/2.0f);
        Vector2 diff = player.position - camera.Target;
        float length = diff.Length();

        if (length > minEffectLength)
        {
            float speed = MathF.Max(fractionSpeed*length, minSpeed);
            camera.Target += diff*(speed*delta/length);
        }
    }

    // C's static locals, kept between calls.
    private static bool eveningOut;
    private static float evenOutTarget;

    private static void UpdateCameraEvenOutOnLanding(ref Camera2D camera, ref Player player, EnvItem[] envItems, int envItemsLength, float delta, int width, int height)
    {
        const float evenOutSpeed = 700;

        camera.Offset = new Vector2(width/2.0f, height/2.0f);
        camera.Target = camera.Target with { X = player.position.X };

        if (eveningOut)
        {
            if (evenOutTarget > camera.Target.Y)
            {
                camera.Target = camera.Target with { Y = camera.Target.Y + evenOutSpeed*delta };

                if (camera.Target.Y > evenOutTarget)
                {
                    camera.Target = camera.Target with { Y = evenOutTarget };
                    eveningOut = false;
                }
            }
            else
            {
                camera.Target = camera.Target with { Y = camera.Target.Y - evenOutSpeed*delta };

                if (camera.Target.Y < evenOutTarget)
                {
                    camera.Target = camera.Target with { Y = evenOutTarget };
                    eveningOut = false;
                }
            }
        }
        else
        {
            if (player.canJump && (player.speed == 0) && (player.position.Y != camera.Target.Y))
            {
                eveningOut = true;
                evenOutTarget = player.position.Y;
            }
        }
    }

    private static void UpdateCameraPlayerBoundsPush(ref Camera2D camera, ref Player player, EnvItem[] envItems, int envItemsLength, float delta, int width, int height)
    {
        Vector2 bbox = new(0.2f, 0.2f);

        Vector2 bboxWorldMin = GetScreenToWorld2D(new Vector2((1 - bbox.X)*0.5f*width, (1 - bbox.Y)*0.5f*height), camera);
        Vector2 bboxWorldMax = GetScreenToWorld2D(new Vector2((1 + bbox.X)*0.5f*width, (1 + bbox.Y)*0.5f*height), camera);
        camera.Offset = new Vector2((1 - bbox.X)*0.5f*width, (1 - bbox.Y)*0.5f*height);

        if (player.position.X < bboxWorldMin.X) camera.Target = camera.Target with { X = player.position.X };
        if (player.position.Y < bboxWorldMin.Y) camera.Target = camera.Target with { Y = player.position.Y };
        if (player.position.X > bboxWorldMax.X) camera.Target = camera.Target with { X = bboxWorldMin.X + (player.position.X - bboxWorldMax.X) };
        if (player.position.Y > bboxWorldMax.Y) camera.Target = camera.Target with { Y = bboxWorldMin.Y + (player.position.Y - bboxWorldMax.Y) };
    }
}

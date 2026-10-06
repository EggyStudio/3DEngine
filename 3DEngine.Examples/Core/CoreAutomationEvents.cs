// raylib's core_automation_events example, Copyright (c) 2023-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreAutomationEvents
{
    private const int GRAVITY = 400;
    private const float PLAYER_JUMP_SPD = 350.0f;
    private const float PLAYER_HOR_SPD = 200.0f;

    private const int MAX_ENVIRONMENT_ELEMENTS = 5;

    private struct Player
    {
        public Vector2 position;
        public float speed;
        public bool canJump;
    }

    private readonly record struct EnvElement(Rectangle rect, int blocking, Color color);

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] automation events");

        // The player, as in core_2d_camera_platformer
        Player player = default;
        player.position = new Vector2(400, 280);
        player.speed = 0;
        player.canJump = false;

        EnvElement[] envElements =
        [
            new(new Rectangle(0, 0, 1000, 400), 0, Color.LightGray),
            new(new Rectangle(0, 400, 1000, 200), 1, Color.Gray),
            new(new Rectangle(300, 200, 400, 10), 1, Color.Gray),
            new(new Rectangle(250, 300, 100, 10), 1, Color.Gray),
            new(new Rectangle(650, 300, 100, 10), 1, Color.Gray),
        ];

        Camera2D camera = default;
        camera.Target = player.position;
        camera.Offset = new Vector2(screenWidth/2.0f, screenHeight/2.0f);
        camera.Rotation = 0.0f;
        camera.Zoom = 1.0f;

        // An empty list to record new events into
        AutomationEventList aelist = LoadAutomationEventList(null);
        SetAutomationEventList(aelist);
        bool eventRecording = false;
        bool eventPlaying = false;

        int frameCounter = 0;
        int playFrameCounter = 0;
        int currentPlayFrame = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // A fixed step rather than the frame's time, so a recording plays back the same
            float deltaTime = 0.015f;

            // A file of events dropped on the window is played from the start
            if (IsFileDropped())
            {
                string[] droppedFiles = LoadDroppedFiles();

                if (Path.GetExtension(droppedFiles[0]).ToLowerInvariant() is ".txt" or ".rae")
                {
                    UnloadAutomationEventList(aelist);
                    aelist = LoadAutomationEventList(droppedFiles[0]);

                    eventRecording = false;

                    // Played from the start, with the game set back as it began
                    eventPlaying = true;
                    playFrameCounter = 0;
                    currentPlayFrame = 0;

                    player.position = new Vector2(400, 280);
                    player.speed = 0;
                    player.canJump = false;

                    camera.Target = player.position;
                    camera.Offset = new Vector2(screenWidth/2.0f, screenHeight/2.0f);
                    camera.Rotation = 0.0f;
                    camera.Zoom = 1.0f;
                }

                UnloadDroppedFiles();
            }

            // The player moves and jumps
            if (IsKeyDown(Key.Left)) player.position.X -= PLAYER_HOR_SPD*deltaTime;
            if (IsKeyDown(Key.Right)) player.position.X += PLAYER_HOR_SPD*deltaTime;
            if (IsKeyDown(Key.Space) && player.canJump)
            {
                player.speed = -PLAYER_JUMP_SPD;
                player.canJump = false;
            }

            int hitObstacle = 0;
            for (int i = 0; i < MAX_ENVIRONMENT_ELEMENTS; i++)
            {
                EnvElement element = envElements[i];
                ref Vector2 p = ref player.position;
                if (element.blocking != 0 &&
                    element.rect.X <= p.X &&
                    element.rect.X + element.rect.Width >= p.X &&
                    element.rect.Y >= p.Y &&
                    element.rect.Y <= p.Y + player.speed*deltaTime)
                {
                    hitObstacle = 1;
                    player.speed = 0.0f;
                    p.Y = element.rect.Y;
                }
            }

            if (hitObstacle == 0)
            {
                player.position.Y += player.speed*deltaTime;
                player.speed += GRAVITY*deltaTime;
                player.canJump = false;
            }
            else player.canJump = true;

            if (IsKeyPressed(Key.R))
            {
                // The game set back as it began
                player.position = new Vector2(400, 280);
                player.speed = 0;
                player.canJump = false;

                camera.Target = player.position;
                camera.Offset = new Vector2(screenWidth/2.0f, screenHeight/2.0f);
                camera.Rotation = 0.0f;
                camera.Zoom = 1.0f;
            }

            // The events of this frame played, while playing
            if (eventPlaying)
            {
                // A frame may hold several events, every input of that frame
                while (playFrameCounter == aelist.Events[currentPlayFrame].Frame)
                {
                    PlayAutomationEvent(aelist.Events[currentPlayFrame]);
                    currentPlayFrame++;

                    if (currentPlayFrame == aelist.Count)
                    {
                        eventPlaying = false;
                        currentPlayFrame = 0;
                        playFrameCounter = 0;

                        TraceLog(LogLevel.Info, "FINISH PLAYING!");
                        break;
                    }
                }

                playFrameCounter++;
            }

            // The camera follows the player, kept inside the map
            camera.Target = player.position;
            camera.Offset = new Vector2(screenWidth/2.0f, screenHeight/2.0f);
            float minX = 1000, minY = 1000, maxX = -1000, maxY = -1000;

            // The wheel zooms
            camera.Zoom += GetMouseWheelMove()*0.05f;
            if (camera.Zoom > 3.0f) camera.Zoom = 3.0f;
            else if (camera.Zoom < 0.25f) camera.Zoom = 0.25f;

            for (int i = 0; i < MAX_ENVIRONMENT_ELEMENTS; i++)
            {
                EnvElement element = envElements[i];
                minX = MathF.Min(element.rect.X, minX);
                maxX = MathF.Max(element.rect.X + element.rect.Width, maxX);
                minY = MathF.Min(element.rect.Y, minY);
                maxY = MathF.Max(element.rect.Y + element.rect.Height, maxY);
            }

            Vector2 max = GetWorldToScreen2D(new Vector2(maxX, maxY), camera);
            Vector2 min = GetWorldToScreen2D(new Vector2(minX, minY), camera);

            if (max.X < screenWidth) camera.Offset = camera.Offset with { X = screenWidth - (max.X - (float)screenWidth/2) };
            if (max.Y < screenHeight) camera.Offset = camera.Offset with { Y = screenHeight - (max.Y - (float)screenHeight/2) };
            if (min.X > 0) camera.Offset = camera.Offset with { X = (float)screenWidth/2 - min.X };
            if (min.Y > 0) camera.Offset = camera.Offset with { Y = (float)screenHeight/2 - min.Y };

            // S records and stops, writing the events to automation.rae
            if (IsKeyPressed(Key.S))
            {
                if (!eventPlaying)
                {
                    if (eventRecording)
                    {
                        StopAutomationEventRecording();
                        eventRecording = false;

                        ExportAutomationEventList(aelist, "automation.rae");

                        TraceLog(LogLevel.Info, $"RECORDED FRAMES: {aelist.Count}");
                    }
                    else
                    {
                        SetAutomationEventBaseFrame(180);
                        StartAutomationEventRecording();
                        eventRecording = true;
                    }
                }
            }
            else if (IsKeyPressed(Key.A))
            {
                // A plays what was recorded, from the next frame, with the game set back
                if (!eventRecording && (aelist.Count > 0))
                {
                    eventPlaying = true;
                    playFrameCounter = 0;
                    currentPlayFrame = 0;

                    player.position = new Vector2(400, 280);
                    player.speed = 0;
                    player.canJump = false;

                    camera.Target = player.position;
                    camera.Offset = new Vector2(screenWidth/2.0f, screenHeight/2.0f);
                    camera.Rotation = 0.0f;
                    camera.Zoom = 1.0f;
                }
            }

            if (eventRecording || eventPlaying) frameCounter++;
            else frameCounter = 0;

            BeginDrawing();

                ClearBackground(Color.LightGray);

                BeginMode2D(camera);

                    for (int i = 0; i < MAX_ENVIRONMENT_ELEMENTS; i++) DrawRectangleRec(envElements[i].rect, envElements[i].color);

                    DrawRectangleRec(new Rectangle(player.position.X - 20, player.position.Y - 40, 40, 40), Color.Red);

                EndMode2D();

                // The controls
                DrawRectangle(10, 10, 290, 145, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines(10, 10, 290, 145, Fade(Color.Blue, 0.8f));

                DrawText("Controls:", 20, 20, 10, Color.Black);
                DrawText("- RIGHT | LEFT: Player movement", 30, 40, 10, Color.DarkGray);
                DrawText("- SPACE: Player jump", 30, 60, 10, Color.DarkGray);
                DrawText("- R: Reset game state", 30, 80, 10, Color.DarkGray);

                DrawText("- S: START/STOP RECORDING INPUT EVENTS", 30, 110, 10, Color.Black);
                DrawText("- A: REPLAY LAST RECORDED INPUT EVENTS", 30, 130, 10, Color.Black);

                // Whether it records or plays, blinking
                if (eventRecording)
                {
                    DrawRectangle(10, 160, 290, 30, Fade(Color.Red, 0.3f));
                    DrawRectangleLines(10, 160, 290, 30, Fade(Color.Maroon, 0.8f));
                    DrawCircle(30, 175, 10, Color.Maroon);

                    if (((frameCounter/15)%2) == 1) DrawText($"RECORDING EVENTS... [{aelist.Count}]", 50, 170, 10, Color.Maroon);
                }
                else if (eventPlaying)
                {
                    DrawRectangle(10, 160, 290, 30, Fade(Color.Lime, 0.3f));
                    DrawRectangleLines(10, 160, 290, 30, Fade(Color.DarkGreen, 0.8f));
                    DrawTriangle(new Vector2(20, 155 + 10), new Vector2(20, 155 + 30), new Vector2(40, 155 + 20), Color.DarkGreen);

                    if (((frameCounter/15)%2) == 1) DrawText($"PLAYING RECORDED EVENTS... [{currentPlayFrame}]", 50, 170, 10, Color.DarkGreen);
                }

            EndDrawing();
        }

        CloseWindow();
    }
}

// raylib's models_animation_timing example, Copyright (c) 2026 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsAnimationTiming
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] animation timing");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        Camera3D camera = new(new Vector3(6.0f, 6.0f, 6.0f), new Vector3(0.0f, 2.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Model model = LoadModel("resources/models/gltf/robot.glb");
        Vector3 position = Vector3.Zero;

        ModelAnimation[] anims = LoadModelAnimations("resources/models/gltf/robot.glb");
        int animCount = anims.Length;

        int animIndex = 10;
        float animCurrentFrame = 0.0f;      // A frame between two is a blend of them.
        float animFrameSpeed = 0.5f;
        bool animPause = false;

        // raylib's TextJoin of the clips' names is the combo's list here.
        string[] animNames = anims.Select(anim => anim.Name).ToArray();

        float animFrameProgress = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            if (IsKeyPressed(Key.P)) animPause = !animPause;

            if (!animPause && (animIndex < animCount))
            {
                animCurrentFrame += animFrameSpeed;
                if (animCurrentFrame >= anims[animIndex].KeyframeCount) animCurrentFrame = 0.0f;
                UpdateModelAnimation(model, anims[animIndex], animCurrentFrame);
            }

            // The clip and its speed are picked below, and the bar follows the frame.
            animFrameProgress = animCurrentFrame;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    DrawModel(model, position, 1.0f, Color.White);
                    DrawGrid(10, 1.0f);
                EndMode3D();

                // raygui's controls, as ImGui's, where raygui places them.
                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);

                ImGui.SetCursorScreenPos(new Vector2(10, 10));
                ImGui.SetNextItemWidth(140);
                ImGui.Combo("##animation", ref animIndex, animNames, animNames.Length);

                ImGui.SetCursorScreenPos(new Vector2(260 - ImGui.CalcTextSize("FRAME SPEED:").X - 5, 13));
                ImGui.TextUnformatted("FRAME SPEED:");
                ImGui.SetCursorScreenPos(new Vector2(260, 10));
                ImGui.SetNextItemWidth(500);
                ImGui.SliderFloat("##speed", ref animFrameSpeed, 0.1f, 2.0f, "x%.1f");

                // The clip's timeline, with a mark at each of its frames
                ImGui.SetCursorScreenPos(new Vector2(10, GetScreenHeight() - 64.0f + 5));
                ImGui.TextUnformatted($"CURRENT FRAME: {animFrameProgress:0.00} / {anims[animIndex].KeyframeCount}");
                ImGui.SetCursorScreenPos(new Vector2(10, GetScreenHeight() - 40.0f));
                ImGui.ProgressBar(animFrameProgress/anims[animIndex].KeyframeCount, new Vector2(GetScreenWidth() - 20.0f, 24), "");

                // The marks go through ImGui's draw list, so they lie over the bar as raylib's do,
                // ImGui being drawn over everything else
                uint blue = ImGui.GetColorU32(new Vector4(Color.Blue.R/255.0f, Color.Blue.G/255.0f, Color.Blue.B/255.0f, 1.0f));
                for (int i = 0; i < anims[animIndex].KeyframeCount; i++)
                {
                    int x = 10 + (int)(((float)(GetScreenWidth() - 20)/(float)anims[animIndex].KeyframeCount)*(float)i);
                    ImGui.GetWindowDrawList().AddRectFilled(new Vector2(x, GetScreenHeight() - 40), new Vector2(x + 1, GetScreenHeight() - 40 + 24), blue);
                }

                ImGui.End();

            EndDrawing();
        }

        UnloadModelAnimations(anims);
        UnloadModel(model);

        CloseWindow();
    }
}

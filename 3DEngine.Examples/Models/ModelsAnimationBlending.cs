// raylib's models_animation_blending example, Copyright (c) 2024-2026 Kirandeep
// (@Kirandeep-Singh-Khehra) and Ramon Santamaria (@raysan5), under the zlib license, written again for
// the flat API, with ImGui in raygui's place.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsAnimationBlending
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] animation blending");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        Camera3D camera = new(new Vector3(6.0f, 6.0f, 6.0f), new Vector3(0.0f, 2.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // Load model
        Model model = LoadModel("resources/models/gltf/robot.glb"); // Load character model
        Vector3 position = new(0.0f, 0.0f, 0.0f); // Set model world position

        // raylib's skinning shader, which raylib built with SUPPORT_GPU_SKINNING needs, written in
        // Slang for the model pass, on every material. A skinned mesh is posed on the GPU here
        // whatever its shader.
        Shader skinningShader = LoadShader("resources/shaders/slang/skinning.slang");
        for (int i = 0; i < model.Materials.Length; i++) model.Materials[i].Shader = skinningShader;

        // Load model animations
        ModelAnimation[] anims = LoadModelAnimations("resources/models/gltf/robot.glb");
        int animCount = anims.Length;

        // Animation playing variables
        // NOTE: Two animations are played with a smooth transition between them
        int currentAnimPlaying = 0;         // Current animation playing (0 o 1)
        int nextAnimToPlay = 1;             // Next animation to play (to transition)
        bool animTransition = false;        // Flag to register anim transition state

        int animIndex0 = 10;                // Current animation playing (walking)
        float animCurrentFrame0 = 0.0f;     // Current animation frame (supporting interpolated frames)
        float animFrameSpeed0 = 0.5f;       // Current animation play speed
        int animIndex1 = 6;                 // Next animation to play (running)
        float animCurrentFrame1 = 0.0f;     // Next animation frame (supporting interpolated frames)
        float animFrameSpeed1 = 0.5f;       // Next animation play speed

        float animBlendFactor = 0.0f;       // Blend factor from anim0[frame0] --> anim1[frame1], [0.0f..1.0f]
                                            // NOTE: 0.0f results in full anim0[] and 1.0f in full anim1[]

        float animBlendTime = 2.0f;         // Time to blend from one playing animation to another (in seconds)
        float animBlendTimeCounter = 0.0f;  // Time counter (delta time)

        bool animPause = false;             // Pause animation

        // UI required variables
        string[] animNames = [.. anims.Select(anim => anim.Name)];  // Animation names for dropdown box

        bool dropdownEditMode0 = false;
        bool dropdownEditMode1 = false;
        float animFrameProgress0 = 0.0f;
        float animFrameProgress1 = 0.0f;
        float animBlendProgress = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            if (IsKeyPressed(Key.P)) animPause = !animPause;

            if (!animPause)
            {
                // Start transition from anim0[] to anim1[]
                if (IsKeyPressed(Key.Space) && !animTransition)
                {
                    if (currentAnimPlaying == 0)
                    {
                        // Transition anim0 --> anim1
                        nextAnimToPlay = 1;
                        animCurrentFrame1 = 0.0f;
                    }
                    else
                    {
                        // Transition anim1 --> anim0
                        nextAnimToPlay = 0;
                        animCurrentFrame0 = 0.0f;
                    }

                    // Set animation transition
                    animTransition = true;
                    animBlendTimeCounter = 0.0f;
                    animBlendFactor = 0.0f;
                }

                if (animTransition)
                {
                    // Playing anim0 and anim1 at the same time
                    animCurrentFrame0 += animFrameSpeed0;
                    if (animCurrentFrame0 >= anims[animIndex0].FrameCount) animCurrentFrame0 = 0.0f;
                    animCurrentFrame1 += animFrameSpeed1;
                    if (animCurrentFrame1 >= anims[animIndex1].FrameCount) animCurrentFrame1 = 0.0f;

                    // Increment blend factor over time to transition from anim0 --> anim1 over time
                    // NOTE: Time blending could be other than linear, using some easing
                    animBlendFactor = animBlendTimeCounter/animBlendTime;
                    animBlendTimeCounter += GetFrameTime();
                    animBlendProgress = animBlendFactor;

                    // Update model with animations blending
                    if (nextAnimToPlay == 1)
                    {
                        // Blend anim0 --> anim1
                        UpdateModelAnimationEx(model, anims[animIndex0], animCurrentFrame0,
                            anims[animIndex1], animCurrentFrame1, animBlendFactor);
                    }
                    else
                    {
                        // Blend anim1 --> anim0
                        UpdateModelAnimationEx(model, anims[animIndex1], animCurrentFrame1,
                            anims[animIndex0], animCurrentFrame0, animBlendFactor);
                    }

                    // Check if transition completed
                    if (animBlendFactor > 1.0f)
                    {
                        // Reset frame states
                        if (currentAnimPlaying == 0) animCurrentFrame0 = 0.0f;
                        else if (currentAnimPlaying == 1) animCurrentFrame1 = 0.0f;
                        currentAnimPlaying = nextAnimToPlay; // Update current animation playing

                        animBlendFactor = 0.0f; // Reset blend factor
                        animTransition = false; // Exit transition mode
                        animBlendTimeCounter = 0.0f;
                    }
                }
                else
                {
                    // Play only one anim, the current one
                    if (currentAnimPlaying == 0)
                    {
                        // Playing anim0 at defined speed
                        animCurrentFrame0 += animFrameSpeed0;
                        if (animCurrentFrame0 >= anims[animIndex0].FrameCount) animCurrentFrame0 = 0.0f;
                        UpdateModelAnimation(model, anims[animIndex0], animCurrentFrame0);
                    }
                    else if (currentAnimPlaying == 1)
                    {
                        // Playing anim1 at defined speed
                        animCurrentFrame1 += animFrameSpeed1;
                        if (animCurrentFrame1 >= anims[animIndex1].FrameCount) animCurrentFrame1 = 0.0f;
                        UpdateModelAnimation(model, anims[animIndex1], animCurrentFrame1);
                    }
                }
            }

            // Update progress bars values with current frame for each animation
            animFrameProgress0 = animCurrentFrame0;
            animFrameProgress1 = animCurrentFrame1;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawModel(model, position, 1.0f, Color.White); // Draw animated model

                    DrawGrid(10, 1.0f);

                EndMode3D();

                if (animTransition) DrawText("ANIM TRANSITION BLENDING!", 170, 50, 30, Color.Blue);

                // raygui's controls, as ImGui's, where raygui places them, at raygui's text size of 10
                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
                ImGui.SetWindowFontScale(10.0f/13.0f);

                ImGui.BeginDisabled(dropdownEditMode0);
                Slider(new Rectangle(10, 38, 160, 12), null, $"x{animFrameSpeed0:0.0}", "##speed0", ref animFrameSpeed0, 0.1f, 2.0f);
                ImGui.EndDisabled();
                ImGui.BeginDisabled(dropdownEditMode1);
                Slider(new Rectangle(GetScreenWidth() - 170.0f, 38, 160, 12), $"{animFrameSpeed1:0.0}x", null, "##speed1", ref animFrameSpeed1, 0.1f, 2.0f);
                ImGui.EndDisabled();

                // Draw animation selectors for blending transition
                // NOTE: Transition does not start until requested
                dropdownEditMode0 = DropdownBox(new Rectangle(10, 10, 160, 24), "##anim0", animNames, ref animIndex0);

                // Blending process progress bar, filling from the right on the way back
                ProgressBar(new Rectangle(180, 14, 440, 16), null, null, animBlendProgress, 0.0f, 1.0f, rightToLeft: nextAnimToPlay != 1);

                dropdownEditMode1 = DropdownBox(new Rectangle(GetScreenWidth() - 170.0f, 10, 160, 24), "##anim1", animNames, ref animIndex1);

                // raygui's label at twice its text size, centered across the window
                ImGui.SetWindowFontScale(20.0f/13.0f);
                const string pressSpace = "PRESS SPACE to START BLENDING";
                Vector2 labelSize = ImGui.CalcTextSize(pressSpace);
                ImGui.SetCursorScreenPos(new Vector2((GetScreenWidth() - labelSize.X)/2, GetScreenHeight() - 100.0f + (40 - labelSize.Y)/2));
                ImGui.TextUnformatted(pressSpace);
                ImGui.SetWindowFontScale(10.0f/13.0f);

                // Draw playing timeline with keyframes for anim0[]
                ProgressBar(new Rectangle(60, GetScreenHeight() - 60.0f, GetScreenWidth() - 180.0f, 20), "ANIM 0",
                    $"FRAME: {animFrameProgress0:0.00} / {anims[animIndex0].FrameCount}",
                    animFrameProgress0, 0.0f, (float)anims[animIndex0].FrameCount);
                for (int i = 0; i < anims[animIndex0].FrameCount; i++)
                    KeyframeMark(60 + (int)(((float)(GetScreenWidth() - 180)/(float)anims[animIndex0].FrameCount)*(float)i),
                        GetScreenHeight() - 60, 20);

                // Draw playing timeline with keyframes for anim1[]
                ProgressBar(new Rectangle(60, GetScreenHeight() - 30.0f, GetScreenWidth() - 180.0f, 20), "ANIM 1",
                    $"FRAME: {animFrameProgress1:0.00} / {anims[animIndex1].FrameCount}",
                    animFrameProgress1, 0.0f, (float)anims[animIndex1].FrameCount);
                for (int i = 0; i < anims[animIndex1].FrameCount; i++)
                    KeyframeMark(60 + (int)(((float)(GetScreenWidth() - 180)/(float)anims[animIndex1].FrameCount)*(float)i),
                        GetScreenHeight() - 30, 20);

                ImGui.End();

            EndDrawing();
        }

        UnloadModelAnimations(anims);   // Unload model animation
        UnloadModel(model);             // Unload model and meshes/material
        UnloadShader(skinningShader);   // Unload GPU skinning shader

        CloseWindow();
    }

    // raygui's slider, as ImGui's at the rectangle, with raygui's texts either side of it
    private static void Slider(Rectangle bounds, string? textLeft, string? textRight, string id, ref float value, float min, float max)
    {
        SideTexts(bounds, textLeft, textRight);
        ImGui.SetCursorScreenPos(new Vector2(bounds.X, bounds.Y));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, (bounds.Height - ImGui.GetFontSize())/2));
        ImGui.SetNextItemWidth(bounds.Width);
        ImGui.SliderFloat(id, ref value, min, max, "");
        ImGui.PopStyleVar();
    }

    // raygui's dropdown box, as ImGui's combo at the rectangle, answering whether its list is open
    private static bool DropdownBox(Rectangle bounds, string id, string[] items, ref int active)
    {
        ImGui.SetCursorScreenPos(new Vector2(bounds.X, bounds.Y));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, (bounds.Height - ImGui.GetFontSize())/2));
        ImGui.SetNextItemWidth(bounds.Width);
        bool open = ImGui.BeginCombo(id, items[active]);
        ImGui.PopStyleVar();
        if (open)
        {
            for (int i = 0; i < items.Length; i++)
                if (ImGui.Selectable(items[i], i == active)) active = i;
            ImGui.EndCombo();
        }
        return open;
    }

    // raygui's progress bar, a frame filled from the left, or from the right as raygui's
    // PROGRESS_SIDE sets it, drawn through ImGui's draw list, with raygui's texts either side of it
    private static void ProgressBar(Rectangle bounds, string? textLeft, string? textRight, float value, float min, float max, bool rightToLeft = false)
    {
        SideTexts(bounds, textLeft, textRight);
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        Vector2 from = new(bounds.X, bounds.Y);
        Vector2 to = new(bounds.X + bounds.Width, bounds.Y + bounds.Height);
        drawList.AddRectFilled(from, to, ImGui.GetColorU32(ImGuiCol.FrameBg));
        float filled = (bounds.Width - 2)*Math.Clamp((value - min)/(max - min), 0.0f, 1.0f);
        if (filled > 0)
        {
            Vector2 fillFrom = rightToLeft ? new Vector2(to.X - 1 - filled, from.Y + 1) : from + Vector2.One;
            drawList.AddRectFilled(fillFrom, fillFrom + new Vector2(filled, bounds.Height - 2), ImGui.GetColorU32(ImGuiCol.PlotHistogram));
        }
        drawList.AddRect(from, to, ImGui.GetColorU32(ImGuiCol.Border));
    }

    // A keyframe's mark on a timeline, raylib's blue, through ImGui's draw list so it lies over the
    // bar, as raylib draws it after the bar
    private static void KeyframeMark(int x, int y, int height)
    {
        Color blue = Color.Blue;
        ImGui.GetWindowDrawList().AddRectFilled(new Vector2(x, y), new Vector2(x + 1, y + height),
            ImGui.GetColorU32(new Vector4(blue.R/255.0f, blue.G/255.0f, blue.B/255.0f, 1.0f)));
    }

    // The texts raygui draws to the left and the right of a control, centered down its height
    private static void SideTexts(Rectangle bounds, string? textLeft, string? textRight)
    {
        if (textLeft is not null)
        {
            Vector2 size = ImGui.CalcTextSize(textLeft);
            ImGui.SetCursorScreenPos(new Vector2(bounds.X - size.X - 4, bounds.Y + (bounds.Height - size.Y)/2));
            ImGui.TextUnformatted(textLeft);
        }
        if (textRight is not null)
        {
            Vector2 size = ImGui.CalcTextSize(textRight);
            ImGui.SetCursorScreenPos(new Vector2(bounds.X + bounds.Width + 4, bounds.Y + (bounds.Height - size.Y)/2));
            ImGui.TextUnformatted(textRight);
        }
    }
}

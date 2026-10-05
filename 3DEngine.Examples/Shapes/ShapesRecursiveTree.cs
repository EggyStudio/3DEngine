// raylib's shapes_recursive_tree example, Copyright (c) 2025 Jopestpe (@jopestpe), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesRecursiveTree
{
    // #define RAYGUI_IMPLEMENTATION typedef struct {

    private struct Branch
    {
        public Vector2 start;
        public Vector2 end;
        public float angle;
        public float length;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] recursive tree");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        Vector2 start = new((screenWidth/2.0f) - 125.0f, (float)screenHeight);
        float angle = 40.0f;
        float thick = 1.0f;
        float treeDepth = 10.0f;
        float branchDecay = 0.66f;
        float length = 120.0f;
        bool bezier = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float theta = angle*(MathF.PI/180);
            int maxBranches = (int)(MathF.Pow(2, MathF.Floor(treeDepth)));
            Branch[] branches = new Branch[1030];
            int count = 0;

            Vector2 initialEnd = new(start.X + length*MathF.Sin(0.0f), start.Y - length*MathF.Cos(0.0f));
            branches[count++] = new Branch { start = start, end = initialEnd, angle = 0.0f, length = length };

            for (int i = 0; i < count; i++)
            {
                Branch branch = branches[i];
                if (branch.length < 2) continue;

                float nextLength = branch.length*branchDecay;

                if (count < maxBranches && nextLength >= 2)
                {
                    Vector2 branchStart = branch.end;

                    float angle1 = branch.angle + theta;
                    Vector2 branchEnd1 = new(branchStart.X + nextLength*MathF.Sin(angle1), branchStart.Y - nextLength*MathF.Cos(angle1));
                    branches[count++] = new Branch { start = branchStart, end = branchEnd1, angle = angle1, length = nextLength };

                    float angle2 = branch.angle - theta;
                    Vector2 branchEnd2 = new(branchStart.X + nextLength*MathF.Sin(angle2), branchStart.Y - nextLength*MathF.Cos(angle2));
                    branches[count++] = new Branch { start = branchStart, end = branchEnd2, angle = angle2, length = nextLength };
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int i = 0; i < count; i++)
                {
                    Branch branch = branches[i];
                    if (branch.length >= 2)
                    {
                        if (bezier) DrawLineBezier(branch.start, branch.end, thick, Color.Red);
                        else DrawLineEx(branch.start, branch.end, thick, Color.Red);
                    }
                }

                DrawLine(580, 0, 580, GetScreenHeight(), new Color(218, 218, 218, 255));
                DrawRectangle(580, 0, GetScreenWidth(), GetScreenHeight(), new Color(232, 232, 232, 255));

                // raygui's controls, as ImGui's, where raygui places them.
                ImGui.SetNextWindowPos(new Vector2(640 - 90, 40));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("Angle", ref angle, 0, 180, "%.2f");
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("Length", ref length, 12.0f, 240.0f, "%.2f");
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("Decay", ref branchDecay, 0.1f, 0.78f, "%.2f");
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("Depth", ref treeDepth, 1.0f, 10.0f, "%.2f");
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("Thick", ref thick, 1, 8, "%.2f");
                ImGui.Checkbox("Bezier", ref bezier);
                ImGui.End();

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}

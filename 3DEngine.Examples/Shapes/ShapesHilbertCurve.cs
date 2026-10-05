// raylib's shapes_hilbert_curve example, Copyright (c) 2025 Hamza RAHAL (@hmz-rhl), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesHilbertCurve
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] hilbert curve");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        int order = 2;
        float size = (float)GetScreenHeight();
        Vector2[] hilbertPath = LoadHilbertPath(order, size, out int strokeCount);

        int prevOrder = order;
        int prevSize = (int)size;
        int counter = 0;
        float thick = 2.0f;
        bool animate = true;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if ((prevOrder != order) || (prevSize != (int)size))
            {
                hilbertPath = LoadHilbertPath(order, size, out strokeCount);

                if (animate) counter = 0;
                else counter = strokeCount;

                prevOrder = order;
                prevSize = (int)size;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (counter < strokeCount)
                {
                    for (int i = 1; i <= counter; i++)
                    {
                        DrawLineEx(hilbertPath[i], hilbertPath[i - 1], thick, ColorFromHSV(((float)i/strokeCount)*360.0f, 1.0f, 1.0f));
                    }

                    counter += 1;
                }
                else
                {
                    for (int i = 1; i < strokeCount; i++)
                    {
                        DrawLineEx(hilbertPath[i], hilbertPath[i - 1], thick, ColorFromHSV(((float)i/strokeCount)*360.0f, 1.0f, 1.0f));
                    }
                }

                // raygui's controls, as ImGui's, where raygui places them.
                ImGui.SetNextWindowPos(new Vector2(450, 50));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.Checkbox("ANIMATE GENERATION ON CHANGE", ref animate);
                ImGui.SetNextItemWidth(120);
                if (ImGui.InputInt("HILBERT CURVE ORDER", ref order)) order = Math.Clamp(order, 2, 8);
                ImGui.SetNextItemWidth(160);
                ImGui.SliderFloat("THICKNESS", ref thick, 1.0f, 10.0f, "%.2f");
                ImGui.SetNextItemWidth(160);
                ImGui.SliderFloat("TOTAL SIZE", ref size, 10.0f, GetScreenHeight()*1.5f, "%.2f");
                ImGui.End();

            EndDrawing();
        }

        CloseWindow();
    }

    private static Vector2[] LoadHilbertPath(int order, float size, out int strokeCount)
    {
        int N = 1 << order;
        float len = size/N;
        strokeCount = N*N;

        Vector2[] hilbertPath = new Vector2[strokeCount];

        for (int i = 0; i < strokeCount; i++)
        {
            hilbertPath[i] = ComputeHilbertStep(order, i);
            hilbertPath[i].X = hilbertPath[i].X*len + len/2.0f;
            hilbertPath[i].Y = hilbertPath[i].Y*len + len/2.0f;
        }

        return hilbertPath;
    }

    private static readonly Vector2[] hilbertPoints = [new(0, 0), new(0, 1), new(1, 1), new(1, 0)];

    private static Vector2 ComputeHilbertStep(int order, int index)
    {
        int hilbertIndex = index & 3;
        Vector2 vect = hilbertPoints[hilbertIndex];
        float temp = 0.0f;
        int len = 0;

        for (int j = 1; j < order; j++)
        {
            index = index >> 2;
            hilbertIndex = index & 3;
            len = 1 << j;

            switch (hilbertIndex)
            {
                case 0:
                    temp = vect.X;
                    vect.X = vect.Y;
                    vect.Y = temp;
                    break;
                // raylib's case 2 falls through to case 1.
                case 2:
                    vect.X += len;
                    vect.Y += len;
                    break;
                case 1:
                    vect.Y += len;
                    break;
                case 3:
                    temp = len - 1 - vect.X;
                    vect.X = 2*len - 1 - vect.Y;
                    vect.Y = temp;
                    break;
                default: break;
            }
        }

        return vect;
    }
}

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class GuiImGuiWindow
{
    public static void Run()
    {
        InitWindow(800, 450, "[gui] imgui window");

        var camera = new Camera3D(new Vector3(6, 6, 6), Vector3.Zero, Vector3.UnitY, 45);
        var size = 2f;
        var color = new Vector3(0.9f, 0.16f, 0.22f);
        var wires = true;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Free);

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            BeginMode3D(camera);
            var cube = new Color((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255));
            DrawCube(Vector3.Zero, size, size, size, cube);
            if (wires) DrawCubeWires(Vector3.Zero, size, size, size, Color.Black);
            DrawGrid(10, 1);
            EndMode3D();

            // ImGui works anywhere between BeginDrawing and EndDrawing. The dock space covers the
            // window and lets the scene show through its middle, so a window dragged to an edge
            // docks there.
            ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

            ImGui.SetNextWindowSize(new Vector2(280, 0), ImGuiCond.FirstUseEver);
            ImGui.Begin("Cube");
            ImGui.SliderFloat("Size", ref size, 0.5f, 5f);
            ImGui.ColorEdit3("Color", ref color);
            ImGui.Checkbox("Wires", ref wires);
            ImGui.Text($"{GetFPS()} FPS");
            ImGui.End();

            ImGui.SetNextWindowPos(new Vector2(480, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(240, 100), ImGuiCond.FirstUseEver);
            ImGui.Begin("Help");
            ImGui.TextWrapped("Drag a window by its title to an edge to dock it.");
            ImGui.End();

            EndDrawing();
        }

        CloseWindow();
    }
}

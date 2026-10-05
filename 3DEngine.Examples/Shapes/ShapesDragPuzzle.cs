// raylib's shapes_drag_puzzle example, Copyright (c) 2026 Gabriel Piangers (@gabriel-piangers), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesDragPuzzle
{
    private struct Circle
    {
        public Vector2 center;
        public float radius;
    }

    private struct Triangle
    {
        public Vector2 v1;
        public Vector2 v2;
        public Vector2 v3;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] drag puzzle");

        Rectangle rec = new(screenWidth/2 - 250, screenHeight/2 + 50, 100.0f, 100.0f);
        Rectangle recArea = new(screenWidth/2 - 60, screenHeight/2 - 110, 110.0f, 110.0f);
        bool recPickedUp = false;

        Circle circ = new() { center = new Vector2(screenWidth/2, screenHeight/2 + 100), radius = 50.0f };
        Circle circArea = new() { center = new Vector2(screenWidth/2 - 195, screenHeight/2 - 55), radius = 55.0f };
        bool circPickedUp = false;

        Triangle tri = new() { v1 = new Vector2(600, 282), v2 = new Vector2(550, 369), v3 = new Vector2(650, 369) };
        Triangle triArea = new() { v1 = new Vector2(600, 115), v2 = new Vector2(540, 222), v3 = new Vector2(660, 222) };
        bool triPickedUp = false;

        Vector2 mouseOffset = new(0.0f, 0.0f);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            Vector2 mousePosition = GetMousePosition();
            bool recPlaced = false;
            bool circPlaced = false;
            bool triPlaced = false;

            if (IsMouseButtonPressed(MouseButton.Left) && CheckCollisionPointRec(mousePosition, rec))
            {
                recPickedUp = true;
                mouseOffset = new Vector2(rec.X - mousePosition.X, rec.Y - mousePosition.Y);
            }
            else if (IsMouseButtonPressed(MouseButton.Left) && CheckCollisionPointCircle(mousePosition, circ.center, circ.radius))
            {
                circPickedUp = true;
                mouseOffset = new Vector2(circ.center.X - mousePosition.X, circ.center.Y - mousePosition.Y);
            }
            else if (IsMouseButtonPressed(MouseButton.Left) && CheckCollisionPointTriangle(mousePosition, tri.v1, tri.v2, tri.v3))
            {
                triPickedUp = true;
                mouseOffset = new Vector2(tri.v1.X - mousePosition.X, tri.v1.Y - mousePosition.Y);
            }

            if (IsMouseButtonReleased(MouseButton.Left) && recPickedUp) recPickedUp = false;
            else if (IsMouseButtonReleased(MouseButton.Left) && circPickedUp) circPickedUp = false;
            else if (IsMouseButtonReleased(MouseButton.Left) && triPickedUp) triPickedUp = false;

            if (recPickedUp)
            {
                rec = rec with { X = mousePosition.X + mouseOffset.X };
                rec = rec with { Y = mousePosition.Y + mouseOffset.Y };
            }
            Rectangle RecCol = GetCollisionRec(recArea, rec);
            if (RecCol.Width == rec.Width && RecCol.Height == rec.Height) recPlaced = true;

            if (circPickedUp)
            {
                circ.center.X = mousePosition.X + mouseOffset.X;
                circ.center.Y = mousePosition.Y + mouseOffset.Y;
            }
            if (Vector2.Distance(circ.center, circArea.center) < circArea.radius - circ.radius) circPlaced = true;

            if (triPickedUp)
            {
                Vector2 v2Offset = new(tri.v2.X - tri.v1.X, tri.v2.Y - tri.v1.Y);
                Vector2 v3Offset = new(tri.v3.X - tri.v1.X, tri.v3.Y - tri.v1.Y);

                tri.v1 = new Vector2(mousePosition.X + mouseOffset.X, mousePosition.Y + mouseOffset.Y);
                tri.v2 = new Vector2(tri.v1.X + v2Offset.X, tri.v1.Y + v2Offset.Y);
                tri.v3 = new Vector2(tri.v1.X + v3Offset.X, tri.v1.Y + v3Offset.Y);
            }
            if (
                CheckCollisionPointTriangle(tri.v1, triArea.v1, triArea.v2, triArea.v3) &&
                CheckCollisionPointTriangle(tri.v2, triArea.v1, triArea.v2, triArea.v3) &&
                CheckCollisionPointTriangle(tri.v2, triArea.v1, triArea.v2, triArea.v3)
            ) triPlaced = true;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawRectangleLinesEx(recArea, 2.0f, recPlaced ? Color.Green : Color.Red);
                DrawCircleLinesEx(circArea.center, circArea.radius, 2.0f, circPlaced ? Color.Green : Color.Red);
                DrawTriangleLinesEx(triArea.v1, triArea.v2, triArea.v3, 2.0f, triPlaced ? Color.Green : Color.Red);

                if (!triPickedUp) DrawTriangle(tri.v1, tri.v2, tri.v3, Color.Violet);
                if (!circPickedUp) DrawCircleV(circ.center, circ.radius, Color.Blue);
                if (!recPickedUp) DrawRectangleRec(rec, Color.Orange);

                if (triPickedUp) DrawTriangle(tri.v1, tri.v2, tri.v3, Color.Violet);
                if (circPickedUp) DrawCircleV(circ.center, circ.radius, Color.Blue);
                if (recPickedUp) DrawRectangleRec(rec, Color.Orange);

                DrawText("Use mouse to drag and drop the objects into the right spot!", 10, 10, 20, Color.Gray);
            EndDrawing();
        }

        CloseWindow();
    }
}

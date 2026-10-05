// raylib's shapes_outlines_testbed example, Copyright (c) 2026 Matthew Roush (@MatthewRoush), under the
// zlib license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesOutlinesTestbed
{
    private static readonly Color COLOR_FILLED = Color.DarkBlue;
    private static readonly Color COLOR_OUTLINE = Color.Yellow;

    private const float SHAPE_SPACING = 14.0f;
    private const float SHAPE_SIZE = 46.0f;
    private const float BOX_SPACING = 10.0f;

    private const float MOUSE_CAMERA_ZOOM_SPEED = 0.3f;
    private const float KEYBOARD_CAMERA_MOVE_SPEED = 10.0f;
    private const float KEYBOARD_CAMERA_ZOOM_SPEED = 0.1f;

    private const float CAMERA_ZOOM_MIN = 0.1f;
    private const float CAMERA_ZOOM_MAX = 1000.0f;

    // The shapes, left to right.
    private const int ORDER_RECTANGLE = 0;
    private const int ORDER_RECTANGLE_ROUNDED = 1;
    private const int ORDER_CIRCLE = 2;
    private const int ORDER_ELLIPSE = 3;
    private const int ORDER_CIRCLE_SECTOR = 4;
    private const int ORDER_RING = 5;
    private const int ORDER_TRIANGLE = 6;
    private const int ORDER_POLYGON = 7;
    private const int COUNT_SHAPES = 8;

    // The line styles, top to bottom.
    private const int ORDER_LINES = 0;
    private const int ORDER_LINES_EX_WORLD = 1;
    private const int ORDER_LINES_EX_SCREEN = 2;

    private const float BOX_WIDTH = SHAPE_SIZE*COUNT_SHAPES + SHAPE_SPACING*(COUNT_SHAPES - 1) + BOX_SPACING*2.0f;
    private const float BOX_HEIGHT = SHAPE_SIZE + BOX_SPACING*2.0f;

    // raygui's line color, which its group box draws its lines and title in.
    private static readonly Color GroupBoxColor = new(0x90, 0xab, 0xb5, 255);

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] outlines testbed");

        Camera2D camera = new() { Zoom = 1 };

        float lineOpacity          = 130;
        float lineThickness        = 4.0f;
        float rectangleRoundness   = 0.4f;
        float rectangleSegments    = 9;
        float ellipseRadiusY       = 0.5f;
        float circleStartAngle     = 20.0f;
        float circleEndAngle       = 270.0f;
        float circleSegments       = 36;
        float ringInnerRadiusScale = 0.3f;
        float polygonSides         = 6;

        bool disableMouseControl = false;

        Rectangle optionsBackground = new(510, 0, 290, 450);

        Vector2 zoomPoint = new(((float)screenWidth - optionsBackground.Width)/2.0f, (float)screenHeight/2.0f);

        // Zooms about the middle of the shapes' side, keeping the point there where it is.
        void ZoomTo(float zoom)
        {
            Vector2 prevWorldZoomPoint = GetScreenToWorld2D(zoomPoint, camera);

            camera.Zoom = Math.Clamp(zoom, CAMERA_ZOOM_MIN, CAMERA_ZOOM_MAX);

            Vector2 worldZoomPoint = GetScreenToWorld2D(zoomPoint, camera);
            camera.Target += prevWorldZoomPoint - worldZoomPoint;
        }

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            Vector2 mousePosScreen = GetMousePosition();
            Vector2 mouseWheelVec = GetMouseWheelMoveV();

            if (IsMouseButtonPressed(MouseButton.Left) && CheckCollisionPointRec(mousePosScreen, optionsBackground)) disableMouseControl = true;

            if (IsMouseButtonReleased(MouseButton.Left)) disableMouseControl = false;

            if (!disableMouseControl)
            {
                // A constant rate of zoom.
                if (mouseWheelVec.Y != 0.0f) ZoomTo(camera.Zoom*float.Exp2(MOUSE_CAMERA_ZOOM_SPEED*mouseWheelVec.Y));

                if (IsMouseButtonDown(MouseButton.Left))
                {
                    camera.Target -= GetMouseDelta()/camera.Zoom;

                    int mouseMaxX = (int)optionsBackground.X;
                    int mouseMaxY = screenHeight;

                    int newX = (int)mousePosScreen.X;
                    int newY = (int)mousePosScreen.Y;

                    // The pointer wraps around the shapes' side, a modulus where C# has a remainder.
                    newX = (newX%mouseMaxX + mouseMaxX)%mouseMaxX;
                    newY = (newY%mouseMaxY + mouseMaxY)%mouseMaxY;

                    if ((newX != (int)mousePosScreen.X) || (newY != (int)mousePosScreen.Y)) SetMousePosition(newX, newY);
                }
            }

            if (IsKeyDown(Key.A)) camera.Target -= new Vector2(KEYBOARD_CAMERA_MOVE_SPEED/camera.Zoom, 0);
            if (IsKeyDown(Key.D)) camera.Target += new Vector2(KEYBOARD_CAMERA_MOVE_SPEED/camera.Zoom, 0);
            if (IsKeyDown(Key.W)) camera.Target -= new Vector2(0, KEYBOARD_CAMERA_MOVE_SPEED/camera.Zoom);
            if (IsKeyDown(Key.S)) camera.Target += new Vector2(0, KEYBOARD_CAMERA_MOVE_SPEED/camera.Zoom);

            if (IsKeyDown(Key.Up)) ZoomTo(camera.Zoom*float.Exp2(KEYBOARD_CAMERA_ZOOM_SPEED));
            if (IsKeyDown(Key.Down)) ZoomTo(camera.Zoom*float.Exp2(-KEYBOARD_CAMERA_ZOOM_SPEED));

            if (IsKeyPressed(Key.Z)) ZoomTo(1.0f);

            if (IsKeyPressed(Key.C)) camera.Target = Vector2.Zero;

            BeginDrawing();

                ClearBackground(new Color(50, 50, 55, 255));

                Color colorOutline = COLOR_OUTLINE with { A = (byte)lineOpacity };

                BeginMode2D(camera);

                const float shapeOffset = BOX_SPACING*2.0f;
                const float shapePaddingX = SHAPE_SIZE + SHAPE_SPACING;
                const float shapePaddingY = SHAPE_SIZE + SHAPE_SPACING + BOX_SPACING*2.0f;

                float radiusX = SHAPE_SIZE/2.0f;
                float radiusY = radiusX*ellipseRadiusY;

                float ringOuterRadius = SHAPE_SIZE/2.0f;
                float ringInnerRadius = ringOuterRadius*ringInnerRadiusScale;

                Vector2 triangleVertex0Offset = new(0.0f, SHAPE_SIZE*0.8f);
                Vector2 triangleVertex1Offset = new(SHAPE_SIZE*0.6f, SHAPE_SIZE);
                Vector2 triangleVertex2Offset = new(SHAPE_SIZE, 0.0f);

                // Draw*Lines()
                float posY = shapeOffset + shapePaddingY*ORDER_LINES;

                float posX = shapeOffset + shapePaddingX*ORDER_RECTANGLE;
                DrawRectangleRec(new Rectangle(posX, posY, SHAPE_SIZE, SHAPE_SIZE), COLOR_FILLED);
                DrawRectangleLines((int)posX, (int)posY, (int)SHAPE_SIZE, (int)SHAPE_SIZE, colorOutline);

                posX = shapeOffset + shapePaddingX*ORDER_RECTANGLE_ROUNDED;
                DrawRectangleRounded(new Rectangle(posX, posY, SHAPE_SIZE, SHAPE_SIZE), rectangleRoundness, (int)rectangleSegments, COLOR_FILLED);
                DrawRectangleRoundedLines(new Rectangle(posX, posY, SHAPE_SIZE, SHAPE_SIZE), rectangleRoundness, (int)rectangleSegments, colorOutline);

                posX = shapeOffset + shapePaddingX*ORDER_CIRCLE;
                DrawCircleV(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, COLOR_FILLED);
                DrawCircleLinesV(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, colorOutline);

                posX = shapeOffset + shapePaddingX*ORDER_ELLIPSE;
                DrawEllipseV(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, radiusY, COLOR_FILLED);
                DrawEllipseLinesV(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, radiusY, colorOutline);

                posX = shapeOffset + shapePaddingX*ORDER_CIRCLE_SECTOR;
                DrawCircleSector(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, circleStartAngle, circleEndAngle, (int)circleSegments, COLOR_FILLED);
                DrawCircleSectorLines(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, circleStartAngle, circleEndAngle, (int)circleSegments, colorOutline);

                posX = shapeOffset + shapePaddingX*ORDER_RING;
                DrawRing(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), ringInnerRadius, ringOuterRadius, circleStartAngle, circleEndAngle, (int)circleSegments, COLOR_FILLED);
                DrawRingLines(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), ringInnerRadius, ringOuterRadius, circleStartAngle, circleEndAngle, (int)circleSegments, colorOutline);

                posX = shapeOffset + shapePaddingX*ORDER_TRIANGLE;
                DrawTriangle(new Vector2(posX, posY) + triangleVertex0Offset, new Vector2(posX, posY) + triangleVertex1Offset, new Vector2(posX, posY) + triangleVertex2Offset, COLOR_FILLED);
                DrawTriangleLines(new Vector2(posX, posY) + triangleVertex0Offset, new Vector2(posX, posY) + triangleVertex1Offset, new Vector2(posX, posY) + triangleVertex2Offset, colorOutline);

                posX = shapeOffset + shapePaddingX*ORDER_POLYGON;
                DrawPoly(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), (int)polygonSides, radiusX, 0.0f, COLOR_FILLED);
                DrawPolyLines(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), (int)polygonSides, radiusX, 0.0f, colorOutline);

                GroupBox(new Rectangle(shapeOffset - BOX_SPACING, posY - BOX_SPACING, BOX_WIDTH, BOX_HEIGHT), "Draw*Lines()");

                // Draw*LinesEx() with a thickness in world space, and then in screen space.
                for (int order = ORDER_LINES_EX_WORLD; order <= ORDER_LINES_EX_SCREEN; order++)
                {
                    posY = shapeOffset + shapePaddingY*order;
                    float thickness = (order == ORDER_LINES_EX_WORLD)? lineThickness : lineThickness/camera.Zoom;

                    posX = shapeOffset + shapePaddingX*ORDER_RECTANGLE;
                    DrawRectangleRec(new Rectangle(posX, posY, SHAPE_SIZE, SHAPE_SIZE), COLOR_FILLED);
                    DrawRectangleLinesEx(new Rectangle(posX, posY, SHAPE_SIZE, SHAPE_SIZE), thickness, colorOutline);

                    posX = shapeOffset + shapePaddingX*ORDER_RECTANGLE_ROUNDED;
                    DrawRectangleRounded(new Rectangle(posX, posY, SHAPE_SIZE, SHAPE_SIZE), rectangleRoundness, (int)rectangleSegments, COLOR_FILLED);
                    DrawRectangleRoundedLinesEx(new Rectangle(posX, posY, SHAPE_SIZE, SHAPE_SIZE), rectangleRoundness, (int)rectangleSegments, thickness, colorOutline);

                    posX = shapeOffset + shapePaddingX*ORDER_CIRCLE;
                    DrawCircleV(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, COLOR_FILLED);
                    DrawCircleLinesEx(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, thickness, colorOutline);

                    posX = shapeOffset + shapePaddingX*ORDER_ELLIPSE;
                    DrawEllipseV(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, radiusY, COLOR_FILLED);
                    DrawEllipseLinesEx(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, radiusY, thickness, colorOutline);

                    posX = shapeOffset + shapePaddingX*ORDER_CIRCLE_SECTOR;
                    DrawCircleSector(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, circleStartAngle, circleEndAngle, (int)circleSegments, COLOR_FILLED);
                    DrawCircleSectorLinesEx(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), radiusX, circleStartAngle, circleEndAngle, (int)circleSegments, thickness, colorOutline);

                    posX = shapeOffset + shapePaddingX*ORDER_RING;
                    DrawRing(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), ringInnerRadius, ringOuterRadius, circleStartAngle, circleEndAngle, (int)circleSegments, COLOR_FILLED);
                    DrawRingLinesEx(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), ringInnerRadius, ringOuterRadius, circleStartAngle, circleEndAngle, (int)circleSegments, thickness, colorOutline);

                    posX = shapeOffset + shapePaddingX*ORDER_TRIANGLE;
                    DrawTriangle(new Vector2(posX, posY) + triangleVertex0Offset, new Vector2(posX, posY) + triangleVertex1Offset, new Vector2(posX, posY) + triangleVertex2Offset, COLOR_FILLED);
                    DrawTriangleLinesEx(new Vector2(posX, posY) + triangleVertex0Offset, new Vector2(posX, posY) + triangleVertex1Offset, new Vector2(posX, posY) + triangleVertex2Offset, thickness, colorOutline);

                    posX = shapeOffset + shapePaddingX*ORDER_POLYGON;
                    DrawPoly(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), (int)polygonSides, radiusX, 0.0f, COLOR_FILLED);
                    DrawPolyLinesEx(new Vector2(posX + SHAPE_SIZE/2.0f, posY + SHAPE_SIZE/2.0f), (int)polygonSides, radiusX, 0.0f, thickness, colorOutline);

                    GroupBox(new Rectangle(shapeOffset - BOX_SPACING, posY - BOX_SPACING, BOX_WIDTH, BOX_HEIGHT),
                        (order == ORDER_LINES_EX_WORLD)? "Draw*LinesEx() with *world space* pixel thickness" : "Draw*LinesEx() with *screen space* pixel thickness");
                }

                EndMode2D();

                DrawRectangleRec(optionsBackground, Fade(Color.DarkGray, 0.75f));

                DrawRectangleRec(new Rectangle(optionsBackground.X, 360, optionsBackground.Width, 90), Fade(Color.Black, 0.4f));
                DrawLineEx(new Vector2(optionsBackground.X, 360), new Vector2(optionsBackground.X + optionsBackground.Width, 360), 1.0f, Color.Black);

                DrawText("Move with the mouse or WASD keys", (int)optionsBackground.X + 10, 370, 10, Color.Orange);
                DrawText("Zoom with the mouse or UP and DOWN keys", (int)optionsBackground.X + 10, 390, 10, Color.Orange);
                DrawText("Press C to reset position", (int)optionsBackground.X + 10, 410, 10, Color.Orange);
                DrawText("Press Z to reset zoom", (int)optionsBackground.X + 10, 430, 10, Color.Orange);

                DrawLineEx(new Vector2(optionsBackground.X, optionsBackground.Y), new Vector2(optionsBackground.X, optionsBackground.Y + optionsBackground.Height), 1.0f, Color.Black);

                // raygui's sliders, as ImGui's, where raygui places them, each named on its left as
                // raygui names it, in ImGui's dark style since raylib's own labels here are white.
                ImGui.SetNextWindowPos(new Vector2(optionsBackground.X, 0));
                ImGui.SetNextWindowSize(new Vector2(optionsBackground.Width, 360));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
                Slider(10, "Line Opacity", ref lineOpacity, 0, 255, "%.0f");
                Slider(45, "Thickness", ref lineThickness, -20.0f, 40.0f, "%.2f");
                Slider(80, "Rect Roundness", ref rectangleRoundness, -1.0f, 2.0f, "%.2f");
                Slider(115, "Rect Segments", ref rectangleSegments, -1, 100, "%.0f");
                Slider(150, "Ellipse Radius Y", ref ellipseRadiusY, 0.0f, 1.25f, "%.2f");
                Slider(185, "Start Angle", ref circleStartAngle, -360.0f, 360.0f, "%.2f");
                Slider(220, "End Angle", ref circleEndAngle, -360.0f, 360.0f, "%.2f");
                Slider(255, "Circle Segments", ref circleSegments, -1, 100, "%.0f");
                Slider(290, "Ring Radius", ref ringInnerRadiusScale, -1.0f, 2.0f, "%.2f");
                Slider(325, "Poly Sides", ref polygonSides, -1, 100, "%.0f");
                ImGui.End();

            EndDrawing();
        }

        CloseWindow();
    }

    // A slider in raygui's place, 150 wide, its name ending 5 pixels left of it. raygui's bar
    // starts at x 605 and writes its value to the right, and ImGui writes the value inside, so the
    // slider moves into that space, 35 pixels right, leaving room for ImGui's wider font on its left.
    private static void Slider(float y, string label, ref float value, float min, float max, string format)
    {
        ImGui.SetCursorScreenPos(new Vector2(635 - ImGui.CalcTextSize(label).X, y + 3));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SetCursorScreenPos(new Vector2(640, y + 3));
        ImGui.SetNextItemWidth(150);
        ImGui.SliderFloat("##" + label, ref value, min, max, format);
    }

    // raygui's group box, drawn in the camera's space as raylib's is, which ImGui does not draw in,
    // so it is lines and text in raygui's color. The title breaks the top line 12 pixels in.
    private static void GroupBox(Rectangle bounds, string text)
    {
        const int textSize = 10;
        float textWidth = MeasureText(text, textSize) + 2;

        DrawRectangleRec(new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), GroupBoxColor);
        DrawRectangleRec(new Rectangle(bounds.X + bounds.Width - 1, bounds.Y, 1, bounds.Height), GroupBoxColor);
        DrawRectangleRec(new Rectangle(bounds.X, bounds.Y + bounds.Height - 1, bounds.Width, 1), GroupBoxColor);

        DrawRectangleRec(new Rectangle(bounds.X, bounds.Y, 12 - 4, 1), GroupBoxColor);
        DrawText(text, (int)(bounds.X + 12), (int)(bounds.Y - textSize/2), textSize, GroupBoxColor);
        DrawRectangleRec(new Rectangle(bounds.X + 12 + textWidth + 4, bounds.Y, bounds.Width - textWidth - 12 - 4, 1), GroupBoxColor);
    }
}

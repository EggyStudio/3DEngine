// raylib's shapes_rlgl_color_wheel example, Copyright (c) 2025 Robin (@RobinsAviary), under the zlib
// license, written again for the flat API, with ImGui in raygui's place.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesRlglColorWheel
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        // The minimum/maximum points the circle can have
        const uint pointsMin = 3;
        const uint pointsMax = 256;

        // The current number of points and the radius of the circle
        uint triangleCount = 64;
        float pointScale = 150.0f;

        // Slider value, literally maps to value in HSV
        float value = 1.0f;

        // The center of the screen
        Vector2 center = new((float)screenWidth/2.0f, (float)screenHeight/2.0f);
        // The location of the color wheel
        Vector2 circlePosition = center;

        // The currently selected color
        Color color = new(255, 255, 255, 255);

        // Indicates if the slider is being clicked
        bool sliderClicked = false;

        // Indicates if the current color going to be updated, as well as the handle position
        bool settingColor = false;

        // How the color wheel will be rendered
        RlDrawMode renderType = RlDrawMode.Triangles;

        // Enable anti-aliasing
        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shapes] rlgl color wheel");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            triangleCount = unchecked(triangleCount + (uint)(int)GetMouseWheelMove());
            triangleCount = (uint)Math.Clamp((float)triangleCount, (float)pointsMin, (float)pointsMax);

            Rectangle sliderRectangle = new(42.0f, 16.0f + 64.0f + 45.0f, 64.0f, 16.0f);
            Vector2 mousePosition = GetMousePosition();

            // Checks if the user is hovering over the value slider
            bool sliderHover = (mousePosition.X >= sliderRectangle.X && mousePosition.Y >= sliderRectangle.Y && mousePosition.X < sliderRectangle.X + sliderRectangle.Width && mousePosition.Y < sliderRectangle.Y + sliderRectangle.Height);

            // Copy color as hex
            if (IsKeyDown(Key.LeftControl) && IsKeyDown(Key.C))
            {
                if (IsKeyPressed(Key.C))
                {
                    SetClipboardText($"#{color.R:X2}{color.G:X2}{color.B:X2}");
                }
            }

            // Scale up the color wheel, adjusting the handle visually
            if (IsKeyDown(Key.Up))
            {
                pointScale *= 1.025f;

                if (pointScale > (float)screenHeight/2.0f)
                {
                    pointScale = (float)screenHeight/2.0f;
                }
                else
                {
                    circlePosition = (circlePosition - center)*new Vector2(1.025f, 1.025f) + center;
                }
            }

            // Scale down the wheel, adjusting the handle visually
            if (IsKeyDown(Key.Down))
            {
                pointScale *= 0.975f;

                if (pointScale < 32.0f)
                {
                    pointScale = 32.0f;
                }
                else
                {
                    circlePosition = (circlePosition - center)*new Vector2(0.975f, 0.975f) + center;
                }

                float distance = Vector2.Distance(center, circlePosition)/pointScale;
                float angle = ((Vector2Angle(new Vector2(0.0f, -pointScale), center - circlePosition)/MathF.PI + 1.0f)/2.0f);

                if (distance > 1.0f)
                {
                    circlePosition = new Vector2(MathF.Sin(angle*(MathF.PI*2.0f))*pointScale, -MathF.Cos(angle*(MathF.PI*2.0f))*pointScale) + center;
                }
            }

            // Checks if the user clicked on the color wheel
            if (IsMouseButtonPressed(MouseButton.Left) && Vector2.Distance(GetMousePosition(), center) <= pointScale + 10.0f)
            {
                settingColor = true;
            }

            // Update flag when mouse button is released
            if (IsMouseButtonReleased(MouseButton.Left)) settingColor = false;

            // Check if the user clicked/released the slider for the color's value
            if (sliderHover && IsMouseButtonPressed(MouseButton.Left)) sliderClicked = true;

            if (sliderClicked && IsMouseButtonReleased(MouseButton.Left)) sliderClicked = false;

            // Update render mode accordingly
            if (IsKeyPressed(Key.Space)) renderType = RlDrawMode.Lines;

            if (IsKeyReleased(Key.Space)) renderType = RlDrawMode.Triangles;

            // If the slider or the wheel was clicked, update the current color
            if (settingColor || sliderClicked)
            {
                if (settingColor) circlePosition = GetMousePosition();

                float distance = Vector2.Distance(center, circlePosition)/pointScale;

                float angle = ((Vector2Angle(new Vector2(0.0f, -pointScale), center - circlePosition)/MathF.PI + 1.0f)/2.0f);
                if (settingColor && distance > 1.0f) circlePosition = new Vector2(MathF.Sin(angle*(MathF.PI*2.0f))*pointScale, -MathF.Cos(angle*(MathF.PI*2.0f))*pointScale) + center;

                float angle360 = angle*360.0f;
                float valueActual = Math.Clamp(distance, 0.0f, 1.0f);
                color = ColorLerp(new Color((byte)(value*255.0f), (byte)(value*255.0f), (byte)(value*255.0f), 255), ColorFromHSV(angle360, Math.Clamp(distance, 0.0f, 1.0f), 1.0f), valueActual);
            }

            BeginDrawing();

            ClearBackground(Color.RayWhite);

            // Begin rendering color wheel
            rlBegin(renderType);
            for (uint i = 0; i < triangleCount; i++)
            {
                float angleOffset = ((MathF.PI*2.0f)/(float)triangleCount);
                float angle = angleOffset*(float)i;
                float angleOffsetCalculated = ((float)i + 1)*angleOffset;
                Vector2 scale = new(pointScale, pointScale);

                Vector2 offset = new Vector2(MathF.Sin(angle), -MathF.Cos(angle))*scale;
                Vector2 offset2 = new Vector2(MathF.Sin(angleOffsetCalculated), -MathF.Cos(angleOffsetCalculated))*scale;

                Vector2 position = center + offset;
                Vector2 position2 = center + offset2;

                float angleNonRadian = (angle/(2.0f*MathF.PI))*360.0f;
                float angleNonRadianOffset = (angleOffset/(2.0f*MathF.PI))*360.0f;

                Color currentColor = ColorFromHSV(angleNonRadian, 1.0f, 1.0f);
                Color offsetColor = ColorFromHSV(angleNonRadian + angleNonRadianOffset, 1.0f, 1.0f);

                // Input vertices differently depending on mode
                if (renderType == RlDrawMode.Triangles)
                {
                    // RL_TRIANGLES expects three vertices per triangle
                    rlColor4ub(currentColor.R, currentColor.G, currentColor.B, currentColor.A);
                    rlVertex2f(position.X, position.Y);
                    rlColor4f(value, value, value, 1.0f);
                    rlVertex2f(center.X, center.Y);
                    rlColor4ub(offsetColor.R, offsetColor.G, offsetColor.B, offsetColor.A);
                    rlVertex2f(position2.X, position2.Y);
                }
                else if (renderType == RlDrawMode.Lines)
                {
                    // RL_LINES expects two vertices per line
                    rlColor4ub(currentColor.R, currentColor.G, currentColor.B, currentColor.A);
                    rlVertex2f(position.X, position.Y);
                    rlColor4ub(Color.White.R, Color.White.G, Color.White.B, Color.White.A);
                    rlVertex2f(center.X, center.Y);

                    rlVertex2f(center.X, center.Y);
                    rlColor4ub(offsetColor.R, offsetColor.G, offsetColor.B, offsetColor.A);
                    rlVertex2f(position2.X, position2.Y);

                    rlVertex2f(position2.X, position2.Y);
                    rlColor4ub(currentColor.R, currentColor.G, currentColor.B, currentColor.A);
                    rlVertex2f(position.X, position.Y);
                }
            }
            rlEnd();

            // Make the handle slightly more visible overtop darker colors
            Color handleColor = Color.Black;

            if (Vector2.Distance(center, circlePosition)/pointScale <= 0.5f && value <= 0.5f)
            {
                handleColor = Color.DarkGray;
            }

            // Draw the color handle
            DrawCircleLinesV(circlePosition, 4.0f, handleColor);

            // Draw the color in a preview, with a darkened outline.
            DrawRectangleV(new Vector2(8.0f, 8.0f), new Vector2(64.0f, 64.0f), color);
            DrawRectangleLinesEx(new Rectangle(8.0f, 8.0f, 64.0f, 64.0f), 2.0f, ColorLerp(color, Color.Black, 0.5f));

            // Draw current color as hex and decimal
            DrawText($"#{color.R:X2}{color.G:X2}{color.B:X2}\n({color.R}, {color.G}, {color.B})", 8, 8 + 64 + 8, 20, Color.DarkGray);

            // Update the visuals for the copying text
            Color copyColor = Color.DarkGray;
            int offsetY = 0;
            if (IsKeyDown(Key.LeftControl) && IsKeyDown(Key.C))
            {
                copyColor = Color.DarkGreen;
                offsetY = 4;
            }

            // Draw the copying text
            DrawText("press ctrl+c to copy!", 8, 425 - offsetY, 20, copyColor);

            // Display the number of rendered triangles
            DrawText($"triangle count: {triangleCount}", 8, 395, 20, Color.DarkGray);

            // Slider to change color's value, raygui's slider bar as ImGui's, with its text to the
            // left at raygui's text size of 10
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
            ImGui.Begin("##value", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
            ImGui.SetWindowFontScale(10.0f/13.0f);
            Vector2 labelSize = ImGui.CalcTextSize("value: ");
            ImGui.SetCursorScreenPos(new Vector2(sliderRectangle.X - labelSize.X - 4, sliderRectangle.Y + (sliderRectangle.Height - labelSize.Y)/2));
            ImGui.TextUnformatted("value: ");
            ImGui.SetCursorScreenPos(new Vector2(sliderRectangle.X, sliderRectangle.Y));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, (sliderRectangle.Height - ImGui.GetFontSize())/2));
            ImGui.SetNextItemWidth(sliderRectangle.Width);
            ImGui.SliderFloat("##valueSlider", ref value, 0.0f, 1.0f, "");
            ImGui.PopStyleVar();
            ImGui.End();

            // Draw FPS next to outlined color preview
            DrawFPS(64 + 16, 8);

            EndDrawing();
        }

        CloseWindow();
    }
}

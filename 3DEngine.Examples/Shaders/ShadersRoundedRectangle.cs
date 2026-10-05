// raylib's shaders_rounded_rectangle example, Copyright (c) 2025 Anstro Pleuton (@anstropleuton), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersRoundedRectangle
{
    private struct RoundedRectangle
    {
        public Vector4 cornerRadius;    // Each corner's: top left, top right, bottom left, bottom right

        public float shadowRadius;
        public Vector2 shadowOffset;
        public float shadowScale;

        public float borderThickness;   // Inside the rectangle's edge

        // The shader's places for them
        public int rectangleLoc;
        public int radiusLoc;
        public int colorLoc;
        public int shadowRadiusLoc;
        public int shadowOffsetLoc;
        public int shadowScaleLoc;
        public int shadowColorLoc;
        public int borderThicknessLoc;
        public int borderColorLoc;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] rounded rectangle");

        // raylib's rounded_rectangle.fs, written in Slang, with the engine's own vertex stage for its base.vs
        Shader shader = LoadShader("resources/shaders/slang/rounded_rectangle.slang");

        // The shader's one value raylib's has no need of, the screen's height, which turns Vulkan's
        // pixel rows to count from the bottom as GLSL's do
        SetShaderValue(shader, GetShaderLocation(shader, "screenHeight"), (float)screenHeight);

        RoundedRectangle roundedRectangle = CreateRoundedRectangle(
            new Vector4(5.0f, 10.0f, 15.0f, 20.0f),     // Corner radius
            20.0f,                                      // Shadow radius
            new Vector2(0.0f, -5.0f),                   // Shadow offset
            0.95f,                                      // Shadow scale
            5.0f,                                       // Border thickness
            shader);

        UpdateRoundedRectangle(roundedRectangle, shader);

        Color rectangleColor = Color.Blue;
        Color shadowColor = Color.DarkBlue;
        Color borderColor = Color.SkyBlue;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // The rectangle alone,
                Rectangle rec = new(50, 70, 110, 60);
                DrawRectangleLines((int)rec.X - 20, (int)rec.Y - 20, (int)rec.Width + 40, (int)rec.Height + 40, Color.DarkGray);
                DrawText("Rounded rectangle", (int)rec.X - 20, (int)rec.Y - 35, 10, Color.DarkGray);

                Draw(shader, roundedRectangle, rec, screenHeight, rectangleColor, Color.Blank, Color.Blank);

                // its shadow alone,
                rec = new Rectangle(50, 200, 110, 60);
                DrawRectangleLines((int)rec.X - 20, (int)rec.Y - 20, (int)rec.Width + 40, (int)rec.Height + 40, Color.DarkGray);
                DrawText("Rounded rectangle shadow", (int)rec.X - 20, (int)rec.Y - 35, 10, Color.DarkGray);

                Draw(shader, roundedRectangle, rec, screenHeight, Color.Blank, shadowColor, Color.Blank);

                // its border alone,
                rec = new Rectangle(50, 330, 110, 60);
                DrawRectangleLines((int)rec.X - 20, (int)rec.Y - 20, (int)rec.Width + 40, (int)rec.Height + 40, Color.DarkGray);
                DrawText("Rounded rectangle border", (int)rec.X - 20, (int)rec.Y - 35, 10, Color.DarkGray);

                Draw(shader, roundedRectangle, rec, screenHeight, Color.Blank, Color.Blank, borderColor);

                // and all three together.
                rec = new Rectangle(240, 80, 500, 300);
                DrawRectangleLines((int)rec.X - 30, (int)rec.Y - 30, (int)rec.Width + 60, (int)rec.Height + 60, Color.DarkGray);
                DrawText("Rectangle with all three combined", (int)rec.X - 30, (int)rec.Y - 45, 10, Color.DarkGray);

                Draw(shader, roundedRectangle, rec, screenHeight, rectangleColor, shadowColor, borderColor);

                DrawText("(c) Rounded rectangle SDF by Iñigo Quilez. MIT License.", screenWidth - 300, screenHeight - 20, 10, Color.Black);

            EndDrawing();
        }

        UnloadShader(shader);

        CloseWindow();
    }

    // The rectangle drawn over the screen in its colors, a clear one leaving that part out. Its y is
    // turned to count from the bottom, as the shader's pixels are. raylib writes each draw out.
    private static void Draw(Shader shader, RoundedRectangle roundedRectangle, Rectangle rec, int screenHeight, Color color, Color shadowColor, Color borderColor)
    {
        rec = rec with { Y = screenHeight - rec.Y - rec.Height };
        SetShaderValue(shader, roundedRectangle.rectangleLoc, new Vector4(rec.X, rec.Y, rec.Width, rec.Height));

        SetShaderValue(shader, roundedRectangle.colorLoc, color.ToVector4());
        SetShaderValue(shader, roundedRectangle.shadowColorLoc, shadowColor.ToVector4());
        SetShaderValue(shader, roundedRectangle.borderColorLoc, borderColor.ToVector4());

        BeginShaderMode(shader);
            DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Color.White);
        EndShaderMode();
    }

    // A rounded rectangle and its places in the shader
    private static RoundedRectangle CreateRoundedRectangle(Vector4 cornerRadius, float shadowRadius, Vector2 shadowOffset, float shadowScale, float borderThickness, Shader shader)
    {
        RoundedRectangle rec = new()
        {
            cornerRadius = cornerRadius,
            shadowRadius = shadowRadius,
            shadowOffset = shadowOffset,
            shadowScale = shadowScale,
            borderThickness = borderThickness,

            rectangleLoc = GetShaderLocation(shader, "rectangle"),
            radiusLoc = GetShaderLocation(shader, "radius"),
            colorLoc = GetShaderLocation(shader, "color"),
            shadowRadiusLoc = GetShaderLocation(shader, "shadowRadius"),
            shadowOffsetLoc = GetShaderLocation(shader, "shadowOffset"),
            shadowScaleLoc = GetShaderLocation(shader, "shadowScale"),
            shadowColorLoc = GetShaderLocation(shader, "shadowColor"),
            borderThicknessLoc = GetShaderLocation(shader, "borderThickness"),
            borderColorLoc = GetShaderLocation(shader, "borderColor"),
        };

        UpdateRoundedRectangle(rec, shader);

        return rec;
    }

    private static void UpdateRoundedRectangle(RoundedRectangle rec, Shader shader)
    {
        SetShaderValue(shader, rec.radiusLoc, rec.cornerRadius);
        SetShaderValue(shader, rec.shadowRadiusLoc, rec.shadowRadius);
        SetShaderValue(shader, rec.shadowOffsetLoc, rec.shadowOffset);
        SetShaderValue(shader, rec.shadowScaleLoc, rec.shadowScale);
        SetShaderValue(shader, rec.borderThicknessLoc, rec.borderThickness);
    }
}

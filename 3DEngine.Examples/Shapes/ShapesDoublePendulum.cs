// raylib's shapes_double_pendulum example, Copyright (c) 2025 JoeCheong (@Joecheong2006), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesDoublePendulum
{
    private const int SIMULATION_STEPS = 30;

    private const float G = 9.81f;

    private static Vector2 CalculatePendulumEndPoint(float l, float theta)
    {
        return new Vector2(10*l*MathF.Sin(theta), 10*l*MathF.Cos(theta));
    }

    private static Vector2 CalculateDoublePendulumEndPoint(float l1, float theta1, float l2, float theta2)
    {
        Vector2 endpoint1 = CalculatePendulumEndPoint(l1, theta1);
        Vector2 endpoint2 = CalculatePendulumEndPoint(l2, theta2);
        return new Vector2(endpoint1.X + endpoint2.X, endpoint1.Y + endpoint2.Y);
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        // raylib's asks for FLAG_WINDOW_HIGHDPI here, which the flat API does not carry, and which
        // draws nothing differently on a monitor at a scale of one.
        InitWindow(screenWidth, screenHeight, "[shapes] double pendulum");

        float l1 = 15.0f, m1 = 0.2f, theta1 = (MathF.PI/180)*170, w1 = 0;
        float l2 = 15.0f, m2 = 0.1f, theta2 = (MathF.PI/180)*0, w2 = 0;
        float lengthScaler = 0.1f;
        float totalM = m1 + m2;

        Vector2 previousPosition = CalculateDoublePendulumEndPoint(l1, theta1, l2, theta2);
        previousPosition.X += ((float)screenWidth/2);
        previousPosition.Y += ((float)screenHeight/2 - 100);

        float L1 = l1*lengthScaler;
        float L2 = l2*lengthScaler;

        float lineThick = 20, trailThick = 2;
        float fateAlpha = 0.01f;

        RenderTexture2D target = LoadRenderTexture(screenWidth, screenHeight);
        SetTextureFilter(target.Texture, TextureFilter.Bilinear);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float dt = GetFrameTime();
            float step = dt/SIMULATION_STEPS, step2 = step*step;

            for (int i = 0; i < SIMULATION_STEPS; i++)
            {
                float delta = theta1 - theta2;
                float sinD = MathF.Sin(delta), cosD = MathF.Cos(delta), cos2D = MathF.Cos(2*delta);
                float ww1 = w1*w1, ww2 = w2*w2;

                float a1 = (-G*(2*m1 + m2)*MathF.Sin(theta1)
                             - m2*G*MathF.Sin(theta1 - 2*theta2)
                             - 2*sinD*m2*(ww2*L2 + ww1*L1*cosD))
                            /(L1*(2*m1 + m2 - m2*cos2D));

                float a2 = (2*sinD*(ww1*L1*totalM
                             + G*totalM*MathF.Cos(theta1)
                             + ww2*L2*m2*cosD))
                            /(L2*(2*m1 + m2 - m2*cos2D));

                theta1 += w1*step + 0.5f*a1*step2;
                theta2 += w2*step + 0.5f*a2*step2;

                w1 += a1*step;
                w2 += a2*step;
            }

            Vector2 currentPosition = CalculateDoublePendulumEndPoint(l1, theta1, l2, theta2);
            currentPosition.X += (float)screenWidth/2;
            currentPosition.Y += (float)screenHeight/2 - 100;

            BeginTextureMode(target);

                DrawRectangle(0, 0, screenWidth, screenHeight, Fade(Color.Black, fateAlpha));

                DrawCircleV(previousPosition, trailThick, Color.Red);
                DrawLineEx(previousPosition, currentPosition, trailThick*2, Color.Red);
            EndTextureMode();

            previousPosition = currentPosition;

            BeginDrawing();

                ClearBackground(Color.Black);

                DrawTextureRec(target.Texture, new Rectangle(0, 0, (float)target.Texture.Width, (float)target.Texture.Height), new Vector2(0, 0), Color.White);

                DrawRectanglePro(new Rectangle(screenWidth/2.0f, screenHeight/2.0f - 100, 10*l1, lineThick),
                    new Vector2(0, lineThick*0.5f), 90 - (180/MathF.PI)*theta1, Color.RayWhite);

                Vector2 endpoint1 = CalculatePendulumEndPoint(l1, theta1);
                DrawRectanglePro(new Rectangle(screenWidth/2.0f + endpoint1.X, screenHeight/2.0f - 100 + endpoint1.Y, 10*l2, lineThick),
                    new Vector2(0, lineThick*0.5f), 90 - (180/MathF.PI)*theta2, Color.RayWhite);

            EndDrawing();
        }

        UnloadRenderTexture(target);

        CloseWindow();
    }
}

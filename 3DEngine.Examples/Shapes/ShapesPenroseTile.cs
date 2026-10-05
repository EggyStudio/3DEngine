// raylib's shapes_penrose_tile example, Copyright (c) 2025 David Buzatto (@davidbuzatto), under the zlib
// license, written again for the flat API.

using System.Numerics;
using System.Text;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesPenroseTile
{
    private const int STR_MAX_SIZE = 10000;
    private const int TURTLE_STACK_MAX_SIZE = 50;
    private const float DEG2RAD = MathF.PI/180.0f;

    private struct TurtleState
    {
        public Vector2 origin;
        public float angle;
    }

    private sealed class PenroseLSystem
    {
        public int steps;
        public string production = "";
        public string ruleW = "";
        public string ruleX = "";
        public string ruleY = "";
        public string ruleZ = "";
        public float drawLength;
        public float theta;
    }

    private static readonly TurtleState[] turtleStack = new TurtleState[TURTLE_STACK_MAX_SIZE];
    private static int turtleTop = -1;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shapes] penrose tile");

        float drawLength = 460.0f;
        int minGenerations = 0;
        int maxGenerations = 4;
        int generations = 0;

        PenroseLSystem ls = CreatePenroseLSystem(drawLength*(generations/(float)maxGenerations));
        for (int i = 0; i < generations; i++) BuildProductionStep(ls);

        SetTargetFPS(120);

        while (!WindowShouldClose())
        {
            bool rebuild = false;
            if (IsKeyPressed(Key.Up))
            {
                if (generations < maxGenerations)
                {
                    generations++;
                    rebuild = true;
                }
            }
            else if (IsKeyPressed(Key.Down))
            {
                if (generations > minGenerations)
                {
                    generations--;
                    if (generations > 0) rebuild = true;
                }
            }

            if (rebuild)
            {
                ls = CreatePenroseLSystem(drawLength*(generations/(float)maxGenerations));
                for (int i = 0; i < generations; i++) BuildProductionStep(ls);
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (generations > 0) DrawPenroseLSystem(ls);

                DrawText("penrose l-system", 10, 10, 20, Color.DarkGray);
                DrawText("press up or down to change generations", 10, 30, 20, Color.DarkGray);
                DrawText($"generations: {generations}", 10, 50, 20, Color.DarkGray);

            EndDrawing();
        }

        CloseWindow();
    }

    private static void PushTurtleState(TurtleState state)
    {
        if (turtleTop < (TURTLE_STACK_MAX_SIZE - 1)) turtleStack[++turtleTop] = state;
        else TraceLog(LogLevel.Warning, "TURTLE STACK OVERFLOW!");
    }

    private static TurtleState PopTurtleState()
    {
        if (turtleTop >= 0) return turtleStack[turtleTop--];
        else TraceLog(LogLevel.Warning, "TURTLE STACK UNDERFLOW!");

        return default;
    }

    private static PenroseLSystem CreatePenroseLSystem(float drawLength) => new()
    {
        steps = 0,
        ruleW = "YF++ZF4-XF[-YF4-WF]++",
        ruleX = "+YF--ZF[3-WF--XF]+",
        ruleY = "-WF++XF[+++YF++ZF]-",
        ruleZ = "--YF++++WF[+ZF++++XF]--XF",
        drawLength = drawLength,
        theta = 36.0f,
        production = "[X]++[X]++[X]++[X]++[X]",
    };

    private static void BuildProductionStep(PenroseLSystem ls)
    {
        // At most STR_MAX_SIZE - 1 characters, as raylib's buffer holds with its terminator.
        var newProduction = new StringBuilder();
        void Append(string text) => newProduction.Append(text.AsSpan(0, Math.Min(text.Length, STR_MAX_SIZE - 1 - newProduction.Length)));

        foreach (char step in ls.production)
        {
            switch (step)
            {
                case 'W': Append(ls.ruleW); break;
                case 'X': Append(ls.ruleX); break;
                case 'Y': Append(ls.ruleY); break;
                case 'Z': Append(ls.ruleZ); break;
                default:
                    if (step != 'F') Append(step.ToString());
                    break;
            }
        }

        ls.drawLength *= 0.5f;
        ls.production = newProduction.ToString();
    }

    private static void DrawPenroseLSystem(PenroseLSystem ls)
    {
        Vector2 screenCenter = new(GetScreenWidth()/2.0f, GetScreenHeight()/2.0f);

        TurtleState turtle = new() { origin = Vector2.Zero, angle = -90.0f };

        int repeats = 1;
        int productionLength = ls.production.Length;
        ls.steps += 12;

        if (ls.steps > productionLength) ls.steps = productionLength;

        for (int i = 0; i < ls.steps; i++)
        {
            char step = ls.production[i];

            if (step == 'F')
            {
                for (int j = 0; j < repeats; j++)
                {
                    Vector2 startPosWorld = turtle.origin;
                    float radAngle = DEG2RAD*turtle.angle;
                    turtle.origin.X += ls.drawLength*MathF.Cos(radAngle);
                    turtle.origin.Y += ls.drawLength*MathF.Sin(radAngle);
                    Vector2 startPosScreen = startPosWorld + screenCenter;
                    Vector2 endPosScreen = turtle.origin + screenCenter;

                    DrawLineEx(startPosScreen, endPosScreen, 2, Fade(Color.Black, 0.2f));
                }

                repeats = 1;
            }
            else if (step == '+')
            {
                for (int j = 0; j < repeats; j++) turtle.angle += ls.theta;

                repeats = 1;
            }
            else if (step == '-')
            {
                for (int j = 0; j < repeats; j++) turtle.angle += -ls.theta;

                repeats = 1;
            }
            else if (step == '[') PushTurtleState(turtle);
            else if (step == ']') turtle = PopTurtleState();
            else if ((step >= 48) && (step <= 57)) repeats = (int)step - 48;
        }

        turtleTop = -1;
    }
}

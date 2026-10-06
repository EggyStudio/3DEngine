// raylib's text_strings_management example, Copyright (c) 2025 David Buzatto (@davidbuzatto), under the
// zlib license, written again for the flat API.

using System.Numerics;
using System.Text;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextStringsManagement
{
    private const int MAX_TEXT_PARTICLES = 100;
    private const int FONT_SIZE = 30;

    private struct TextParticle
    {
        public string text;
        public Rectangle rect;
        public Vector2 vel;
        public Vector2 ppos;
        public float padding;
        public float borderWidth;
        public float friction;
        public float elasticity;
        public Color color;
        public bool grabbed;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] strings management");

        TextParticle[] textParticles = new TextParticle[MAX_TEXT_PARTICLES];
        int particleCount = 0;

        // raylib keeps a pointer to the grabbed particle's place in the array, and this its index.
        int grabbedTextParticle = -1;
        Vector2 pressOffset = Vector2.Zero;

        PrepareFirstTextParticle("raylib => fun videogames programming!", textParticles, ref particleCount);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float delta = GetFrameTime();
            Vector2 mousePos = GetMousePosition();

            if (IsMouseButtonPressed(MouseButton.Left))
            {
                for (int i = particleCount - 1; i >= 0; i--)
                {
                    ref TextParticle tp = ref textParticles[i];
                    pressOffset.X = mousePos.X - tp.rect.X;
                    pressOffset.Y = mousePos.Y - tp.rect.Y;
                    if (CheckCollisionPointRec(mousePos, tp.rect))
                    {
                        tp.grabbed = true;
                        grabbedTextParticle = i;
                        break;
                    }
                }
            }

            if (IsMouseButtonReleased(MouseButton.Left))
            {
                if (grabbedTextParticle != -1)
                {
                    textParticles[grabbedTextParticle].grabbed = false;
                    grabbedTextParticle = -1;
                }
            }

            // A right click slices a particle in two, or with shift into its characters.
            if (IsMouseButtonPressed(MouseButton.Right))
            {
                for (int i = particleCount - 1; i >= 0; i--)
                {
                    if (CheckCollisionPointRec(mousePos, textParticles[i].rect))
                    {
                        if (IsKeyDown(Key.LeftShift))
                        {
                            ShatterTextParticle(i, textParticles, ref particleCount);
                        }
                        else
                        {
                            SliceTextParticle(i, textParticles[i].text.Length/2, textParticles, ref particleCount);
                        }
                        break;
                    }
                }
            }

            if (IsMouseButtonPressed(MouseButton.Middle))
            {
                for (int i = 0; i < particleCount; i++)
                {
                    if (!textParticles[i].grabbed) textParticles[i].vel = new Vector2((float)GetRandomValue(-2000, 2000), (float)GetRandomValue(-2000, 2000));
                }
            }

            // raylib's TextToUpper and TextToLower are C#'s, and its Pascal, snake and camel case
            // are this example's own, as raylib writes them.
            if (IsKeyPressed(Key.One)) PrepareFirstTextParticle("raylib => fun videogames programming!", textParticles, ref particleCount);
            if (IsKeyPressed(Key.Two)) PrepareFirstTextParticle("raylib => fun videogames programming!".ToUpperInvariant(), textParticles, ref particleCount);
            if (IsKeyPressed(Key.Three)) PrepareFirstTextParticle("raylib => fun videogames programming!".ToLowerInvariant(), textParticles, ref particleCount);
            if (IsKeyPressed(Key.Four)) PrepareFirstTextParticle(TextToPascal("raylib_fun_videogames_programming"), textParticles, ref particleCount);
            if (IsKeyPressed(Key.Five)) PrepareFirstTextParticle(TextToSnake("RaylibFunVideogamesProgramming"), textParticles, ref particleCount);
            if (IsKeyPressed(Key.Six)) PrepareFirstTextParticle(TextToCamel("raylib_fun_videogames_programming"), textParticles, ref particleCount);

            // A character typed slices the only particle at that character.
            int charPressed = GetCharPressed();
            if ((charPressed >= 'A') && (charPressed <= 'z') && (particleCount == 1))
            {
                SliceTextParticleByChar((char)charPressed, textParticles, ref particleCount);
            }

            for (int i = 0; i < particleCount; i++)
            {
                ref TextParticle tp = ref textParticles[i];

                if (!tp.grabbed)
                {
                    tp.rect = tp.rect with { X = tp.rect.X + tp.vel.X*delta, Y = tp.rect.Y + tp.vel.Y*delta };

                    // Each wall it hits takes a tenth of its speed.
                    if ((tp.rect.X + tp.rect.Width) >= screenWidth)
                    {
                        tp.rect = tp.rect with { X = screenWidth - tp.rect.Width };
                        tp.vel.X = -tp.vel.X*tp.elasticity;
                    }
                    else if (tp.rect.X <= 0)
                    {
                        tp.rect = tp.rect with { X = 0.0f };
                        tp.vel.X = -tp.vel.X*tp.elasticity;
                    }

                    if ((tp.rect.Y + tp.rect.Height) >= screenHeight)
                    {
                        tp.rect = tp.rect with { Y = screenHeight - tp.rect.Height };
                        tp.vel.Y = -tp.vel.Y*tp.elasticity;
                    }
                    else if (tp.rect.Y <= 0)
                    {
                        tp.rect = tp.rect with { Y = 0.0f };
                        tp.vel.Y = -tp.vel.Y*tp.elasticity;
                    }

                    // Friction takes a hundredth of its speed a frame.
                    tp.vel.X = tp.vel.X*tp.friction;
                    tp.vel.Y = tp.vel.Y*tp.friction;
                }
                else
                {
                    tp.rect = tp.rect with { X = mousePos.X - pressOffset.X, Y = mousePos.Y - pressOffset.Y };

                    // Its speed while held, for the throw when it is let go
                    tp.vel.X = (tp.rect.X - tp.ppos.X)/delta;
                    tp.vel.Y = (tp.rect.Y - tp.ppos.Y)/delta;
                    tp.ppos.X = tp.rect.X;
                    tp.ppos.Y = tp.rect.Y;

                    // Left control glues the held particle to those it touches.
                    if (IsKeyDown(Key.LeftControl))
                    {
                        for (int j = 0; j < particleCount; j++)
                        {
                            if ((j != grabbedTextParticle) && textParticles[grabbedTextParticle].grabbed)
                            {
                                if (CheckCollisionRecs(textParticles[grabbedTextParticle].rect, textParticles[j].rect))
                                {
                                    GlueTextParticles(grabbedTextParticle, j, textParticles, ref particleCount);
                                    grabbedTextParticle = particleCount - 1;
                                }
                            }
                        }
                    }
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int i = 0; i < particleCount; i++)
                {
                    ref TextParticle tp = ref textParticles[i];
                    DrawRectangleRec(new Rectangle(tp.rect.X - tp.borderWidth, tp.rect.Y - tp.borderWidth, tp.rect.Width + tp.borderWidth*2, tp.rect.Height + tp.borderWidth*2), Color.Black);
                    DrawRectangleRec(tp.rect, tp.color);
                    DrawText(tp.text, (int)(tp.rect.X + tp.padding), (int)(tp.rect.Y + tp.padding), FONT_SIZE, Color.Black);
                }

                DrawText("grab a text particle by pressing with the mouse and throw it by releasing", 10, 10, 10, Color.DarkGray);
                DrawText("slice a text particle by pressing it with the mouse right button", 10, 30, 10, Color.DarkGray);
                DrawText("shatter a text particle keeping left shift pressed and pressing it with the mouse right button", 10, 50, 10, Color.DarkGray);
                DrawText("glue text particles by grabbing than and keeping left control pressed", 10, 70, 10, Color.DarkGray);
                DrawText("1 to 6 to reset", 10, 90, 10, Color.DarkGray);
                DrawText("when you have only one text particle, you can slice it by pressing a char", 10, 110, 10, Color.DarkGray);
                DrawText($"TEXT PARTICLE COUNT: {particleCount}", 10, GetScreenHeight() - 30, 20, Color.Black);

            EndDrawing();
        }

        CloseWindow();
    }

    private static void PrepareFirstTextParticle(string text, TextParticle[] tps, ref int particleCount)
    {
        tps[0] = CreateTextParticle(text, GetScreenWidth()/2.0f, GetScreenHeight()/2.0f, Color.RayWhite);
        particleCount = 1;
    }

    private static TextParticle CreateTextParticle(string text, float x, float y, Color color)
    {
        TextParticle tp = new()
        {
            text = text,
            rect = new Rectangle(x, y, 30, 30),
            vel = new Vector2((float)GetRandomValue(-200, 200), (float)GetRandomValue(-200, 200)),
            ppos = Vector2.Zero,
            padding = 5.0f,
            borderWidth = 5.0f,
            friction = 0.99f,
            elasticity = 0.9f,
            color = color,
            grabbed = false,
        };

        tp.rect = tp.rect with { Width = MeasureText(tp.text, FONT_SIZE) + tp.padding*2, Height = FONT_SIZE + tp.padding*2 };
        return tp;
    }

    private static Color RandomColor() => new((byte)GetRandomValue(0, 255), (byte)GetRandomValue(0, 255), (byte)GetRandomValue(0, 255), 255);

    private static void SliceTextParticle(int particlePos, int sliceLength, TextParticle[] tps, ref int particleCount)
    {
        TextParticle tp = tps[particlePos];
        int length = tp.text.Length;

        if ((length > 1) && ((particleCount + length) < MAX_TEXT_PARTICLES))
        {
            for (int i = 0; i < length; i += sliceLength)
            {
                // raylib's TextSubtext, held to the text's end
                string text = tp.text.Substring(i, Math.Min(sliceLength, length - i));
                tps[particleCount++] = CreateTextParticle(text, tp.rect.X + i*tp.rect.Width/length, tp.rect.Y, RandomColor());
            }
            RealocateTextParticles(tps, particlePos, ref particleCount);
        }
    }

    private static void SliceTextParticleByChar(char charToSlice, TextParticle[] tps, ref int particleCount)
    {
        TextParticle tp = tps[0];
        string[] tokens = tp.text.Split(charToSlice);

        if (tokens.Length > 1)
        {
            foreach (char c in tp.text)
            {
                if (c == charToSlice) tps[particleCount++] = CreateTextParticle(charToSlice.ToString(), tp.rect.X, tp.rect.Y, RandomColor());
            }
            for (int i = 0; i < tokens.Length; i++)
            {
                int tokenLength = tokens[i].Length;
                tps[particleCount++] = CreateTextParticle(tokens[i], tp.rect.X + i*tp.rect.Width/tokenLength, tp.rect.Y, RandomColor());
            }
            RealocateTextParticles(tps, 0, ref particleCount);
        }
    }

    private static void ShatterTextParticle(int particlePos, TextParticle[] tps, ref int particleCount) =>
        SliceTextParticle(particlePos, 1, tps, ref particleCount);

    private static void GlueTextParticles(int p1, int p2, TextParticle[] tps, ref int particleCount)
    {
        TextParticle tp = CreateTextParticle(tps[p1].text + tps[p2].text, tps[p1].rect.X, tps[p1].rect.Y, Color.RayWhite);
        tp.grabbed = true;
        tps[particleCount++] = tp;
        tps[p1].grabbed = false;
        if (p1 < p2)
        {
            RealocateTextParticles(tps, p2, ref particleCount);
            RealocateTextParticles(tps, p1, ref particleCount);
        }
        else
        {
            RealocateTextParticles(tps, p1, ref particleCount);
            RealocateTextParticles(tps, p2, ref particleCount);
        }
    }

    // Takes the particle at a place out, moving those after it down one.
    private static void RealocateTextParticles(TextParticle[] tps, int particlePos, ref int particleCount)
    {
        for (int i = particlePos + 1; i < particleCount; i++) tps[i - 1] = tps[i];
        particleCount--;
    }

    // raylib's TextToPascal: the first letter and each after a run of underscores upper case, the
    // underscores dropped.
    private static string TextToPascal(string text) => JoinWords(text, upperFirst: true);

    // raylib's TextToCamel: as Pascal case with the first letter lower case.
    private static string TextToCamel(string text) => JoinWords(text, upperFirst: false);

    private static string JoinWords(string text, bool upperFirst)
    {
        if (text.Length == 0) return "";

        var buffer = new StringBuilder();
        char first = text[0];
        buffer.Append(upperFirst ? (first is >= 'a' and <= 'z' ? (char)(first - 32) : first) : (first is >= 'A' and <= 'Z' ? (char)(first + 32) : first));

        for (int j = 1; j < text.Length; j++)
        {
            if (text[j] != '_') buffer.Append(text[j]);
            else
            {
                while ((j < text.Length) && (text[j] == '_')) j++;
                if (j == text.Length) break;

                buffer.Append(text[j] is >= 'a' and <= 'z' ? (char)(text[j] - 32) : text[j]);
            }
        }

        return buffer.ToString();
    }

    // raylib's TextToSnake: a space or a word's capital starts a new word after an underscore, a
    // run of capitals staying one word, and capitals go lower case.
    private static string TextToSnake(string text)
    {
        var buffer = new StringBuilder();

        for (int j = 0; j < text.Length; j++)
        {
            char c = text[j];
            if (c == ' ')
            {
                if ((buffer.Length > 0) && (buffer[^1] != '_')) buffer.Append('_');
            }
            else if (c is >= 'A' and <= 'Z')
            {
                if ((buffer.Length > 0) && (buffer[^1] != '_'))
                {
                    char prev = text[j - 1];
                    char next = (j + 1 < text.Length) ? text[j + 1] : '\0';

                    if ((prev is >= 'a' and <= 'z') || ((prev is >= 'A' and <= 'Z') && (next is >= 'a' and <= 'z'))) buffer.Append('_');
                }

                buffer.Append((char)(c + 32));
            }
            else buffer.Append(c);
        }

        return buffer.ToString();
    }
}

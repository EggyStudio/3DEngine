// raylib's text_3d_drawing example, Copyright (c) 2021-2025 Vlad Adrian (@demizdor), under the zlib
// license, written again for the flat API.

using System.Numerics;
using System.Text;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Text3DDrawing
{
    private const float LETTER_BOUNDRY_SIZE = 0.25f;
    private const int TEXT_MAX_LAYERS = 32;
    private static readonly Color LETTER_BOUNDRY_COLOR = Color.Violet;

    // raylib's text is a buffer of 64 bytes, the last ending it
    private const int TEXT_CAPACITY = 64;

    private static bool SHOW_LETTER_BOUNDRY = false;
    private static bool SHOW_TEXT_BOUNDRY = false;

    // Configuration structure for waving the text
    private struct WaveTextConfig
    {
        public Vector3 waveRange;
        public Vector3 waveSpeed;
        public Vector3 waveOffset;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VsyncHint);
        InitWindow(screenWidth, screenHeight, "[text] 3d drawing");

        bool spin = true;        // Spin the camera?
        bool multicolor = false; // Multicolor mode

        // Define the camera to look into our 3d world
        Camera3D camera = new(new Vector3(-10.0f, 15.0f, -10.0f), new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        CameraMode camera_mode = CameraMode.Orbital;

        Vector3 cubePosition = new(0.0f, 1.0f, 0.0f);
        Vector3 cubeSize = new(2.0f, 2.0f, 2.0f);

        // Use the default font
        Font font = GetFontDefault();
        float fontSize = 0.8f;
        float fontSpacing = 0.05f;
        float lineSpacing = -0.1f;

        // Set the text (using markdown!)
        string text = "Hello ~~World~~ in 3D!";
        Vector3 tbox = Vector3.Zero;
        int layers = 1;
        int quads = 0;
        float layerDistance = 0.01f;

        WaveTextConfig wcfg;
        wcfg.waveSpeed = new Vector3(3.0f, 3.0f, 0.5f);
        wcfg.waveOffset = new Vector3(0.35f, 0.35f, 0.35f);
        wcfg.waveRange = new Vector3(0.45f, 0.45f, 0.45f);

        float time = 0.0f;

        // Setup a light and dark color
        Color light = Color.Maroon;
        Color dark = Color.Red;

        // raylib's alpha_discard.fs, written in Slang
        Shader alphaDiscard = LoadShader("resources/shaders/slang/alpha_discard.slang");

        // Array filled with multiple random colors (when multicolor mode is set)
        Color[] multi = new Color[TEXT_MAX_LAYERS];

        DisableCursor();                    // Limit cursor to relative movement inside the window

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, camera_mode);

            // Handle font files dropped
            if (IsFileDropped())
            {
                string[] droppedFiles = LoadDroppedFiles();

                // NOTE: We only support first ttf file dropped
                if (Path.GetExtension(droppedFiles[0]).Equals(".ttf", StringComparison.OrdinalIgnoreCase))
                {
                    UnloadFont(font);
                    font = LoadFontEx(droppedFiles[0], (int)fontSize);
                }
                else if (Path.GetExtension(droppedFiles[0]).Equals(".fnt", StringComparison.OrdinalIgnoreCase))
                {
                    UnloadFont(font);
                    font = LoadFont(droppedFiles[0]);
                    fontSize = (float)font.BaseSize;
                }
            }

            // Handle Events
            if (IsKeyPressed(Key.F1)) SHOW_LETTER_BOUNDRY = !SHOW_LETTER_BOUNDRY;
            if (IsKeyPressed(Key.F2)) SHOW_TEXT_BOUNDRY = !SHOW_TEXT_BOUNDRY;
            if (IsKeyPressed(Key.F3))
            {
                // Handle camera change
                spin = !spin;
                // we need to reset the camera when changing modes
                camera = new Camera3D(Vector3.Zero, new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

                if (spin)
                {
                    camera.Position = new Vector3(-10.0f, 15.0f, -10.0f);   // Camera position
                    camera_mode = CameraMode.Orbital;
                }
                else
                {
                    camera.Position = new Vector3(10.0f, 10.0f, -10.0f);   // Camera position
                    camera_mode = CameraMode.Free;
                }
            }

            // Handle clicking the cube
            if (IsMouseButtonPressed(MouseButton.Left))
            {
                Ray ray = GetScreenToWorldRay(GetMousePosition(), camera);

                // Check collision between ray and box
                RayCollision collision = GetRayCollisionBox(ray,
                                new BoundingBox(new Vector3(cubePosition.X - cubeSize.X/2, cubePosition.Y - cubeSize.Y/2, cubePosition.Z - cubeSize.Z/2),
                                                new Vector3(cubePosition.X + cubeSize.X/2, cubePosition.Y + cubeSize.Y/2, cubePosition.Z + cubeSize.Z/2)));
                if (collision.Hit)
                {
                    // Generate new random colors
                    light = GenerateRandomColor(0.5f, 0.78f);
                    dark = GenerateRandomColor(0.4f, 0.58f);
                }
            }

            // Handle text layers changes
            if (IsKeyPressed(Key.Home)) { if (layers > 1) --layers; }
            else if (IsKeyPressed(Key.End)) { if (layers < TEXT_MAX_LAYERS) ++layers; }

            // Handle text changes
            if (IsKeyPressed(Key.Left)) fontSize -= 0.5f;
            else if (IsKeyPressed(Key.Right)) fontSize += 0.5f;
            else if (IsKeyPressed(Key.Up)) fontSpacing -= 0.1f;
            else if (IsKeyPressed(Key.Down)) fontSpacing += 0.1f;
            else if (IsKeyPressed(Key.Pageup)) lineSpacing -= 0.1f;
            else if (IsKeyPressed(Key.Pagedown)) lineSpacing += 0.1f;
            else if (IsKeyDown(Key.Insert)) layerDistance -= 0.001f;
            else if (IsKeyDown(Key.Delete)) layerDistance += 0.001f;
            else if (IsKeyPressed(Key.Tab))
            {
                multicolor = !multicolor;   // Enable /disable multicolor mode

                if (multicolor)
                {
                    // Fill color array with random colors
                    for (int i = 0; i < TEXT_MAX_LAYERS; i++)
                    {
                        multi[i] = GenerateRandomColor(0.5f, 0.8f);
                        multi[i] = multi[i] with { A = (byte)GetRandomValue(0, 255) };
                    }
                }
            }

            // Handle text input
            int ch = GetCharPressed();
            if (IsKeyPressed(Key.Backspace))
            {
                // Remove last char
                int len = text.Length;
                if (len > 0) text = text[..(len - 1)];
            }
            else if (IsKeyPressed(Key.Enter))
            {
                // handle newline, in raylib's 64 bytes with the one that ends them
                int len = text.Length;
                if (len < TEXT_CAPACITY - 1) text += '\n';
            }
            else
            {
                // append only printable chars, a character of none appending nothing
                int len = text.Length;
                if ((len < TEXT_CAPACITY - 1) && (ch != 0)) text += (char)ch;
            }

            // Measure 3D text so we can center it
            tbox = MeasureTextWave3D(font, text, fontSize, fontSpacing, lineSpacing);

            quads = 0;                      // Reset quad counter
            time += GetFrameTime();         // Update timer needed by `DrawTextWave3D()`

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    DrawCubeV(cubePosition, cubeSize, dark);
                    DrawCubeWires(cubePosition, 2.1f, 2.1f, 2.1f, light);

                    DrawGrid(10, 2.0f);

                    // Use a shader to handle the depth buffer issue with transparent textures
                    // NOTE: more info at https://bedroomcoders.co.uk/posts/198
                    BeginShaderMode(alphaDiscard);

                        // Draw the 3D text above the red cube
                        rlPushMatrix();
                            rlRotatef(90.0f, 1.0f, 0.0f, 0.0f);
                            rlRotatef(90.0f, 0.0f, 0.0f, -1.0f);

                            for (int i = 0; i < layers; i++)
                            {
                                Color clr = light;
                                if (multicolor) clr = multi[i];
                                DrawTextWave3D(font, text, new Vector3(-tbox.X/2.0f, layerDistance*i, -4.5f), fontSize, fontSpacing, lineSpacing, true, wcfg, time, clr);
                            }

                            // Draw the text boundry if set
                            if (SHOW_TEXT_BOUNDRY) DrawCubeWiresV(new Vector3(0.0f, 0.0f, -4.5f + tbox.Z/2), tbox, dark);
                        rlPopMatrix();

                        // Don't draw the letter boundries for the 3D text below
                        bool slb = SHOW_LETTER_BOUNDRY;
                        SHOW_LETTER_BOUNDRY = false;

                        // Draw 3D options (use default font)
                        //-------------------------------------------------------------------------
                        rlPushMatrix();
                            rlRotatef(180.0f, 0.0f, 1.0f, 0.0f);
                            string opt = $"< SIZE: {fontSize,3:0.0} >";
                            quads += opt.Length;
                            Vector2 m = MeasureTextEx(GetFontDefault(), opt, 0.8f, 0.1f);
                            Vector3 pos = new(-m.X/2.0f, 0.01f, 2.0f);
                            DrawText3D(GetFontDefault(), opt, pos, 0.8f, 0.1f, 0.0f, false, Color.Blue);
                            pos.Z += 0.5f + m.Y;

                            opt = $"< SPACING: {fontSpacing,3:0.0} >";
                            quads += opt.Length;
                            m = MeasureTextEx(GetFontDefault(), opt, 0.8f, 0.1f);
                            pos.X = -m.X/2.0f;
                            DrawText3D(GetFontDefault(), opt, pos, 0.8f, 0.1f, 0.0f, false, Color.Blue);
                            pos.Z += 0.5f + m.Y;

                            opt = $"< LINE: {lineSpacing,3:0.0} >";
                            quads += opt.Length;
                            m = MeasureTextEx(GetFontDefault(), opt, 0.8f, 0.1f);
                            pos.X = -m.X/2.0f;
                            DrawText3D(GetFontDefault(), opt, pos, 0.8f, 0.1f, 0.0f, false, Color.Blue);
                            pos.Z += 0.5f + m.Y;

                            opt = $"< LBOX: {(slb? "ON" : "OFF"),3} >";
                            quads += opt.Length;
                            m = MeasureTextEx(GetFontDefault(), opt, 0.8f, 0.1f);
                            pos.X = -m.X/2.0f;
                            DrawText3D(GetFontDefault(), opt, pos, 0.8f, 0.1f, 0.0f, false, Color.Red);
                            pos.Z += 0.5f + m.Y;

                            opt = $"< TBOX: {(SHOW_TEXT_BOUNDRY? "ON" : "OFF"),3} >";
                            quads += opt.Length;
                            m = MeasureTextEx(GetFontDefault(), opt, 0.8f, 0.1f);
                            pos.X = -m.X/2.0f;
                            DrawText3D(GetFontDefault(), opt, pos, 0.8f, 0.1f, 0.0f, false, Color.Red);
                            pos.Z += 0.5f + m.Y;

                            opt = $"< LAYER DISTANCE: {layerDistance:0.000} >";
                            quads += opt.Length;
                            m = MeasureTextEx(GetFontDefault(), opt, 0.8f, 0.1f);
                            pos.X = -m.X/2.0f;
                            DrawText3D(GetFontDefault(), opt, pos, 0.8f, 0.1f, 0.0f, false, Color.DarkPurple);
                        rlPopMatrix();
                        //-------------------------------------------------------------------------

                        // Draw 3D info text (use default font)
                        //-------------------------------------------------------------------------
                        opt = "All the text displayed here is in 3D";
                        quads += 36;
                        m = MeasureTextEx(GetFontDefault(), opt, 1.0f, 0.05f);
                        pos = new Vector3(-m.X/2.0f, 0.01f, 2.0f);
                        DrawText3D(GetFontDefault(), opt, pos, 1.0f, 0.05f, 0.0f, false, Color.DarkBlue);
                        pos.Z += 1.5f + m.Y;

                        opt = "press [Left]/[Right] to change the font size";
                        quads += 44;
                        m = MeasureTextEx(GetFontDefault(), opt, 0.6f, 0.05f);
                        pos.X = -m.X/2.0f;
                        DrawText3D(GetFontDefault(), opt, pos, 0.6f, 0.05f, 0.0f, false, Color.DarkBlue);
                        pos.Z += 0.5f + m.Y;

                        opt = "press [Up]/[Down] to change the font spacing";
                        quads += 44;
                        m = MeasureTextEx(GetFontDefault(), opt, 0.6f, 0.05f);
                        pos.X = -m.X/2.0f;
                        DrawText3D(GetFontDefault(), opt, pos, 0.6f, 0.05f, 0.0f, false, Color.DarkBlue);
                        pos.Z += 0.5f + m.Y;

                        opt = "press [PgUp]/[PgDown] to change the line spacing";
                        quads += 48;
                        m = MeasureTextEx(GetFontDefault(), opt, 0.6f, 0.05f);
                        pos.X = -m.X/2.0f;
                        DrawText3D(GetFontDefault(), opt, pos, 0.6f, 0.05f, 0.0f, false, Color.DarkBlue);
                        pos.Z += 0.5f + m.Y;

                        opt = "press [F1] to toggle the letter boundry";
                        quads += 39;
                        m = MeasureTextEx(GetFontDefault(), opt, 0.6f, 0.05f);
                        pos.X = -m.X/2.0f;
                        DrawText3D(GetFontDefault(), opt, pos, 0.6f, 0.05f, 0.0f, false, Color.DarkBlue);
                        pos.Z += 0.5f + m.Y;

                        opt = "press [F2] to toggle the text boundry";
                        quads += 37;
                        m = MeasureTextEx(GetFontDefault(), opt, 0.6f, 0.05f);
                        pos.X = -m.X/2.0f;
                        DrawText3D(GetFontDefault(), opt, pos, 0.6f, 0.05f, 0.0f, false, Color.DarkBlue);
                        //-------------------------------------------------------------------------

                        SHOW_LETTER_BOUNDRY = slb;
                    EndShaderMode();

                EndMode3D();

                // Draw 2D info text & stats
                //-------------------------------------------------------------------------
                DrawText("Drag & drop a font file to change the font!\nType something, see what happens!\n\n" +
                "Press [F3] to toggle the camera", 10, 35, 10, Color.Black);

                quads += text.Length*2*layers;
                string tmp = $"{layers,2} layer(s) | {(spin? "ORBITAL" : "FREE")} camera | {quads,4} quads ({quads*4,4} verts)";
                int width = MeasureText(tmp, 10);
                DrawText(tmp, screenWidth - 20 - width, 10, 10, Color.DarkGreen);

                tmp = "[Home]/[End] to add/remove 3D text layers";
                width = MeasureText(tmp, 10);
                DrawText(tmp, screenWidth - 20 - width, 25, 10, Color.DarkGray);

                tmp = "[Insert]/[Delete] to increase/decrease distance between layers";
                width = MeasureText(tmp, 10);
                DrawText(tmp, screenWidth - 20 - width, 40, 10, Color.DarkGray);

                tmp = "click the [CUBE] for a random color";
                width = MeasureText(tmp, 10);
                DrawText(tmp, screenWidth - 20 - width, 55, 10, Color.DarkGray);

                tmp = "[Tab] to toggle multicolor mode";
                width = MeasureText(tmp, 10);
                DrawText(tmp, screenWidth - 20 - width, 70, 10, Color.DarkGray);
                //-------------------------------------------------------------------------

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadShader(alphaDiscard);
        UnloadFont(font);
        CloseWindow();
    }

    // The code point at a byte of UTF-8 text and how many bytes it takes, '?' and one byte for a
    // byte that begins none, as raylib's GetCodepoint reads it, and none past the end
    private static int GetCodepoint(byte[] text, int at, out int codepointByteCount)
    {
        if (at >= text.Length)
        {
            codepointByteCount = 1;
            return 0;
        }
        if (Rune.DecodeFromUtf8(text.AsSpan(at), out Rune rune, out codepointByteCount) != System.Buffers.OperationStatus.Done)
        {
            codepointByteCount = 1;
            return 0x3f;
        }
        return rune.Value;
    }

    // How far a glyph moves the pen at the font's size, or its width where it moves none. raylib
    // reads both from the font's arrays by the glyph's index, which GetGlyphIndex gives, and a font
    // here keeps its glyphs by code point, which GetGlyphInfo takes, its width X1 - X0.
    private static float GlyphAdvance(Font font, int codepoint)
    {
        Glyph glyph = GetGlyphInfo(font, codepoint) ?? default;
        return (glyph.Advance == 0) ? glyph.X1 - glyph.X0 : glyph.Advance;
    }

    // Draw codepoint at specified position in 3D space
    private static void DrawTextCodepoint3D(Font font, int codepoint, Vector3 position, float fontSize, bool backface, Color tint)
    {
        // The glyph, by its code point, the font's '?' when it has none, as raylib's index points to it.
        // Its place by the pen, its size and its part of the atlas are where raylib reads them from
        // the font's arrays, with no padding, which a font here has none of.
        Glyph glyph = GetGlyphInfo(font, codepoint) ?? default;
        float scale = fontSize/(float)font.BaseSize;

        // Character destination rectangle on screen
        position.X += glyph.X0*scale;
        position.Z += glyph.Y0*scale;

        float width = (glyph.X1 - glyph.X0)*scale;
        float height = (glyph.Y1 - glyph.Y0)*scale;

        if (font.Texture.IsValid)
        {
            const float x = 0.0f;
            const float y = 0.0f;
            const float z = 0.0f;

            // normalized texture coordinates of the glyph inside the font texture (0.0f -> 1.0f)
            float tx = glyph.U0;
            float ty = glyph.V0;
            float tw = glyph.U1;
            float th = glyph.V1;

            if (SHOW_LETTER_BOUNDRY) DrawCubeWiresV(new Vector3(position.X + width/2, position.Y, position.Z + height/2), new Vector3(width, LETTER_BOUNDRY_SIZE, height), LETTER_BOUNDRY_COLOR);

            rlCheckRenderBatchLimit(4 + 4*(backface ? 1 : 0));
            rlSetTexture(font.Texture.Id);

            rlPushMatrix();
                rlTranslatef(position.X, position.Y, position.Z);

                rlBegin(RlDrawMode.Quads);
                    rlColor4ub(tint.R, tint.G, tint.B, tint.A);

                    // Front Face
                    rlNormal3f(0.0f, 1.0f, 0.0f);                                   // Normal Pointing Up
                    rlTexCoord2f(tx, ty); rlVertex3f(x,         y, z);              // Top Left Of The Texture and Quad
                    rlTexCoord2f(tx, th); rlVertex3f(x,         y, z + height);     // Bottom Left Of The Texture and Quad
                    rlTexCoord2f(tw, th); rlVertex3f(x + width, y, z + height);     // Bottom Right Of The Texture and Quad
                    rlTexCoord2f(tw, ty); rlVertex3f(x + width, y, z);              // Top Right Of The Texture and Quad

                    if (backface)
                    {
                        // Back Face
                        rlNormal3f(0.0f, -1.0f, 0.0f);                              // Normal Pointing Down
                        rlTexCoord2f(tx, ty); rlVertex3f(x,         y, z);          // Top Right Of The Texture and Quad
                        rlTexCoord2f(tw, ty); rlVertex3f(x + width, y, z);          // Top Left Of The Texture and Quad
                        rlTexCoord2f(tw, th); rlVertex3f(x + width, y, z + height); // Bottom Left Of The Texture and Quad
                        rlTexCoord2f(tx, th); rlVertex3f(x,         y, z + height); // Bottom Right Of The Texture and Quad
                    }
                rlEnd();
            rlPopMatrix();

            rlSetTexture(0);
        }
    }

    // Draw a 2D text in 3D space
    private static void DrawText3D(Font font, string str, Vector3 position, float fontSize, float fontSpacing, float lineSpacing, bool backface, Color tint)
    {
        byte[] text = Encoding.UTF8.GetBytes(str);
        int length = text.Length;               // Total length in bytes of the text, scanned by codepoints in loop

        float textOffsetY = 0.0f;               // Offset between lines (on line break '\n')
        float textOffsetX = 0.0f;               // Offset X to next character to draw

        float scale = fontSize/(float)font.BaseSize;

        for (int i = 0; i < length;)
        {
            // Get next codepoint from byte string
            int codepoint = GetCodepoint(text, i, out int codepointByteCount);

            // NOTE: Normally we exit the decoding sequence as soon as a bad byte is found (and return 0x3f)
            // but we need to draw all of the bad bytes using the '?' symbol moving one byte
            if (codepoint == 0x3f) codepointByteCount = 1;

            if (codepoint == '\n')
            {
                // NOTE: Fixed line spacing of 1.5 line-height
                textOffsetY += fontSize + lineSpacing;
                textOffsetX = 0.0f;
            }
            else
            {
                if ((codepoint != ' ') && (codepoint != '\t'))
                {
                    DrawTextCodepoint3D(font, codepoint, new Vector3(position.X + textOffsetX, position.Y, position.Z + textOffsetY), fontSize, backface, tint);
                }

                textOffsetX += GlyphAdvance(font, codepoint)*scale + fontSpacing;
            }

            i += codepointByteCount;   // Move text bytes counter to next codepoint
        }
    }

    // Draw a 2D text in 3D space and wave the parts that start with `~~` and end with `~~`
    // This is a modified version of the original code by @Nighten found here https://github.com/NightenDushi/Raylib_DrawTextStyle
    private static void DrawTextWave3D(Font font, string str, Vector3 position, float fontSize, float fontSpacing, float lineSpacing, bool backface, WaveTextConfig config, float time, Color tint)
    {
        byte[] text = Encoding.UTF8.GetBytes(str);
        int length = text.Length;               // Total length in bytes of the text, scanned by codepoints in loop

        float textOffsetY = 0.0f;               // Offset between lines (on line break '\n')
        float textOffsetX = 0.0f;               // Offset X to next character to draw

        float scale = fontSize/(float)font.BaseSize;

        bool wave = false;

        for (int i = 0, k = 0; i < length; ++k)
        {
            // Get next codepoint from byte string
            int codepoint = GetCodepoint(text, i, out int codepointByteCount);

            // NOTE: Normally we exit the decoding sequence as soon as a bad byte is found (and return 0x3f)
            // but we need to draw all of the bad bytes using the '?' symbol moving one byte
            if (codepoint == 0x3f) codepointByteCount = 1;

            if (codepoint == '\n')
            {
                // NOTE: Fixed line spacing of 1.5 line-height
                textOffsetY += fontSize + lineSpacing;
                textOffsetX = 0.0f;
                k = 0;
            }
            else if (codepoint == '~')
            {
                if (GetCodepoint(text, i + 1, out codepointByteCount) == '~')
                {
                    codepointByteCount += 1;
                    wave = !wave;
                }
            }
            else
            {
                if ((codepoint != ' ') && (codepoint != '\t'))
                {
                    Vector3 pos = position;
                    if (wave) // Apply the wave effect
                    {
                        pos.X += MathF.Sin(time*config.waveSpeed.X - k*config.waveOffset.X)*config.waveRange.X;
                        pos.Y += MathF.Sin(time*config.waveSpeed.Y - k*config.waveOffset.Y)*config.waveRange.Y;
                        pos.Z += MathF.Sin(time*config.waveSpeed.Z - k*config.waveOffset.Z)*config.waveRange.Z;
                    }

                    DrawTextCodepoint3D(font, codepoint, new Vector3(pos.X + textOffsetX, pos.Y, pos.Z + textOffsetY), fontSize, backface, tint);
                }

                textOffsetX += GlyphAdvance(font, codepoint)*scale + fontSpacing;
            }

            i += codepointByteCount;   // Move text bytes counter to next codepoint
        }
    }

    // Measure a text in 3D ignoring the `~~` chars
    private static Vector3 MeasureTextWave3D(Font font, string str, float fontSize, float fontSpacing, float lineSpacing)
    {
        byte[] text = Encoding.UTF8.GetBytes(str);
        int len = text.Length;
        int tempLen = 0;                // Used to count longer text line num chars
        int lenCounter = 0;

        float tempTextWidth = 0.0f;     // Used to count longer text line width

        float scale = fontSize/(float)font.BaseSize;
        float textHeight = scale;
        float textWidth = 0.0f;

        int letter = 0;                 // Current character

        for (int i = 0; i < len; i++)
        {
            letter = GetCodepoint(text, i, out int next);

            // NOTE: normally we exit the decoding sequence as soon as a bad byte is found (and return 0x3f)
            // but we need to draw all of the bad bytes using the '?' symbol so to not skip any we set next = 1
            if (letter == 0x3f) next = 1;
            i += next - 1;

            if (letter != '\n')
            {
                if (letter == '~' && GetCodepoint(text, i + 1, out next) == '~')
                {
                    i++;
                }
                else
                {
                    lenCounter++;
                    Glyph glyph = GetGlyphInfo(font, letter) ?? default;
                    if (glyph.Advance != 0) textWidth += glyph.Advance*scale;
                    else textWidth += ((glyph.X1 - glyph.X0) + glyph.X0)*scale;
                }
            }
            else
            {
                if (tempTextWidth < textWidth) tempTextWidth = textWidth;
                lenCounter = 0;
                textWidth = 0.0f;
                textHeight += fontSize + lineSpacing;
            }

            if (tempLen < lenCounter) tempLen = lenCounter;
        }

        if (tempTextWidth < textWidth) tempTextWidth = textWidth;

        Vector3 vec = Vector3.Zero;
        vec.X = tempTextWidth + (float)((tempLen - 1)*fontSpacing); // Adds chars spacing to measure
        vec.Y = 0.25f;
        vec.Z = textHeight;

        return vec;
    }

    // Generates a nice color with a random hue
    private static Color GenerateRandomColor(float s, float v)
    {
        const float Phi = 0.618033988749895f; // Golden ratio conjugate
        float h = (float)GetRandomValue(0, 360);
        h = (h + h*Phi)%360.0f;
        return ColorFromHSV(h, s, v);
    }
}

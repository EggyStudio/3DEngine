// raylib's core_keyboard_testbed example, Copyright (c) 2026 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreKeyboardTestbed
{
    private const int KEY_REC_SPACING = 4;

    private static string GetKeyText(Key key)
    {
        switch (key)
        {
            case Key.Apostrophe      : return "'";
            case Key.Comma           : return ",";
            case Key.Minus           : return "-";
            case Key.Period          : return ".";
            case Key.Slash           : return "/";
            case Key.Alpha0            : return "0";
            case Key.Alpha1             : return "1";
            case Key.Alpha2             : return "2";
            case Key.Alpha3           : return "3";
            case Key.Alpha4            : return "4";
            case Key.Alpha5            : return "5";
            case Key.Alpha6             : return "6";
            case Key.Alpha7           : return "7";
            case Key.Alpha8           : return "8";
            case Key.Alpha9            : return "9";
            case Key.Semicolon       : return ";";
            case Key.Equals           : return "=";
            case Key.A               : return "A";
            case Key.B               : return "B";
            case Key.C               : return "C";
            case Key.D               : return "D";
            case Key.E               : return "E";
            case Key.F               : return "F";
            case Key.G               : return "G";
            case Key.H               : return "H";
            case Key.I               : return "I";
            case Key.J               : return "J";
            case Key.K               : return "K";
            case Key.L               : return "L";
            case Key.M               : return "M";
            case Key.N               : return "N";
            case Key.O               : return "O";
            case Key.P               : return "P";
            case Key.Q               : return "Q";
            case Key.R               : return "R";
            case Key.S               : return "S";
            case Key.T               : return "T";
            case Key.U               : return "U";
            case Key.V               : return "V";
            case Key.W               : return "W";
            case Key.X               : return "X";
            case Key.Y               : return "Y";
            case Key.Z               : return "Z";
            case Key.Leftbracket    : return "[";
            case Key.Backslash       : return "\\";
            case Key.Rightbracket   : return "]";
            case Key.Grave           : return "`";
            case Key.Space           : return "SPACE";
            case Key.Escape          : return "ESC";
            case Key.Enter           : return "ENTER";
            case Key.Tab             : return "TAB";
            case Key.Backspace       : return "BACK";
            case Key.Insert          : return "INS";
            case Key.Delete          : return "DEL";
            case Key.Right           : return "RIGHT";
            case Key.Left            : return "LEFT";
            case Key.Down            : return "DOWN";
            case Key.Up              : return "UP";
            case Key.Pageup         : return "PGUP";
            case Key.Pagedown       : return "PGDOWN";
            case Key.Home            : return "HOME";
            case Key.End             : return "END";
            case Key.Capslock       : return "CAPS";
            case Key.Scrolllock     : return "LOCK";
            case Key.NumLockClear        : return "NUMLOCK";
            case Key.Printscreen    : return "PRINTSCR";
            case Key.Pause           : return "PAUSE";
            case Key.F1              : return "F1";
            case Key.F2              : return "F2";
            case Key.F3              : return "F3";
            case Key.F4              : return "F4";
            case Key.F5              : return "F5";
            case Key.F6              : return "F6";
            case Key.F7              : return "F7";
            case Key.F8              : return "F8";
            case Key.F9              : return "F9";
            case Key.F10             : return "F10";
            case Key.F11             : return "F11";
            case Key.F12             : return "F12";
            case Key.LShift      : return "LSHIFT";
            case Key.LCtrl    : return "LCTRL";
            case Key.LAlt        : return "LALT";
            case Key.LGUI      : return "WIN";
            case Key.RShift     : return "RSHIFT";
            case Key.RCtrl   : return "RCTRL";
            case Key.RAlt       : return "ALTGR";
            case Key.RGUI     : return "RSUPER";
            case Key.Menu         : return "KBMENU";
            case Key.Kp0            : return "KP0";
            case Key.Kp1            : return "KP1";
            case Key.Kp2            : return "KP2";
            case Key.Kp3            : return "KP3";
            case Key.Kp4            : return "KP4";
            case Key.Kp5            : return "KP5";
            case Key.Kp6            : return "KP6";
            case Key.Kp7            : return "KP7";
            case Key.Kp8            : return "KP8";
            case Key.Kp9            : return "KP9";
            case Key.KpPeriod      : return "KPDEC";
            case Key.KpDivide       : return "KPDIV";
            case Key.KpMultiply     : return "KPMUL";
            case Key.KpMinus     : return "KPSUB";
            case Key.KpPlus          : return "KPADD";
            case Key.KpEnter        : return "KPENTER";
            case Key.KpEquals        : return "KPEQU";
            default: return "";
        }
    }

    private static void GuiKeyboardKey(Rectangle bounds, Key key)
    {
        if (key == Key.Unknown) DrawRectangleLinesEx(bounds, 2.0f, Color.LightGray);
        else
        {
            if (IsKeyDown(key))
            {
                DrawRectangleLinesEx(bounds, 2.0f, Color.Maroon);
                DrawText(GetKeyText(key), (int)(bounds.X + 4), (int)(bounds.Y + 4), 10, Color.Maroon);
            }
            else
            {
                DrawRectangleLinesEx(bounds, 2.0f, Color.DarkGray);
                DrawText(GetKeyText(key), (int)(bounds.X + 4), (int)(bounds.Y + 4), 10, Color.DarkGray);
            }
        }

        if (CheckCollisionPointRec(GetMousePosition(), bounds))
        {
            DrawRectangleRec(bounds, Fade(Color.Red, 0.2f));
            DrawRectangleLinesEx(bounds, 3.0f, Color.Red);
        }
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] keyboard testbed");
        SetExitKey(Key.Unknown);

        int[] line01KeyWidths = new int[15];
        for (int i = 0; i < 15; i++) line01KeyWidths[i] = 45;
        line01KeyWidths[13] = 62;
        Key[] line01Keys = {
            Key.Escape, Key.F1, Key.F2, Key.F3, Key.F4, Key.F5,
            Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11,
            Key.F12, Key.Printscreen, Key.Pause
        };

        int[] line02KeyWidths = new int[15];
        for (int i = 0; i < 15; i++) line02KeyWidths[i] = 45;
        line02KeyWidths[0] = 25;
        line02KeyWidths[13] = 82;
        Key[] line02Keys = {
            Key.Grave, Key.Alpha1, Key.Alpha2, Key.Alpha3, Key.Alpha4,
            Key.Alpha5, Key.Alpha6, Key.Alpha7, Key.Alpha8, Key.Alpha9,
            Key.Alpha0, Key.Minus, Key.Equals, Key.Backspace, Key.Delete };

        int[] line03KeyWidths = new int[15];
        for (int i = 0; i < 15; i++) line03KeyWidths[i] = 45;
        line03KeyWidths[0] = 50;
        line03KeyWidths[13] = 57;
        Key[] line03Keys = {
            Key.Tab, Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y,
            Key.U, Key.I, Key.O, Key.P, Key.Leftbracket,
            Key.Rightbracket, Key.Backslash, Key.Insert
        };

        int[] line04KeyWidths = new int[14];
        for (int i = 0; i < 14; i++) line04KeyWidths[i] = 45;
        line04KeyWidths[0] = 68;
        line04KeyWidths[12] = 88;
        Key[] line04Keys = {
            Key.Capslock, Key.A, Key.S, Key.D, Key.F, Key.G,
            Key.H, Key.J, Key.K, Key.L, Key.Semicolon,
            Key.Apostrophe, Key.Enter, Key.Pageup
        };

        int[] line05KeyWidths = new int[14];
        for (int i = 0; i < 14; i++) line05KeyWidths[i] = 45;
        line05KeyWidths[0] = 80;
        line05KeyWidths[11] = 76;
        Key[] line05Keys = {
            Key.LShift, Key.Z, Key.X, Key.C, Key.V, Key.B,
            Key.N, Key.M, Key.Comma, Key.Period, /*Key.Minus*/
            Key.Slash, Key.RShift, Key.Up, Key.Pagedown
        };

        int[] line06KeyWidths = new int[11];
        for (int i = 0; i < 11; i++) line06KeyWidths[i] = 45;
        line06KeyWidths[0] = 80;
        line06KeyWidths[3] = 208;
        line06KeyWidths[7] = 60;
        Key[] line06Keys = {
            Key.LCtrl, Key.LGUI, Key.LAlt,
            Key.Space, Key.RAlt, Key.Application, Key.Unknown,
            Key.RCtrl, Key.Left, Key.Down, Key.Right
        };

        Vector2 keyboardOffset = new(26, 80);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            Key key = GetKeyPressed();
            if (key > 0) TraceLog(LogLevel.Info, $"KEYBOARD TESTBED: KEY PRESSED:    {key}");

            int ch = GetCharPressed();
            if (ch > 0) TraceLog(LogLevel.Info, $"KEYBOARD TESTBED: CHAR PRESSED:   {ch} ({ch})");

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("KEYBOARD LAYOUT: ENG-US", 26, 38, 20, Color.LightGray);

                for (int i = 0, recOffsetX = 0; i < 15; i++)
                {
                    GuiKeyboardKey(new Rectangle(keyboardOffset.X + recOffsetX, keyboardOffset.Y, (float)line01KeyWidths[i], 30.0f), line01Keys[i]);
                    recOffsetX += line01KeyWidths[i] + KEY_REC_SPACING;
                }

                for (int i = 0, recOffsetX = 0; i < 15; i++)
                {
                    GuiKeyboardKey(new Rectangle(keyboardOffset.X + recOffsetX, keyboardOffset.Y + 30 + KEY_REC_SPACING, (float)line02KeyWidths[i], 38.0f), line02Keys[i]);
                    recOffsetX += line02KeyWidths[i] + KEY_REC_SPACING;
                }

                for (int i = 0, recOffsetX = 0; i < 15; i++)
                {
                    GuiKeyboardKey(new Rectangle(keyboardOffset.X + recOffsetX, keyboardOffset.Y + 30 + 38 + KEY_REC_SPACING*2, (float)line03KeyWidths[i], 38.0f), line03Keys[i]);
                    recOffsetX += line03KeyWidths[i] + KEY_REC_SPACING;
                }

                for (int i = 0, recOffsetX = 0; i < 14; i++)
                {
                    GuiKeyboardKey(new Rectangle(keyboardOffset.X + recOffsetX, keyboardOffset.Y + 30 + 38*2 + KEY_REC_SPACING*3, (float)line04KeyWidths[i], 38.0f), line04Keys[i]);
                    recOffsetX += line04KeyWidths[i] + KEY_REC_SPACING;
                }

                for (int i = 0, recOffsetX = 0; i < 14; i++)
                {
                    GuiKeyboardKey(new Rectangle(keyboardOffset.X + recOffsetX, keyboardOffset.Y + 30 + 38*3 + KEY_REC_SPACING*4, (float)line05KeyWidths[i], 38.0f), line05Keys[i]);
                    recOffsetX += line05KeyWidths[i] + KEY_REC_SPACING;
                }

                for (int i = 0, recOffsetX = 0; i < 11; i++)
                {
                    GuiKeyboardKey(new Rectangle(keyboardOffset.X + recOffsetX, keyboardOffset.Y + 30 + 38*4 + KEY_REC_SPACING*5, (float)line06KeyWidths[i], 38.0f), line06Keys[i]);
                    recOffsetX += line06KeyWidths[i] + KEY_REC_SPACING;
                }

            EndDrawing();
        }

        CloseWindow();
    }
}

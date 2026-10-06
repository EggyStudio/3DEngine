// raylib's text_unicode_emojis example, Copyright (c) 2019-2025 Vlad Adrian (@demizdor) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using System.Text;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextUnicodeEmojis
{
    private const int EMOJI_PER_WIDTH = 8;
    private const int EMOJI_PER_HEIGHT = 4;

    // Arrays that holds the random emojis
    private struct Emoji
    {
        public int index;      // Index inside `emojiCodepoints`
        public int message;    // Message index
        public Color color;    // Emoji color
    }

    private static readonly Emoji[] emoji = new Emoji[EMOJI_PER_WIDTH*EMOJI_PER_HEIGHT];

    private static int hovered = -1;
    private static int selected = -1;

    // The 180 emojis the background draws from, each a character past U+FFFF
    private static readonly string[] emojiCodepoints =
    [
        "\U0001F300", "\U0001F600", "\U0001F602", "\U0001F923", "\U0001F603", "\U0001F606", "\U0001F609", "\U0001F60B",
        "\U0001F60E", "\U0001F60D", "\U0001F618", "\U0001F617", "\U0001F619", "\U0001F61A", "\U0001F642", "\U0001F917",
        "\U0001F929", "\U0001F914", "\U0001F928", "\U0001F610", "\U0001F611", "\U0001F636", "\U0001F644", "\U0001F60F",
        "\U0001F623", "\U0001F625", "\U0001F62E", "\U0001F910", "\U0001F62F", "\U0001F62A", "\U0001F62B", "\U0001F634",
        "\U0001F60C", "\U0001F61B", "\U0001F61D", "\U0001F924", "\U0001F612", "\U0001F615", "\U0001F643", "\U0001F911",
        "\U0001F632", "\U0001F641", "\U0001F616", "\U0001F61E", "\U0001F61F", "\U0001F624", "\U0001F622", "\U0001F62D",
        "\U0001F626", "\U0001F629", "\U0001F92F", "\U0001F62C", "\U0001F630", "\U0001F631", "\U0001F633", "\U0001F92A",
        "\U0001F635", "\U0001F621", "\U0001F620", "\U0001F92C", "\U0001F637", "\U0001F912", "\U0001F915", "\U0001F922",
        "\U0001F92E", "\U0001F927", "\U0001F607", "\U0001F920", "\U0001F92B", "\U0001F92D", "\U0001F9D0", "\U0001F913",
        "\U0001F608", "\U0001F47F", "\U0001F479", "\U0001F47A", "\U0001F480", "\U0001F47B", "\U0001F47D", "\U0001F47E",
        "\U0001F916", "\U0001F4A9", "\U0001F63A", "\U0001F638", "\U0001F639", "\U0001F63B", "\U0001F63D", "\U0001F640",
        "\U0001F63F", "\U0001F33E", "\U0001F33F", "\U0001F340", "\U0001F343", "\U0001F347", "\U0001F353", "\U0001F95D",
        "\U0001F345", "\U0001F965", "\U0001F951", "\U0001F346", "\U0001F954", "\U0001F955", "\U0001F33D", "\U0001F336",
        "\U0001F952", "\U0001F966", "\U0001F344", "\U0001F95C", "\U0001F330", "\U0001F35E", "\U0001F950", "\U0001F956",
        "\U0001F968", "\U0001F95E", "\U0001F9C0", "\U0001F356", "\U0001F357", "\U0001F969", "\U0001F953", "\U0001F354",
        "\U0001F35F", "\U0001F355", "\U0001F32D", "\U0001F96A", "\U0001F32E", "\U0001F32F", "\U0001F959", "\U0001F95A",
        "\U0001F373", "\U0001F958", "\U0001F372", "\U0001F963", "\U0001F957", "\U0001F37F", "\U0001F96B", "\U0001F371",
        "\U0001F358", "\U0001F35D", "\U0001F360", "\U0001F362", "\U0001F365", "\U0001F361", "\U0001F95F", "\U0001F961",
        "\U0001F366", "\U0001F36A", "\U0001F382", "\U0001F370", "\U0001F967", "\U0001F36B", "\U0001F36F", "\U0001F37C",
        "\U0001F95B", "\U0001F375", "\U0001F376", "\U0001F37E", "\U0001F377", "\U0001F37B", "\U0001F942", "\U0001F943",
        "\U0001F964", "\U0001F962", "\U0001F441", "\U0001F445", "\U0001F444", "\U0001F48B", "\U0001F498", "\U0001F493",
        "\U0001F497", "\U0001F499", "\U0001F49B", "\U0001F9E1", "\U0001F49C", "\U0001F5A4", "\U0001F49D", "\U0001F49F",
        "\U0001F48C", "\U0001F4A4", "\U0001F4A2", "\U0001F4A3",
    ];

    // Array containing all of the emojis messages
    private static readonly (string text, string language)[] messages =
    [
        ("Falsches Üben von Xylophonmusik quält jeden größeren Zwerg", "German"),
        ("Beiß nicht in die Hand, die dich füttert.", "German"),
        ("Außerordentliche Übel erfordern außerordentliche Mittel.", "German"),
        ("Կրնամ ապակի ուտել և ինծի անհանգիստ չըներ", "Armenian"),
        ("Երբ որ կացինը եկաւ անտառ, ծառերը ասացին... «Կոտը մերոնցից է:»", "Armenian"),
        ("Գառը՝ գարնան, ձիւնը՝ ձմռան", "Armenian"),
        ("Jeżu klątw, spłódź Finom część gry hańb!", "Polish"),
        ("Dobrymi chęciami jest piekło wybrukowane.", "Polish"),
        ("Îți mulțumesc că ai ales raylib.\nȘi sper să ai o zi bună!", "Romanian"),
        ("Эх, чужак, общий съём цен шляп (юфть) вдрызг!", "Russian"),
        ("Я люблю raylib!", "Russian"),
        ("Молчи, скрывайся и таи\nИ чувства и мечты свои \u2013\nПускай в душевной глубине\nИ всходят и зайдут оне\nКак звезды ясные в ночи-\nЛюбуйся ими \u2013 и молчи.", "Russian"),
        ("Voix ambiguë d’un cœur qui au zéphyr préfère les jattes de kiwi", "French"),
        ("Benjamín pidió una bebida de kiwi y fresa; Noé, sin vergüenza, la más exquisita champaña del menú.", "Spanish"),
        ("Ταχίστη αλώπηξ βαφής ψημένη γη, δρασκελίζει υπέρ νωθρού κυνός", "Greek"),
        ("Η καλύτερη άμυνα είναι η επίθεση.", "Greek"),
        ("Χρόνια και ζαμάνια!", "Greek"),
        ("Πώς τα πας σήμερα;", "Greek"),
        ("我能吞下玻璃而不伤身体。", "Chinese"),
        ("你吃了吗？", "Chinese"),
        ("不作不死。", "Chinese"),
        ("最近好吗？", "Chinese"),
        ("塞翁失马，焉知非福。", "Chinese"),
        ("千军易得, 一将难求", "Chinese"),
        ("万事开头难。", "Chinese"),
        ("风无常顺，兵无常胜。", "Chinese"),
        ("活到老，学到老。", "Chinese"),
        ("一言既出，驷马难追。", "Chinese"),
        ("路遥知马力，日久见人心", "Chinese"),
        ("有理走遍天下，无理寸步难行。", "Chinese"),
        ("猿も木から落ちる", "Japanese"),
        ("亀の甲より年の功", "Japanese"),
        ("うらやまし  思ひ切る時  猫の恋", "Japanese"),
        ("虎穴に入らずんば虎子を得ず。", "Japanese"),
        ("二兎を追う者は一兎をも得ず。", "Japanese"),
        ("馬鹿は死ななきゃ治らない。", "Japanese"),
        ("枯野路に　影かさなりて　わかれけり", "Japanese"),
        ("繰り返し麦の畝縫ふ胡蝶哉", "Japanese"),
        ("아득한 바다 위에 갈매기 두엇 날아 돈다.\n너훌너훌 시를 쓴다. 모르는 나라 글자다.\n널따란 하늘 복판에 나도 같이 시를 쓴다.", "Korean"),
        ("제 눈에 안경이다", "Korean"),
        ("꿩 먹고 알 먹는다", "Korean"),
        ("로마는 하루아침에 이루어진 것이 아니다", "Korean"),
        ("고생 끝에 낙이 온다", "Korean"),
        ("개천에서 용 난다", "Korean"),
        ("안녕하세요?", "Korean"),
        ("만나서 반갑습니다", "Korean"),
        ("한국말 하실 줄 아세요?", "Korean"),
    ];

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VsyncHint);
        InitWindow(screenWidth, screenHeight, "[text] unicode emojis");

        // Load the font resources
        // NOTE: fontAsian is for asian languages,
        // fontEmoji is the emojis and fontDefault is used for everything else
        Font fontDefault = LoadFont("resources/dejavu.fnt"); // Requires "resources/dejavu.png"
        Font fontAsian = LoadFont("resources/noto_cjk.fnt"); // Requires "resources/noto_cjk.png"
        Font fontEmoji = LoadFont("resources/symbola.fnt"); // Requires "resources/symbola.png"

        Vector2 hoveredPos = new(0.0f, 0.0f);
        Vector2 selectedPos = new(0.0f, 0.0f);

        // Set a random set of emojis when starting up
        RandomizeEmoji();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // Add a new set of emojis when SPACE is pressed
            if (IsKeyPressed(Key.Space)) RandomizeEmoji();

            // Set the selected emoji
            if (IsMouseButtonPressed(MouseButton.Left) && (hovered != -1) && (hovered != selected))
            {
                selected = hovered;
                selectedPos = hoveredPos;
            }

            Vector2 mouse = GetMousePosition();
            Vector2 position = new(28.8f, 10.0f);
            hovered = -1;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // Draw random emojis in the background
                for (int i = 0; i < emoji.Length; i++)
                {
                    string txt = emojiCodepoints[emoji[i].index];
                    Rectangle emojiRect = new(position.X, position.Y, (float)fontEmoji.BaseSize, (float)fontEmoji.BaseSize);

                    if (!CheckCollisionPointRec(mouse, emojiRect))
                    {
                        DrawTextEx(fontEmoji, txt, position, (float)fontEmoji.BaseSize, 1.0f, selected == i ? emoji[i].color : Fade(Color.LightGray, 0.4f));
                    }
                    else
                    {
                        DrawTextEx(fontEmoji, txt, position, (float)fontEmoji.BaseSize, 1.0f, emoji[i].color);
                        hovered = i;
                        hoveredPos = position;
                    }

                    if ((i != 0) && (i%EMOJI_PER_WIDTH == 0)) { position.Y += fontEmoji.BaseSize + 24.25f; position.X = 28.8f; }
                    else position.X += fontEmoji.BaseSize + 28.8f;
                }

                // Draw the message when a emoji is selected
                if (selected != -1)
                {
                    int message = emoji[selected].message;
                    const int horizontalPadding = 20, verticalPadding = 30;
                    Font font = fontDefault;

                    // Set correct font for asian languages
                    if (messages[message].language == "Chinese" ||
                        messages[message].language == "Korean" ||
                        messages[message].language == "Japanese") font = fontAsian;

                    // Calculate size for the message box (approximate the height and width)
                    Vector2 sz = MeasureTextEx(font, messages[message].text, (float)font.BaseSize, 1.0f);
                    if (sz.X > 300) { sz.Y *= sz.X/300; sz.X = 300; }
                    else if (sz.X < 160) sz.X = 160;

                    Rectangle msgRect = new(selectedPos.X - 38.8f, selectedPos.Y, 2*horizontalPadding + sz.X, 2*verticalPadding + sz.Y);
                    msgRect = msgRect with { Y = msgRect.Y - msgRect.Height };

                    // Coordinates for the chat bubble triangle
                    Vector2 a = new(selectedPos.X, msgRect.Y + msgRect.Height), b = new(a.X + 8, a.Y + 10), c = new(a.X + 10, a.Y);

                    // Don't go outside the screen
                    if (msgRect.X < 10) msgRect = msgRect with { X = msgRect.X + 28 };
                    if (msgRect.Y < 10)
                    {
                        msgRect = msgRect with { Y = selectedPos.Y + 84 };
                        a.Y = msgRect.Y;
                        c.Y = a.Y;
                        b.Y = a.Y - 10;

                        // Swap values so the triangle is counterclockwise and drawn
                        (a, b) = (b, a);
                    }

                    if (msgRect.X + msgRect.Width > screenWidth) msgRect = msgRect with { X = msgRect.X - ((msgRect.X + msgRect.Width) - screenWidth + 10) };

                    // Draw chat bubble
                    DrawRectangleRec(msgRect, emoji[selected].color);
                    DrawTriangle(a, b, c, emoji[selected].color);

                    // Draw the main text message
                    Rectangle textRect = new(msgRect.X + (float)horizontalPadding/2, msgRect.Y + (float)verticalPadding/2, msgRect.Width - horizontalPadding, msgRect.Height);
                    DrawTextBoxed(font, messages[message].text, textRect, (float)font.BaseSize, 1.0f, true, Color.White);

                    // Draw the info text below the main message
                    int size = Encoding.UTF8.GetByteCount(messages[message].text);
                    int length = messages[message].text.EnumerateRunes().Count();
                    string info = $"{messages[message].language} {length} characters {size} bytes";
                    sz = MeasureTextEx(GetFontDefault(), info, 10, 1.0f);

                    DrawText(info, (int)(textRect.X + textRect.Width - sz.X), (int)(msgRect.Y + msgRect.Height - sz.Y - 2), 10, Color.RayWhite);
                }

                // Draw the info text
                DrawText("These emojis have something to tell you, click each to find out!", (screenWidth - 650)/2, screenHeight - 40, 20, Color.Gray);
                DrawText("Each emoji is a unicode character from a font, not a texture... Press [SPACEBAR] to refresh", (screenWidth - 484)/2, screenHeight - 16, 10, Color.Gray);

            EndDrawing();
        }

        UnloadFont(fontDefault);    // Unload font resource
        UnloadFont(fontAsian);      // Unload font resource
        UnloadFont(fontEmoji);      // Unload font resource

        CloseWindow();
    }

    // Fills the emoji array with random emoji (only those emojis present in fontEmoji)
    private static void RandomizeEmoji()
    {
        hovered = selected = -1;
        int start = GetRandomValue(45, 360);

        for (int i = 0; i < emoji.Length; i++)
        {
            // One of the 180 emoji, each an entry of its own here where raylib's are 5 bytes apart
            emoji[i].index = GetRandomValue(0, 179);

            // Generate a random color for this emoji
            emoji[i].color = Fade(ColorFromHSV((float)((start*(i + 1))%360), 0.6f, 0.85f), 0.8f);

            // Set a random message for this emoji
            emoji[i].message = GetRandomValue(0, messages.Length - 1);
        }
    }

    // Draw text using font inside rectangle limits
    private static void DrawTextBoxed(Font font, string text, Rectangle rec, float fontSize, float spacing, bool wordWrap, Color tint)
    {
        DrawTextBoxedSelectable(font, text, rec, fontSize, spacing, wordWrap, tint, 0, 0, Color.White, Color.White);
    }

    // The code point at a byte of UTF-8 text and how many bytes it takes, '?' and one byte for a
    // byte that begins none, as raylib's GetCodepoint reads it
    private static int GetCodepoint(ReadOnlySpan<byte> text, out int codepointByteCount)
    {
        if (Rune.DecodeFromUtf8(text, out Rune rune, out codepointByteCount) != System.Buffers.OperationStatus.Done)
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

    // Draw text using font inside rectangle limits with support for text selection
    private static void DrawTextBoxedSelectable(Font font, string text, Rectangle rec, float fontSize, float spacing, bool wordWrap, Color tint, int selectStart, int selectLength, Color selectTint, Color selectBackTint)
    {
        // The text as UTF-8, which raylib scans a code point at a time by its bytes
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        int length = bytes.Length;      // Total length in bytes of the text, scanned by codepoints in loop

        float textOffsetY = 0;          // Offset between lines (on line break '\n')
        float textOffsetX = 0.0f;       // Offset X to next character to draw

        float scaleFactor = fontSize/(float)font.BaseSize;     // Character rectangle scaling factor

        // Word/character wrapping mechanism variables
        const int MEASURE_STATE = 0, DRAW_STATE = 1;
        int state = wordWrap? MEASURE_STATE : DRAW_STATE;

        int startLine = -1;         // Index where to begin drawing (where a line begins)
        int endLine = -1;           // Index where to stop drawing (where a line ends)
        int lastk = -1;             // Holds last value of the character position

        for (int i = 0, k = 0; i < length; i++, k++)
        {
            // Get next codepoint from byte string
            int codepoint = GetCodepoint(bytes.AsSpan(i), out int codepointByteCount);

            // NOTE: Normally we exit the decoding sequence as soon as a bad byte is found (and return 0x3f)
            // but we need to draw all of the bad bytes using the '?' symbol moving one byte
            if (codepoint == 0x3f) codepointByteCount = 1;
            i += (codepointByteCount - 1);

            float glyphWidth = 0;
            if (codepoint != '\n')
            {
                glyphWidth = GlyphAdvance(font, codepoint)*scaleFactor;

                if (i + 1 < length) glyphWidth = glyphWidth + spacing;
            }

            // NOTE: When wordWrap is ON we first measure how much of the text we can draw before going outside of the rec container
            // We store this info in startLine and endLine, then we change states, draw the text between those two variables
            // and change states again and again recursively until the end of the text (or until we get outside of the container)
            // When wordWrap is OFF we don't need the measure state so we go to the drawing state immediately
            // and begin drawing on the next line before we can get outside the container
            if (state == MEASURE_STATE)
            {
                // There are multiple types of spaces in UNICODE, which this leaves out
                // Ref: http://jkorpela.fi/chars/spaces.html
                if ((codepoint == ' ') || (codepoint == '\t') || (codepoint == '\n')) endLine = i;

                if ((textOffsetX + glyphWidth) > rec.Width)
                {
                    endLine = (endLine < 1)? i : endLine;
                    if (i == endLine) endLine -= codepointByteCount;
                    if ((startLine + codepointByteCount) == endLine) endLine = (i - codepointByteCount);

                    state = 1 - state;
                }
                else if ((i + 1) == length)
                {
                    endLine = i;
                    state = 1 - state;
                }
                else if (codepoint == '\n') state = 1 - state;

                if (state == DRAW_STATE)
                {
                    textOffsetX = 0;
                    i = startLine;
                    glyphWidth = 0;

                    // Save character position when we switch states
                    int tmp = lastk;
                    lastk = k - 1;
                    k = tmp;
                }
            }
            else
            {
                if (codepoint == '\n')
                {
                    if (!wordWrap)
                    {
                        textOffsetY += (font.BaseSize + (float)font.BaseSize/2)*scaleFactor;
                        textOffsetX = 0;
                    }
                }
                else
                {
                    if (!wordWrap && ((textOffsetX + glyphWidth) > rec.Width))
                    {
                        textOffsetY += (font.BaseSize + (float)font.BaseSize/2)*scaleFactor;
                        textOffsetX = 0;
                    }

                    // When text overflows rectangle height limit, stop drawing
                    if ((textOffsetY + font.BaseSize*scaleFactor) > rec.Height) break;

                    // Draw selection background
                    bool isGlyphSelected = false;
                    if ((selectStart >= 0) && (k >= selectStart) && (k < (selectStart + selectLength)))
                    {
                        DrawRectangleRec(new Rectangle(rec.X + textOffsetX - 1, rec.Y + textOffsetY, glyphWidth, (float)font.BaseSize*scaleFactor), selectBackTint);
                        isGlyphSelected = true;
                    }

                    // Draw current character glyph
                    if ((codepoint != ' ') && (codepoint != '\t'))
                    {
                        DrawTextCodepoint(font, codepoint, new Vector2(rec.X + textOffsetX, rec.Y + textOffsetY), fontSize, isGlyphSelected? selectTint : tint);
                    }
                }

                if (wordWrap && (i == endLine))
                {
                    textOffsetY += (font.BaseSize + (float)font.BaseSize/2)*scaleFactor;
                    textOffsetX = 0;
                    startLine = endLine;
                    endLine = -1;
                    glyphWidth = 0;
                    selectStart += lastk - k;
                    k = lastk;

                    state = 1 - state;
                }
            }

            textOffsetX += glyphWidth;
        }
    }
}

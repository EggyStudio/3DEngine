# Text and fonts

Text is drawn like any shape, in the frame's order, into the window or a render texture. The
default font needs no file, and a TrueType or OpenType font loaded from one draws the same way
through a `Font` value.

## The default font

`DrawText` draws in the default font with its top left corner at a pixel, at a size in pixels.
`MeasureText` says how wide the same call would draw, which centers text and places what follows
it. The `text_fonts` example draws it at five sizes:

```csharp
DrawText("The default font, baked at the size it is drawn", 20, 20, 20, Color.DarkGray);
var y = 60;
for (int size = 10; size <= 30; size += 5)
{
    DrawText($"{size} pixels", 20, y, size, Color.Gray);
    y += size + 8;
}
```

The default font is ProggyClean, which Dear ImGui carries. It is baked again at each size it is
drawn at and kept, so small text stays sharp rather than a large bake scaled down. A size
below 10 is drawn at 10, as raylib's is, since a raylib program that asks for 6 is read at 10.
A newline in the string starts a new line below the first. `DrawFPS(x, y)` draws the frame rate
in it, at 20 pixels in lime, orange below 30 frames a second and red below 15, as raylib's does.

## Fonts from files

`LoadFontEx` reads a font file from beside the program and bakes it at a size, with the Latin-1
characters, which covers English and the languages of western Europe. The file is a TrueType or
OpenType font, or a collection of them (`.ttc`), whose first font is read. A font with no character
map of Unicode, as a symbol font of Windows' Symbol encoding has none, or with outlines of CFF2
alone, as a variable OpenType font may be made, is refused for the default font with the reason in
the log. A font with none of the Latin-1 characters, as one of a single other script is, is baked
with the first character it has, and is given its own characters as the next section shows.
`DrawTextEx` draws in it,
at a size and with extra spacing in pixels between characters, and `MeasureTextEx` gives the width
and height of the same text. From `text_fonts`:

```csharp
// Lato, under the SIL Open Font License (resources/fonts/Lato-OFL.txt).
var lato = LoadFontEx("resources/fonts/Lato-Regular.ttf", 48);
// ...
DrawTextEx(lato, "Lato, from a file", new Vector2(320, 60), 48, 0, Color.Maroon);
DrawTextEx(lato, "scaled down to 24", new Vector2(320, 120), 24, 1, Color.DarkBlue);

var line = "Measured and centered";
var size2 = MeasureTextEx(lato, line, 32, 0);
DrawRectangle((int)(560 - size2.X / 2), 200, (int)size2.X, (int)size2.Y, Color.SkyBlue.Fade(0.4f));
DrawTextEx(lato, line, new Vector2(560 - size2.X / 2, 200), 32, 0, Color.DarkBlue);
// ...
UnloadFont(lato);
```

A font is best baked at the size it is drawn at most. Drawn smaller, its bake is scaled down and
stays sharp. Drawn a quarter or more past it, the font is baked again at the larger size, rounded
up to 4 pixels, and kept for the next frame, up to eight sizes a font, so large text does not blur.
Each glyph of a larger bake is drawn in the box and with the advance of the font's own bake scaled,
so text lies where it would from the one bake, as raylib's does, whose advances are whole pixels at
the size a font is loaded at, as they are here. `LoadFont(fileName)` bakes at 32 pixels, and
`UnloadFont` frees every bake.

## Fonts drawn as images

A pixel-art game often draws its font by hand, every glyph on a row of one image, separated from
the next by a key color. `LoadFontFromImage` reads such an image, and `LoadFont` reads a PNG this
way with magenta as the key and the space as the first glyph, as raylib's does:

```csharp
var pixels = LoadFont("resources/fonts/pixel.png");
DrawTextEx(pixels, "SCORE 1200", new Vector2(10, 10), pixels.BaseSize * 3, 3, Color.White);
```

The gap before the first glyph is the space between glyphs, and the gap above the first row the
space between rows. The atlas is point filtered, so the font drawn at a whole multiple of its size
stays sharp pixels.

A font made with AngelCode's BMFont, a `.fnt` file in its text form with the images of its pages
beside it, is read by `LoadFont` as well, its pages stacked into one atlas and its size its line
height, as raylib reads it. A page of gray alone is the glyphs' coverage, drawn white so the text
takes its tint. `text_font_loading` draws one beside the TrueType font it was made from, and
`text_unicode_emojis` draws emojis, Chinese, Japanese and Korean from three:

```csharp
Font fontBm = LoadFont("resources/pixantiqua.fnt"); // Requires "resources/pixantiqua.png"
DrawTextEx(fontBm, "BMFont", new Vector2(20, 100), fontBm.BaseSize, 2, Color.Maroon);
```

## Characters past Latin-1

A font holds only the characters it was baked with. Text in Greek, Cyrillic or another script, or
with a sign as the euro, gets its characters from `LoadCodepoints`, which lists those a string
uses, so the bake holds those and no more:

```csharp
// Beyond Latin-1, a font is baked with the characters a text needs.
const string World = "Grüße · Γειά σου · Привет · 5 €";
var wide = LoadFontEx("resources/fonts/Lato-Regular.ttf", 28, LoadCodepoints(World));
// ...
DrawTextEx(wide, World, new Vector2(20, 270), 28, 0, Color.DarkPurple);
```

A game with text in several languages bakes one font from all of its strings together. A character
the font lacks, left out of the bake or missing from the file, draws as the font's `?`, as raylib's
does, and as nothing where the font has no `?` either. Characters past U+FFFF, emoji, historic
scripts and the mathematical letters among them, are baked from the font's outlines with the rest,
TrueType's or an OpenType font's of CFF, as Noto Sans CJK's and STIX's are, so a monochrome emoji
font such as Noto Emoji or Symbola draws them.

A color emoji font draws its emoji in their colors, whether it holds them as pictures, as Twemoji,
Apple Color Emoji and the older builds of Noto Color Emoji do, as outlines colored in layers, as
Segoe UI Emoji does, or as outlines filled with gradients and moved by transforms (COLR version 1),
as Noto Color Emoji's current build and the color fonts of Google Fonts do, and text drawn in white
shows them as they are:

```csharp
const string Faces = "😀 😂 😍 🚀 ❤";
var emoji = LoadFontEx("resources/fonts/NotoColorEmoji.ttf", 48, LoadCodepoints(Faces));
DrawTextEx(emoji, Faces, new Vector2(20, 20), 48, 4, Color.White);
```

A sequence a color font joins into one picture is drawn as that picture: a family from its people
and the joiners between them, a flag from two regional indicators, a skin tone from a person and a
modifier, a keycap from a digit and its marks. The font's own substitutions join them, its GSUB
table's `ccmp` feature, applied to each run of emoji in the text, and the pictures they can make of
the characters a font is loaded with are baked with it, so `LoadCodepoints` of the text is enough:

```csharp
const string Sequences = "👨‍👩‍👧 🇯🇵 👍🏽 1️⃣ 🏳️‍🌈";
var joined = LoadFontEx("resources/fonts/Twemoji.ttf", 48, LoadCodepoints(Sequences));
DrawTextEx(joined, Sequences, new Vector2(20, 90), 48, 4, Color.White);
```

A joiner or a variation selector a font does not join is not drawn. The text outside a color
font's emoji is drawn a character at a time, as raylib draws it, so the ligatures and the shaping
of a script such as Arabic are not made. A variable color font's emoji are drawn as its default
instance.

## Typed text

`GetCharPressed` gives the characters typed since the last frame, one call each, in order, with
the keyboard's layout applied, and 0 when there are no more. Keys that type
nothing, as Backspace and the arrows, are read as keys. The `text_input_box` example:

<!-- compiled with:
const int MAX_INPUT_CHARS = 9;
string name = "";
int letterCount = 0, framesCounter = 0;
Rectangle textBox = default;
-->
```csharp
// Every character typed this frame, in order, those from space to '}' kept
int key = GetCharPressed();
while (key > 0)
{
    if ((key >= 32) && (key <= 125) && (letterCount < MAX_INPUT_CHARS))
    {
        name += (char)key;
        letterCount++;
    }

    key = GetCharPressed();
}

if (IsKeyPressed(Key.Backspace))
{
    letterCount--;
    if (letterCount < 0) letterCount = 0;
    name = name[..letterCount];
}
// ...
DrawText(name, (int)textBox.X + 5, (int)textBox.Y + 8, 40, Color.Maroon);

// A blinking underscore
if (((framesCounter/20)%2) == 0) DrawText("_", (int)textBox.X + 8 + MeasureText(name, 40), (int)textBox.Y + 12, 40, Color.Maroon);
```

A language composed from several keys, as Japanese or Chinese, is typed in the input method's
window, which `SetTextInputArea(textBox)` places beside the box the text lands in, the caret's
distance from its left as the second argument, so the candidates show where the player is looking.
An ImGui text field places it by itself.

A character is an `int` code point rather than a `char`, since one past U+FFFF takes two of C#'s
`char`s, which `char.ConvertFromUtf32` makes. `GetKeyPressed` reads keys the same way, one call
each until `Key.Null`. A program with many fields, or with a console, may draw them with ImGui
instead, which reads typing itself.

## Distance field fonts

A font baked as a signed distance field records how far each pixel is from a character's edge
rather than how much of it is covered, so one bake draws sharp edges at any size without baking
again. `FontType.Sdf` asks for one. The `text_font_sdf` example bakes the same font both ways at
16 pixels, and draws the distance field while the space bar is held, at the size the mouse wheel
sets:

```csharp
// The 95 characters of ASCII, which raylib's font data is made for.
int[] ascii = Enumerable.Range(32, 95).ToArray();
Font fontDefault = LoadFontEx("resources/anonymous_pro_bold.ttf", 16, ascii, FontType.Default);
Font fontSDF = LoadFontEx("resources/anonymous_pro_bold.ttf", 16, ascii, FontType.Sdf);
SetTextureFilter(fontSDF.Texture, TextureFilter.Bilinear);
```

Codepoints given as `null` mean Latin-1, as `LoadFontEx` without them does. Characters past
U+FFFF are baked from the font's outlines too, and a color emoji as its outline's shape, since a
distance field holds a shape and no colors. A distance field font suits text that changes size
every frame, as a title that grows in or a label in a world that zooms, where the coverage font
would bake again at each new size.

## Turned text and text in textures

`DrawTextPro` turns text by a number of degrees about an origin given in the text's own pixels, so
a label turns about its middle with the origin at half its measured size:

<!-- compiled with:
Font lato = default!;
float t = 0;
-->
```csharp
var size = MeasureTextEx(lato, "Bonus!", 32, 0);
DrawTextPro(lato, "Bonus!", new Vector2(400, 225), size / 2, MathF.Sin(t) * 15, 32, 0, Color.Gold);
```

Text reaches render textures like any shape, so a clock face, a sign in a 3D world or a screen
inside a game is drawn into a texture once and then drawn where it belongs. From `text_fonts`,
which draws the time into a texture and shows it turned:

<!-- compiled with:
Font lato = default!;
float t = 0;
RenderTexture2D target = default;
-->
```csharp
// Text reaches render targets like any shape.
BeginTextureMode(target);
ClearBackground(Color.DarkGray);
DrawTextEx(lato, $"{t:0.0} s", new Vector2(20, 30), 48, 0, Color.Gold);
EndTextureMode();
DrawTexturePro(target.Texture, new Rectangle(0, 0, 300, 120), new Rectangle(560, 340, 240, 96),
    new Vector2(120, 48), MathF.Sin(t) * 8, Color.White);
```

Text drawn inside `BeginMode3D` lies on the world's plane z = 0, with a unit for each pixel of its
size. A sign in a 3D world is better drawn into a texture, as above, and put on a model's material
or drawn with `DrawBillboard`.

## See also

- Examples: [`text_fonts`](../3DEngine.Examples/Text/TextFonts.cs),
  [`text_input_box`](../3DEngine.Examples/Text/TextInputBox.cs),
  [`text_font_sdf`](../3DEngine.Examples/Text/TextFontSdf.cs)
- The cheatsheet's [Text and fonts](../CHEATSHEET.md#text-and-fonts) and [Input](../CHEATSHEET.md#input)
- Previous: [Textures and images](textures-and-images.md)
- Next: [Models and animation](models-and-animation.md)

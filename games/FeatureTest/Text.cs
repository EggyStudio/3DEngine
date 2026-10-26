using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// The text, a page drawn into a texture and shown on a billboard, with a line of Lato baked with
/// the characters past Latin-1 it needs, Hebrew read right to left, a colour glyph, the default
/// font, a distance field font turned, and a ligature measured with the font's ligatures on and
/// off.
/// </summary>
/// <remarks>
/// The Hebrew and the colour glyph are drawn in two small fonts the tests carry, made by
/// <c>build/make-color-test-fonts.py</c>, whose alef, bet and gimel are bars 800, 400 and 200 units
/// high and whose U+1F600 is red at its left and blue at its right, so what is drawn is known in
/// advance. Every second the page is read back for the Lato line's width as drawn against
/// <see cref="MeasureTextEx"/>, the Hebrew's bars rising from left to right, as the first letter is
/// drawn at the right, the glyph's red left of its blue, and the default font's line. The text is
/// ok when all of those read as said.
/// </remarks>
sealed class Text : Zone
{
    private const int Width = 360, Height = 160;
    private const string World = "Grüße · Γειά σου · Привет · 5 €", Hebrew = "אבג", Joined = "office";
    private readonly RenderTexture2D _page = LoadRenderTexture(Width, Height);
    private readonly Font _lato = LoadFontEx("resources/fonts/Lato-Regular.ttf", 28, LoadCodepoints(World + Joined));
    private readonly Font _unjoined = LoadFontEx("resources/fonts/Lato-Regular.ttf", 28, LoadCodepoints(Joined));
    private readonly Font _field = LoadFontEx("resources/fonts/Lato-Regular.ttf", 32, null, FontType.Sdf);
    private readonly Font _rtl = LoadFontEx("resources/fonts/rtl.ttf", 32, LoadCodepoints(Hebrew));
    private readonly Font _colour = LoadFontEx("resources/fonts/layers.ttf", 40, [0x1F600]);
    private readonly int _measured;
    private float _time;
    private int _frames, _reads, _drawn = -1;
    private bool _rightToLeft, _coloured, _plain;

    public Text()
    {
        _measured = (int)MathF.Round(MeasureTextEx(_lato, World, 28, 0).X);
        SetFontLigatures(_unjoined, false);
    }

    public override string Name => "Text";
    public override Vector3 Center => Zones.At(247.5f, Zones.Outer);
    public override Vector3 Start => Place(0, -8);

    public override void Update(float dt, Player player)
    {
        _time += dt;
        _frames++;
        if (_frames < 60 || _frames % 60 != 1) return;
        var image = LoadImageFromTexture(_page.Texture);
        if (!IsImageValid(image)) return;
        bool Lit(int x, int y) => GetImageColor(image, x, y) is var c && Math.Max(c.R, Math.Max(c.G, c.B)) > 100;

        // The Lato line's columns from its first lit one to its last.
        int first = -1, last = -1;
        for (var x = 0; x < Width; x++)
            for (var y = 4; y < 42; y++)
                if (Lit(x, y))
                {
                    if (first < 0) first = x;
                    last = x;
                    break;
                }
        _drawn = first < 0 ? 0 : last - first + 1;

        // The bars' heights in the middle of each letter, from the left.
        int Bar(int x) => Enumerable.Range(46, 44).Count(y => Lit(x, y));
        var (left, middle, right) = (Bar(8 + 16), Bar(8 + 48), Bar(8 + 80));
        _rightToLeft = left > 0 && left < middle && middle < right;

        // The glyph's red pixels and its blue ones, and which side of the other each lies.
        double redX = 0, blueX = 0;
        int reds = 0, blues = 0;
        for (var y = 44; y < 96; y++)
            for (var x = 180; x < 240; x++)
            {
                var c = GetImageColor(image, x, y);
                if (c.R > 150 && c.G < 90 && c.B < 90) (reds, redX) = (reds + 1, redX + x);
                else if (c.B > 150 && c.R < 90 && c.G < 90) (blues, blueX) = (blues + 1, blueX + x);
            }
        _coloured = reds > 10 && blues > 10 && redX / reds < blueX / blues;
        _plain = Enumerable.Range(8, 150).Any(x => Enumerable.Range(102, 18).Any(y => Lit(x, y)));
        UnloadImage(image);
        _reads++;
    }

    public override void DrawOffscreen()
    {
        if (_frames % 60 != 0) return;
        BeginTextureMode(_page);
        ClearBackground(Color.Black);
        DrawTextEx(_lato, World, new Vector2(8, 6), 28, 0, Color.White);
        DrawTextEx(_rtl, Hebrew, new Vector2(8, 50), 32, 0, Color.White);
        DrawTextEx(_colour, char.ConvertFromUtf32(0x1F600), new Vector2(190, 48), 40, 0, Color.White);
        DrawText("The default font", 8, 100, 20, Color.White);
        DrawTextEx(_lato, Joined, new Vector2(8, 126), 28, 0, new Color(150, 200, 255));
        DrawTextEx(_unjoined, Joined, new Vector2(110, 126), 28, 0, new Color(150, 200, 255));
        var size = MeasureTextEx(_field, "turned", 30, 1);
        DrawTextPro(_field, "turned", new Vector2(290, 120), size / 2, MathF.Sin(_time) * 15, 30, 1, new Color(255, 210, 120));
        EndTextureMode();
    }

    public override void Draw(Camera3D camera)
    {
        DrawCylinder(Place(0, 0), 0.08f, 0.08f, 2, 8, new Color(110, 80, 50));
        DrawBillboard(camera, _page.Texture, Place(0, 0, 3.2f), 4.5f, Color.White);
    }

    public override string Status()
    {
        if (_reads == 0) return "the page not read yet";
        var measured = Math.Abs(_drawn - _measured) <= 6;
        var ok = measured && _rightToLeft && _coloured && _plain;
        var joined = MeasureTextEx(_lato, Joined, 28, 0).X;
        var apart = MeasureTextEx(_unjoined, Joined, 28, 0).X;
        return $"Lato's line measured {_measured} and drawn {_drawn} pixels wide{(measured ? "" : ", not as measured")}, "
               + $"the Hebrew {(_rightToLeft ? "drawn from right to left" : "not drawn from right to left")}, "
               + $"the colour glyph {(_coloured ? "red at its left and blue at its right" : "not drawn in its colours")}, "
               + $"the default font {(_plain ? "drawn" : "not drawn")}, \"{Joined}\" {joined:0} pixels wide with its ligatures and {apart:0} without, "
               + $"a distance field font turned, {_reads} reads{(ok ? ", text ok" : "")}";
    }
}

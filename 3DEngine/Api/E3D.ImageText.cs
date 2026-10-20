using System.Numerics;

namespace Engine;

public static partial class E3D
{
    /// <summary>Draws text into an image in the default font, as <see cref="DrawText"/> draws it on the screen.</summary>
    public static void ImageDrawText(ref Image dst, string text, int x, int y, int fontSize, Color color) =>
        ImageDrawTextEx(ref dst, GetFontDefault(fontSize), text, new Vector2(x, y), fontSize, 0, color);

    /// <summary>
    /// Draws text into an image in a font, as <see cref="DrawTextEx"/> draws it on the screen, each
    /// glyph scaled from its bake by the nearest pixel and blended by its coverage.
    /// </summary>
    public static void ImageDrawTextEx(ref Image dst, Font font, string text, Vector2 position, float fontSize, float spacing, Color tint)
    {
        if (!font.IsValid || !font.Atlas.IsValid || string.IsNullOrEmpty(text)) return;
        font = font.ForSize(fontSize);

        var scale = fontSize / font.BaseSize;
        var pen = position;
        foreach (var (key, offset, advance) in PlacedKeys(font, text))
        {
            if (key == '\n')
            {
                pen = new Vector2(position.X, pen.Y + LineAdvance(font, fontSize));
                continue;
            }
            if (!TryGetGlyph(font, key, out var g)) continue;
            if (g.X1 > g.X0 && g.Y1 > g.Y0)
            {
                var source = new Rectangle(g.U0 * font.Atlas.Width, g.V0 * font.Atlas.Height,
                    (g.U1 - g.U0) * font.Atlas.Width, (g.V1 - g.V0) * font.Atlas.Height);
                var at = pen + offset * scale;
                var target = new Rectangle(at.X + g.X0 * scale, at.Y + g.Y0 * scale, (g.X1 - g.X0) * scale, (g.Y1 - g.Y0) * scale);
                ImageDraw(ref dst, font.Atlas, source, target, tint);
            }
            pen.X += (advance ?? g.Advance) * scale + spacing;
        }
    }

    /// <summary>A new image holding text in the default font, as large as the text, clear around it.</summary>
    /// <remarks>A size below 10 is drawn at 10, as <see cref="DrawText"/> draws it.</remarks>
    public static Image ImageText(string text, int fontSize, Color color)
    {
        fontSize = DefaultTextSize(fontSize);
        return ImageTextEx(GetFontDefault(fontSize), text, fontSize, 0, color);
    }

    /// <summary>A new image holding text in a font, as large as the text, clear around it.</summary>
    public static Image ImageTextEx(Font font, string text, float fontSize, float spacing, Color tint)
    {
        var size = MeasureTextEx(font, text, fontSize, spacing);
        var image = GenImageColor(Math.Max(1, (int)MathF.Ceiling(size.X)), Math.Max(1, (int)MathF.Ceiling(size.Y)), Color.Blank);
        ImageDrawTextEx(ref image, font, text, Vector2.Zero, fontSize, spacing, tint);
        return image;
    }
}

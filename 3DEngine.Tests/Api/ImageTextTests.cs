using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>Text drawn into an image, through the default font's atlas, in an app whose stores queue without a GPU.</summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class ImageTextTests : IDisposable
{
    private readonly App _app = new();

    public ImageTextTests()
    {
        _app.World.InitResource<TextureStore>();
        UseApp(_app);
    }

    public void Dispose() => UseApp(null);

    [Fact]
    public void Text_Is_Drawn_Into_An_Image_Where_The_Screen_Would_Have_It()
    {
        var image = GenImageColor(64, 32, Color.Black);
        ImageDrawText(ref image, "Hi", 4, 4, 20, new Color(255, 0, 0));

        var red = new List<Vector2>();
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
            if (GetImageColor(image, x, y).R > 128) red.Add(new Vector2(x, y));

        red.Should().NotBeEmpty("the glyphs cover some pixels");
        red.Min(p => p.X).Should().BeGreaterThanOrEqualTo(4, "nothing is drawn left of where the text starts");
        red.Max(p => p.X).Should().BeLessThan(4 + MeasureText("Hi", 20) + 1, "nor past its measured width");
        red.Should().OnlyContain(p => GetImageColor(image, (int)p.X, (int)p.Y).G == 0, "the glyphs take the text's color");
    }
}

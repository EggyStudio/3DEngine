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

    [Fact]
    public void ImageText_Makes_An_Image_As_Large_As_The_Text_Clear_Around_It()
    {
        var image = ImageText("Hello", 20, Color.Red);

        image.Width.Should().Be(MeasureText("Hello", 20));
        image.Height.Should().BeGreaterThanOrEqualTo(20);
        var colors = LoadImageColors(image);
        colors.Should().HaveCount(image.Width * image.Height);
        colors.Should().Contain(c => c.R > 128 && c.A > 128, "the glyphs are drawn");
        colors.Should().Contain(c => c.A == 0, "and around them it is clear");
        colors[image.Width + 1].Should().Be(GetImageColor(image, 1, 1), "the colors are row by row");
    }

    [Fact]
    public void Default_Text_Below_Ten_Pixels_Is_Ten_High_As_In_Raylib()
    {
        MeasureText("Sine 0.50", 6).Should().Be(MeasureText("Sine 0.50", 10), "raylib raises a smaller size to 10");
        MeasureText("Sine 0.50", 12).Should().BeGreaterThan(MeasureText("Sine 0.50", 10), "and leaves a larger one");

        var small = ImageText("Sine", 6, Color.Red);
        var ten = ImageText("Sine", 10, Color.Red);
        (small.Width, small.Height).Should().Be((ten.Width, ten.Height), "ImageText raises it as well");
    }
}

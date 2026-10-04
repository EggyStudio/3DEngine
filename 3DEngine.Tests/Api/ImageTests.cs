using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>The flat API's image functions, which work on memory and need no app.</summary>
[Trait("Category", "Unit")]
public class ImageTests
{
    private static readonly Color Red = new(255, 0, 0), Blue = new(0, 0, 255);

    // A 3 by 2 image whose pixels are numbered left to right, top to bottom, in their red byte.
    private static Image Numbered()
    {
        var image = GenImageColor(3, 2, Color.Black);
        for (int i = 0; i < 6; i++) ImageDrawPixel(ref image, i % 3, i / 3, new Color((byte)i, 0, 0));
        return image;
    }

    private static int[] Reds(Image image) =>
        Enumerable.Range(0, image.Width * image.Height).Select(i => (int)image.Data[i * 4]).ToArray();

    [Fact]
    public void Flips_And_Quarter_Turns_Move_Pixels_Where_Expected()
    {
        var image = Numbered();
        ImageFlipHorizontal(ref image);
        Reds(image).Should().Equal(2, 1, 0, 5, 4, 3);

        image = Numbered();
        ImageFlipVertical(ref image);
        Reds(image).Should().Equal(3, 4, 5, 0, 1, 2);

        image = Numbered();
        ImageRotateCW(ref image);
        (image.Width, image.Height).Should().Be((2, 3));
        Reds(image).Should().Equal(3, 0, 4, 1, 5, 2);

        ImageRotateCCW(ref image);
        Reds(image).Should().Equal(Reds(Numbered()));
    }

    [Fact]
    public void Crop_And_FromImage_Clip_To_The_Image()
    {
        var image = Numbered();
        Reds(ImageFromImage(image, new Rectangle(1, 0, 5, 1))).Should().Equal(1, 2);

        ImageCrop(ref image, new Rectangle(1, 1, 2, 1));
        Reds(image).Should().Equal(4, 5);
    }

    [Fact]
    public void ImageCopy_Does_Not_Share_Pixels()
    {
        var image = Numbered();
        var copy = ImageCopy(image);
        ImageClearBackground(ref copy, Color.White);

        Reds(image).Should().Equal(0, 1, 2, 3, 4, 5);
    }

    [Fact]
    public void Resizing_By_Nearest_Repeats_Pixels_And_Bilinear_Blends_Them()
    {
        var image = GenImageColor(2, 1, Color.Black);
        ImageDrawPixel(ref image, 1, 0, Color.White);

        var nearest = ImageCopy(image);
        ImageResizeNN(ref nearest, 4, 2);
        Reds(nearest).Should().Equal(0, 0, 255, 255, 0, 0, 255, 255);

        ImageResize(ref image, 4, 1);
        Reds(image).Should().Equal(0, 64, 191, 255);
    }

    [Fact]
    public void ResizeCanvas_Places_The_Image_And_Fills_The_Rest()
    {
        var image = GenImageColor(1, 1, Red);
        ImageResizeCanvas(ref image, 3, 1, 1, 0, Blue);

        Enumerable.Range(0, 3).Select(x => GetImageColor(image, x, 0)).Should().Equal(Blue, Red, Blue);
    }

    [Fact]
    public void Color_Adjustments_Change_Every_Pixel()
    {
        var image = GenImageColor(1, 1, new Color(100, 150, 200, 77));

        ImageColorInvert(ref image);
        GetImageColor(image, 0, 0).Should().Be(new Color(155, 105, 55, 77));

        ImageColorBrightness(ref image, 120);
        GetImageColor(image, 0, 0).Should().Be(new Color(255, 225, 175, 77));

        ImageColorGrayscale(ref image);
        var gray = GetImageColor(image, 0, 0);
        (gray.R == gray.G && gray.G == gray.B && gray.A == 77).Should().BeTrue();

        ImageColorContrast(ref image, -100);
        GetImageColor(image, 0, 0).R.Should().Be(127);

        ImageColorReplace(ref image, new Color(127, 127, 127, 77), Red);
        GetImageColor(image, 0, 0).Should().Be(Red);

        ImageColorTint(ref image, new Color(255, 255, 255, 128));
        GetImageColor(image, 0, 0).Should().Be(new Color(255, 0, 0, 128));
    }

    [Fact]
    public void A_Line_Reaches_Both_Ends_And_A_Circle_Is_Symmetric()
    {
        var image = GenImageColor(9, 9, Color.Black);
        ImageDrawLine(ref image, 0, 0, 8, 4, Color.White);
        GetImageColor(image, 0, 0).Should().Be(Color.White);
        GetImageColor(image, 8, 4).Should().Be(Color.White);
        Reds(image).Count(r => r == 255).Should().Be(9, "a line steps one pixel at a time along its longer axis");

        var circle = GenImageColor(9, 9, Color.Black);
        ImageDrawCircleLines(ref circle, 4, 4, 3, Color.White);
        var flipped = ImageCopy(circle);
        ImageFlipHorizontal(ref flipped);
        Reds(flipped).Should().Equal(Reds(circle));
        GetImageColor(circle, 7, 4).Should().Be(Color.White);
        GetImageColor(circle, 4, 4).Should().Be(Color.Black);

        var disc = GenImageColor(9, 9, Color.Black);
        ImageDrawCircle(ref disc, 4, 4, 3, Color.White);
        Reds(disc).Count(r => r == 255).Should().Be(29);
    }

    [Fact]
    public void Rectangles_Clip_To_The_Image_And_Outlines_Stay_Inside_Their_Edge()
    {
        var image = GenImageColor(4, 4, Color.Black);
        ImageDrawRectangle(ref image, -2, -2, 4, 4, Color.White);
        Reds(image).Count(r => r == 255).Should().Be(4);

        var outline = GenImageColor(4, 4, Color.Black);
        ImageDrawRectangleLines(ref outline, new Rectangle(0, 0, 4, 4), 1, Color.White);
        Reds(outline).Count(r => r == 255).Should().Be(12);
        GetImageColor(outline, 1, 1).Should().Be(Color.Black);
    }

    [Fact]
    public void ImageDraw_Scales_The_Source_And_Blends_It_By_Alpha()
    {
        var destination = GenImageColor(4, 2, Blue);
        var source = GenImageColor(1, 1, new Color(255, 0, 0, 128));

        ImageDraw(ref destination, source, new Rectangle(0, 0, 1, 1), new Rectangle(2, 0, 2, 2), Color.White);

        GetImageColor(destination, 0, 0).Should().Be(Blue);
        GetImageColor(destination, 3, 1).Should().Be(new Color(128, 0, 127, 255));
    }

    [Fact]
    public void Gradients_Run_From_Their_Start_To_Their_End()
    {
        var vertical = GenImageGradientLinear(1, 5, 0, Color.Black, Color.White);
        GetImageColor(vertical, 0, 0).Should().Be(Color.Black);
        GetImageColor(vertical, 0, 4).Should().Be(Color.White);

        var horizontal = GenImageGradientLinear(5, 1, 90, Color.Black, Color.White);
        GetImageColor(horizontal, 0, 0).Should().Be(Color.Black);
        GetImageColor(horizontal, 4, 0).Should().Be(Color.White);

        var radial = GenImageGradientRadial(8, 8, 0, Color.White, Color.Black);
        GetImageColor(radial, 4, 4).R.Should().BeGreaterThan(200);
        GetImageColor(radial, 0, 0).Should().Be(Color.Black);

        var noise = GenImageWhiteNoise(64, 64, 0.5f);
        Reds(noise).Count(r => r == 255).Should().BeInRange(1500, 2600);
    }

    [Fact]
    public void ExportImage_Writes_A_Png_That_Loads_Back()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("engine-image-").FullName, "out.png");
        var image = Numbered();

        ExportImage(image, path).Should().BeTrue();

        var loaded = StbImageSharp.ImageResult.FromMemory(File.ReadAllBytes(path), StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        loaded.Data.Should().Equal(image.Data);
    }

    [Fact]
    public void Perlin_Noise_Is_Gray_And_Middle_Gray_Where_It_Is_Sampled_At_Whole_Numbers()
    {
        var flat = GenImagePerlinNoise(8, 8, 0, 0, 0);
        flat.Data.Chunk(4).Should().OnlyContain(p => p[0] == 127 && p[1] == 127 && p[2] == 127, "noise is zero at whole coordinates");

        var noise = GenImagePerlinNoise(64, 64, 10, 20, 4);
        noise.Data.Chunk(4).Should().OnlyContain(p => p[0] == p[1] && p[1] == p[2]);
        noise.Data.Chunk(4).Select(p => p[0]).Distinct().Count().Should().BeGreaterThan(20, "it varies across the image");
        GenImagePerlinNoise(64, 64, 10, 20, 4).Data.Should().Equal(noise.Data, "the same arguments give the same image");
    }

    [Fact]
    public void Cellular_Noise_Is_Dark_At_Its_Points_And_Gray_Throughout()
    {
        var cells = GenImageCellular(64, 64, 16);

        var grays = cells.Data.Chunk(4).ToArray();
        grays.Should().OnlyContain(p => p[0] == p[1] && p[1] == p[2]);
        grays.Count(p => p[0] == 0).Should().BeGreaterThanOrEqualTo(16, "each of the 16 squares has its point at distance 0");
        grays.Max(p => p[0]).Should().BeGreaterThan(64, "far from every point is lighter");
    }

    // Two frames of 4 by 2, red then blue, as Pillow writes an animated GIF.
    private static readonly byte[] TwoFrames =
    [
        71, 73, 70, 56, 57, 97, 4, 0, 2, 0, 129, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 33, 255, 11, 78, 69, 84, 83,
        67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0, 33, 249, 4, 4, 10, 0, 0, 0, 44, 0, 0, 0, 0, 4, 0, 2, 0, 0, 8, 7, 0, 1,
        8, 28, 40, 48, 32, 0, 33, 249, 4, 5, 10, 0, 1, 0, 44, 0, 0, 0, 0, 4, 0, 2, 0, 129, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 8, 7, 0, 1, 8, 28, 40, 48, 32, 0, 59,
    ];

    [Fact]
    public void An_Animated_Gif_Loads_As_Its_Frames_Stacked_From_The_Top()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory("engine-gif-").FullName, "two.gif");
        File.WriteAllBytes(file, TwoFrames);

        var image = LoadImageAnim(file, out var frames);

        frames.Should().Be(2);
        (image.Width, image.Height).Should().Be((4, 4));
        GetImageColor(image, 1, 0).Should().Be(Red);
        GetImageColor(image, 1, 3).Should().Be(Blue, "the second frame is below the first");
    }

    [Fact]
    public void An_Image_Exported_To_Memory_Reads_Back_The_Same_And_Files_Keep_Their_Bytes()
    {
        var image = Numbered();
        var png = ExportImageToMemory(image, ".png");
        var back = StbImageSharp.ImageResult.FromMemory(png, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        back.Data.Should().Equal(image.Data);
        ExportImageToMemory(image, ".bmp").Should().BeEmpty("PNG is the type written");

        var file = Path.Combine(Directory.CreateTempSubdirectory("engine-data-").FullName, "save.bin");
        SaveFileData(file, png).Should().BeTrue();
        LoadFileData(file).Should().Equal(png);
        LoadFileData(file + ".missing").Should().BeNull();
    }
}

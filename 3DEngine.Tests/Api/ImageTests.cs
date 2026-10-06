using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>The flat API's image functions, which work on memory and need no app.</summary>
[Trait("Category", "Unit")]
public sealed class ImageTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-image-");

    public void Dispose() => _folder.Dispose();

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
        var path = _folder.File("out.png");
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

    // Pixels of raylib's own GenImagePerlinNoise, built from its rtextures.c and stb_perlin.h, for a
    // square image and for one wider and one taller, whose wider side spans more of the noise.
    [Theory]
    [InlineData(512, 512, 0, 0, 1f, 300, 300, 76)]
    [InlineData(512, 512, 0, 0, 1f, 511, 17, 108)]
    [InlineData(512, 512, 0, 0, 1f, 100, 400, 97)]
    [InlineData(64, 32, 10, 20, 4f, 63, 31, 120)]
    [InlineData(64, 32, 10, 20, 4f, 40, 5, 150)]
    [InlineData(32, 64, 10, 20, 4f, 31, 63, 174)]
    [InlineData(32, 64, 10, 20, 4f, 5, 40, 54)]
    public void Perlin_Noise_Is_Raylibs_Pixel_For_Pixel(int width, int height, int offsetX, int offsetY, float scale, int x, int y, byte gray)
    {
        var noise = GenImagePerlinNoise(width, height, offsetX, offsetY, scale);
        noise.Data[(y * width + x) * 4].Should().Be(gray);
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
        var file = _folder.File("two.gif");
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

        var file = _folder.File("save.bin");
        SaveFileData(file, png).Should().BeTrue();
        LoadFileData(file).Should().Equal(png);
        LoadFileData(file + ".missing").Should().BeNull();
    }

    [Fact]
    public void Alpha_Is_Cleared_Masked_Premultiplied_And_Bounded()
    {
        var image = GenImageColor(4, 4, new Color(200, 100, 50, 0));
        ImageDrawRectangle(ref image, 1, 1, 2, 2, new Color(200, 100, 50, 255));
        GetImageAlphaBorder(image, 0.5f).Should().Be(new Rectangle(1, 1, 2, 2));
        GetImageAlphaBorder(GenImageColor(2, 2, Color.Blank), 0.5f).Should().Be(default(Rectangle));

        var cleared = ImageCopy(image);
        ImageAlphaClear(ref cleared, Blue, 0.5f);
        GetImageColor(cleared, 0, 0).Should().Be(Blue);
        GetImageColor(cleared, 1, 1).Should().Be(new Color(200, 100, 50, 255));

        var masked = GenImageColor(2, 1, Red);
        var mask = GenImageColor(2, 1, Color.Black);
        ImageDrawPixel(ref mask, 1, 0, Color.White);
        ImageAlphaMask(ref masked, mask);
        (GetImageColor(masked, 0, 0).A, GetImageColor(masked, 1, 0).A).Should().Be(((byte)0, (byte)255));

        var half = GenImageColor(1, 1, new Color(200, 100, 50, 128));
        ImageAlphaPremultiply(ref half);
        GetImageColor(half, 0, 0).Should().Be(new Color(100, 50, 25, 128));

        var green = ImageFromChannel(GenImageColor(1, 1, new Color(10, 20, 30, 40)), 1);
        GetImageColor(green, 0, 0).Should().Be(new Color(20, 20, 20, 255));
    }

    [Fact]
    public void A_Blur_Spreads_A_Point_And_A_Kernel_Convolves_It()
    {
        var image = GenImageColor(9, 9, Color.Black);
        ImageDrawPixel(ref image, 4, 4, Color.White);
        var blurred = ImageCopy(image);
        ImageBlurGaussian(ref blurred, 2);
        GetImageColor(blurred, 4, 4).R.Should().BeLessThan(255).And.BeGreaterThan(GetImageColor(blurred, 5, 4).R);
        GetImageColor(blurred, 5, 4).R.Should().BeGreaterThan(0, "the point spreads to its neighbors");
        GetImageColor(blurred, 0, 0).R.Should().Be(0, "and not past the blur's reach");

        // A kernel taking each pixel's left neighbor moves the point one to the right.
        var moved = ImageCopy(image);
        ImageKernelConvolution(ref moved, [0, 0, 0, 1, 0, 0, 0, 0, 0]);
        GetImageColor(moved, 5, 4).R.Should().Be(255);
        GetImageColor(moved, 4, 4).R.Should().Be(0);
    }

    [Fact]
    public void A_Turned_Image_Grows_To_Hold_All_Of_It()
    {
        var image = GenImageColor(10, 4, Red);
        ImageRotate(ref image, 90);
        (image.Width, image.Height).Should().Be((4, 10));
        GetImageColor(image, 2, 5).Should().Be(Red);

        var tilted = GenImageColor(10, 10, Red);
        ImageRotate(ref tilted, 45);
        tilted.Width.Should().Be(15, "a square turned by 45 degrees is as wide as its diagonal");
        GetImageColor(tilted, 7, 7).Should().Be(Red);
        GetImageColor(tilted, 0, 0).A.Should().Be(0, "and clear in the corners it does not reach");
    }

    [Fact]
    public void Shapes_Are_Drawn_By_Their_Points()
    {
        var image = GenImageColor(20, 20, Color.Black);
        ImageDrawTriangle(ref image, new(2, 2), new(18, 2), new(2, 18), Red);
        GetImageColor(image, 4, 4).Should().Be(Red);
        GetImageColor(image, 16, 16).Should().Be(Color.Black, "the far corner is outside the triangle");

        var line = GenImageColor(20, 20, Color.Black);
        ImageDrawLineEx(ref line, new(2, 10), new(18, 10), 5, Blue);
        GetImageColor(line, 10, 8).Should().Be(Blue, "a line five wide reaches two pixels either side");
        GetImageColor(line, 10, 4).Should().Be(Color.Black);

        var circle = GenImageColor(20, 20, Color.Black);
        ImageDrawCircleV(ref circle, new(10, 10), 3, Red);
        GetImageColor(circle, 10, 12).Should().Be(Red);

        var square = GenImageGradientSquare(10, 10, 0, Color.White, Color.Black);
        GetImageColor(square, 5, 5).R.Should().BeGreaterThan(200);
        GetImageColor(square, 0, 5).R.Should().BeLessThan(30, "the edge is the outer color");
    }

    [Fact]
    public void A_Dithered_Gradient_Keeps_Its_Average_In_Two_Levels()
    {
        var image = GenImageGradientLinear(64, 8, 90, Color.Black, Color.White);
        var before = LoadImageColors(image).Average(c => (double)c.R);

        ImageDither(ref image, 1, 1, 1, 8);

        LoadImageColors(image).Should().OnlyContain(c => c.R == 0 || c.R == 255, "one bit a channel leaves black and white");
        LoadImageColors(image).Average(c => (double)c.R).Should().BeApproximately(before, 8, "the rounding is spread, not lost");
    }

    [Fact]
    public void Images_Decode_From_Memory_As_From_Files()
    {
        var image = Numbered();
        var png = ExportImageToMemory(image, ".png");

        var back = LoadImageFromMemory(".png", png);

        IsImageValid(back).Should().BeTrue();
        back.Data.Should().Equal(image.Data);
        IsImageValid(LoadImageFromMemory(".png", [1, 2, 3])).Should().BeFalse("bytes that are no image decode to none");
        var gif = LoadImageAnimFromMemory(".gif", TwoFrames, out var frames);
        frames.Should().Be(2);
        GetImageColor(gif, 1, 3).Should().Be(Blue);
    }

    [Fact]
    public void An_Image_Crops_To_Its_Alpha_Grows_To_Powers_Of_Two_And_Lists_Its_Colors()
    {
        var image = GenImageColor(10, 6, Color.Blank);
        ImageDrawRectangle(ref image, 2, 1, 3, 4, Red);
        ImageDrawPixel(ref image, 4, 4, Blue);

        var cropped = image;
        ImageAlphaCrop(ref cropped, 0.5f);
        (cropped.Width, cropped.Height).Should().Be((3, 4));

        var grown = image;
        ImageToPOT(ref grown, Color.White);
        (grown.Width, grown.Height).Should().Be((16, 8));
        GetImageColor(grown, 15, 7).Should().Be(Color.White, "the new pixels take the fill");
        GetImageColor(grown, 2, 1).Should().Be(Red, "the image keeps its place at the top left");

        LoadImagePalette(image, 8).Should().Equal(Color.Blank, Red, Blue);
        LoadImagePalette(image, 2).Should().HaveCount(2);
    }

    [Fact]
    public void Image_Shapes_Blend_Their_Corners_Keep_To_Their_Outlines_And_Turn()
    {
        var image = GenImageColor(20, 20, Color.Black);
        ImageDrawTriangleGradient(ref image, new Vector2(0, 0), new Vector2(0, 20), new Vector2(20, 0), Red, Red, Blue);
        GetImageColor(image, 0, 0).R.Should().BeGreaterThan(200, "near the red corners it is red");
        GetImageColor(image, 18, 0).B.Should().BeGreaterThan(180, "near the blue corner it is blue");

        var outline = GenImageColor(10, 10, Color.Black);
        ImageDrawRectangleLinesEx(ref outline, new Rectangle(0, 0, 10, 10), 2, Red);
        GetImageColor(outline, 1, 5).Should().Be(Red);
        GetImageColor(outline, 5, 5).Should().Be(Color.Black, "the middle is left as it was");

        // A 4 by 2 strip, red on its left half and blue on its right, turned a quarter around its
        // top left, puts the red half above the blue one, to the left of the corner it turned about.
        var strip = GenImageColor(4, 2, Red);
        ImageDrawRectangle(ref strip, 2, 0, 2, 2, Blue);
        var canvas = GenImageColor(10, 10, Color.Black);
        ImageDrawImagePro(ref canvas, strip, new Rectangle(0, 0, 4, 2), new Rectangle(5, 5, 4, 2), Vector2.Zero, 90, Color.White);
        GetImageColor(canvas, 4, 5).Should().Be(Red);
        GetImageColor(canvas, 4, 8).Should().Be(Blue);

        var glow = GenImageColor(21, 21, Color.Black);
        ImageDrawCircleGradient(ref glow, new Vector2(10.5f, 10.5f), 10, Color.White, Color.Black);
        GetImageColor(glow, 10, 10).R.Should().BeGreaterThan(230);
        GetImageColor(glow, 10, 1).R.Should().BeLessThan(40);
    }

    [Fact]
    public void A_Raw_File_Is_Read_After_Its_Header_In_The_Format_Given()
    {
        // Two pixels of R5G6B5 after a header of three bytes: full red, and half green with full blue
        var path = Path.Combine(_folder.Path, "pixels.raw");
        File.WriteAllBytes(path, [9, 9, 9, 0x00, 0xF8, 0x1F, 0x04]);

        var image = LoadImageRaw(path, 2, 1, PixelFormat.UncompressedR5G6B5, 3);

        GetImageColor(image, 0, 0).Should().Be(new Color(255, 0, 0, 255));
        GetImageColor(image, 1, 0).Should().Be(new Color(0, 130, 255, 255), "32 of 63 levels of green, and all 31 of blue");
        LoadImageRaw(path, 4, 1, PixelFormat.UncompressedR5G6B5, 3).IsValid.Should().BeFalse("the file holds two pixels, not four");
        LoadImageRaw(path, 1, 1, PixelFormat.CompressedDxt1Rgb, 0).IsValid.Should().BeFalse("a compressed format is not read");
    }

    [Fact]
    public void An_Image_Keeps_What_A_Format_Keeps_Of_Each_Pixel()
    {
        Image Of(Color c) => GenImageColor(1, 1, c);
        Color Formatted(Color c, PixelFormat format)
        {
            var image = Of(c);
            ImageFormat(ref image, format);
            return GetImageColor(image, 0, 0);
        }
        var color = new Color(200, 100, 50, 128);

        Formatted(color, PixelFormat.UncompressedR8G8B8A8).Should().Be(color, "an image holds that format already");
        Formatted(color, PixelFormat.UncompressedGrayscale).Should().Be(new Color(124, 124, 124, 255), "gray by raylib's weights, and opaque");
        Formatted(color, PixelFormat.UncompressedGrayAlpha).Should().Be(new Color(124, 124, 124, 128));
        Formatted(color, PixelFormat.UncompressedR5G6B5).Should().Be(new Color(197, 101, 49, 255), "24, 25 and 6 levels, read back as the GPU reads them");
        Formatted(color, PixelFormat.UncompressedR4G4B4A4).Should().Be(new Color(204, 102, 51, 136));
        Formatted(color, PixelFormat.UncompressedR5G5B5A1).A.Should().Be(255, "an alpha past 50 of 255 is kept whole");
        Formatted(color, PixelFormat.UncompressedR32).Should().Be(new Color(124, 0, 0, 255), "a format of one channel is read as red");
        Formatted(color, PixelFormat.UncompressedR32G32B32A32).Should().Be(color);
        Formatted(color, PixelFormat.CompressedEtc2Rgb).Should().Be(color, "a compressed format leaves the image as it is");
    }
}

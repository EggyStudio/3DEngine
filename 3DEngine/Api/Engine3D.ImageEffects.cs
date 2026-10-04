using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // -- Images: alpha, filters, turning, and shapes drawn by their points, as raylib's

    /// <summary>Makes a square gradient, <paramref name="inner"/> at the center to <paramref name="outer"/> at the edges, holding the inner color for the first <paramref name="density"/> part.</summary>
    public static Image GenImageGradientSquare(int width, int height, float density, Color inner, Color outer)
    {
        var half = new Vector2(width, height) / 2f;
        return Generate(width, height, (x, y) =>
        {
            // How far toward the nearer edge, the larger of the two axes' shares.
            var t = MathF.Max(MathF.Abs(x + 0.5f - half.X) / half.X, MathF.Abs(y + 0.5f - half.Y) / half.Y);
            return Lerp(inner, outer, Math.Clamp((t - density) / Math.Max(1e-6f, 1 - density), 0, 1));
        });
    }

    /// <summary>A grayscale image of one channel of another, 0 red, 1 green, 2 blue and 3 alpha, opaque.</summary>
    public static Image ImageFromChannel(Image image, int selectedChannel)
    {
        var channel = Math.Clamp(selectedChannel, 0, 3);
        return Generate(image.Width, image.Height, (x, y) =>
        {
            var value = image.Data[(y * image.Width + x) * 4 + channel];
            return new Color(value, value, value);
        });
    }

    /// <summary>Gives every pixel whose alpha is below <paramref name="threshold"/>, from 0 to 1, the color <paramref name="color"/>.</summary>
    public static void ImageAlphaClear(ref Image image, Color color, float threshold)
    {
        var below = threshold * 255;
        EachPixel(image, c => c.A < below ? color : c);
    }

    /// <summary>Sets each pixel's alpha from the brightness of the same pixel of a mask of the same size, white opaque and black clear.</summary>
    public static void ImageAlphaMask(ref Image image, Image alphaMask)
    {
        if (alphaMask.Width != image.Width || alphaMask.Height != image.Height)
        {
            ApiLogger.Warn("ImageAlphaMask: the mask is not the image's size, so the image is left as it is.");
            return;
        }
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            var m = GetImageColor(alphaMask, x, y);
            var c = GetImageColor(image, x, y);
            SetPixel(image, x, y, c with { A = (byte)Math.Round(0.299f * m.R + 0.587f * m.G + 0.114f * m.B) });
        }
    }

    /// <summary>Multiplies each pixel's color by its alpha, as a texture blended by premultiplied alpha needs.</summary>
    public static void ImageAlphaPremultiply(ref Image image) => EachPixel(image, c =>
    {
        byte Times(byte v) => (byte)Math.Round(v * c.A / 255f);
        return new Color(Times(c.R), Times(c.G), Times(c.B), c.A);
    });

    /// <summary>
    /// The smallest rectangle holding every pixel whose alpha is above <paramref name="threshold"/>,
    /// from 0 to 1, which trims a sprite's clear border, or an empty one when no pixel is.
    /// </summary>
    public static Rectangle GetImageAlphaBorder(Image image, float threshold)
    {
        int minX = image.Width, minY = image.Height, maxX = -1, maxY = -1;
        var above = threshold * 255;
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
            if (image.Data[(y * image.Width + x) * 4 + 3] > above)
                (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
        return maxX < 0 ? default : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>Blurs an image by a Gaussian of about <paramref name="blurSize"/> pixels, across and then down.</summary>
    public static void ImageBlurGaussian(ref Image image, int blurSize)
    {
        if (blurSize <= 0 || !image.IsValid) return;
        var sigma = blurSize / 2f;
        var weights = new float[blurSize * 2 + 1];
        for (int i = 0; i < weights.Length; i++) weights[i] = MathF.Exp(-(i - blurSize) * (i - blurSize) / (2 * sigma * sigma));
        var sum = weights.Sum();
        for (int i = 0; i < weights.Length; i++) weights[i] /= sum;

        image = Convolve(image, weights, 1, weights.Length);
        image = Convolve(image, weights, weights.Length, 1);
    }

    /// <summary>
    /// Convolves an image's color with a square kernel, its rows from the top, each pixel the sum of
    /// its neighbors weighted by the kernel, its alpha kept, as an edge or sharpen filter does.
    /// </summary>
    public static void ImageKernelConvolution(ref Image image, float[] kernel)
    {
        var side = (int)MathF.Round(MathF.Sqrt(kernel.Length));
        if (side * side != kernel.Length || side % 2 == 0)
        {
            ApiLogger.Warn($"ImageKernelConvolution: a kernel of {kernel.Length} values is not an odd square, so the image is left as it is.");
            return;
        }
        image = Convolve(image, kernel, side, side);
    }

    /// <summary>Turns an image by <paramref name="degrees"/> clockwise, its canvas grown to hold all of it, clear where it was not, each pixel read between its neighbors.</summary>
    public static void ImageRotate(ref Image image, int degrees)
    {
        if (!image.IsValid || degrees % 360 == 0) return;
        var radians = float.DegreesToRadians(degrees);
        var (sin, cos) = MathF.SinCos(radians);
        // A thousandth off before rounding up, so a quarter turn, whose cosine is not quite zero in
        // floats, keeps the size it should rather than a pixel more.
        var width = (int)MathF.Ceiling(MathF.Abs(image.Width * cos) + MathF.Abs(image.Height * sin) - 1e-3f);
        var height = (int)MathF.Ceiling(MathF.Abs(image.Width * sin) + MathF.Abs(image.Height * cos) - 1e-3f);
        var source = image;
        var from = new Vector2(source.Width, source.Height) / 2f;
        var to = new Vector2(width, height) / 2f;
        image = Generate(width, height, (x, y) =>
        {
            // Each pixel of the turned image read from where turning back puts it in the source.
            var d = new Vector2(x + 0.5f, y + 0.5f) - to;
            var at = from + new Vector2(d.X * cos + d.Y * sin, -d.X * sin + d.Y * cos) - new Vector2(0.5f);
            return Sample(source, at);
        });
    }

    /// <summary>Draws a line <paramref name="thick"/> pixels wide between two points.</summary>
    public static void ImageDrawLineEx(ref Image dst, Vector2 start, Vector2 end, int thick, Color color)
    {
        var along = end - start;
        var length = along.Length();
        if (length < 1e-6f || thick <= 1)
        {
            ImageDrawLineV(ref dst, start, end, color);
            return;
        }
        // The line as the quad around it, two triangles.
        var side = new Vector2(-along.Y, along.X) / length * (thick / 2f);
        ImageDrawTriangle(ref dst, start + side, start - side, end - side, color);
        ImageDrawTriangle(ref dst, start + side, end - side, end + side, color);
    }

    /// <summary>Fills a triangle, each pixel whose center it holds, blended over what is there by the color's alpha.</summary>
    public static void ImageDrawTriangle(ref Image dst, Vector2 v1, Vector2 v2, Vector2 v3, Color color)
    {
        var min = Vector2.Max(Vector2.Min(Vector2.Min(v1, v2), v3), Vector2.Zero);
        var max = Vector2.Min(Vector2.Max(Vector2.Max(v1, v2), v3), new Vector2(dst.Width - 1, dst.Height - 1));
        static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        var area = Edge(v1, v2, v3);
        if (MathF.Abs(area) < 1e-6f) return;
        for (int y = (int)min.Y; y <= (int)max.Y; y++)
        for (int x = (int)min.X; x <= (int)max.X; x++)
        {
            // Inside when the center is on the same side of all three edges as the triangle is turned.
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float a = Edge(v2, v3, p) / area, b = Edge(v3, v1, p) / area, c = Edge(v1, v2, p) / area;
            if (a >= 0 && b >= 0 && c >= 0) SetPixel(dst, x, y, Blend(GetImageColor(dst, x, y), color));
        }
    }

    /// <summary>Fills a circle around a point.</summary>
    public static void ImageDrawCircleV(ref Image dst, Vector2 center, int radius, Color color) =>
        ImageDrawCircle(ref dst, (int)center.X, (int)center.Y, radius, color);

    // A copy of an image with its color convolved by a kernel of width by height, the edges
    // repeated outward, its alpha kept.
    private static Image Convolve(Image image, float[] kernel, int width, int height)
    {
        var source = image;
        int halfW = width / 2, halfH = height / 2;
        return Generate(image.Width, image.Height, (x, y) =>
        {
            float r = 0, g = 0, b = 0;
            for (int ky = 0; ky < height; ky++)
            for (int kx = 0; kx < width; kx++)
            {
                var c = GetImageColor(source, Math.Clamp(x + kx - halfW, 0, source.Width - 1), Math.Clamp(y + ky - halfH, 0, source.Height - 1));
                var k = kernel[ky * width + kx];
                (r, g, b) = (r + c.R * k, g + c.G * k, b + c.B * k);
            }
            static byte Clamp(float v) => (byte)Math.Clamp(MathF.Round(v), 0, 255);
            return new Color(Clamp(r), Clamp(g), Clamp(b), GetImageColor(source, x, y).A);
        });
    }

    // An image read at a point between pixels, blended from the four around it, clear outside it.
    private static Color Sample(Image image, Vector2 at)
    {
        var x0 = (int)MathF.Floor(at.X);
        var y0 = (int)MathF.Floor(at.Y);
        float fx = at.X - x0, fy = at.Y - y0;
        Vector4 Read(int x, int y) => (uint)x < (uint)image.Width && (uint)y < (uint)image.Height ? GetImageColor(image, x, y).ToVector4() : Vector4.Zero;
        var top = Vector4.Lerp(Read(x0, y0), Read(x0 + 1, y0), fx);
        var bottom = Vector4.Lerp(Read(x0, y0 + 1), Read(x0 + 1, y0 + 1), fx);
        var v = Vector4.Lerp(top, bottom, fy) * 255;
        return new Color((byte)MathF.Round(v.X), (byte)MathF.Round(v.Y), (byte)MathF.Round(v.Z), (byte)MathF.Round(v.W));
    }
}

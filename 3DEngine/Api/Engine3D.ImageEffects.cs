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

    /// <summary>
    /// Reduces an image to so many bits for each channel, spreading each pixel's rounding to the
    /// pixels after it (Floyd and Steinberg), so few colors still read as smooth gradients, as
    /// art for an old console's palette is made.
    /// </summary>
    public static void ImageDither(ref Image image, int rBpp, int gBpp, int bBpp, int aBpp)
    {
        if (!image.IsValid) return;
        int[] bits = [Math.Clamp(rBpp, 1, 8), Math.Clamp(gBpp, 1, 8), Math.Clamp(bBpp, 1, 8), Math.Clamp(aBpp, 1, 8)];
        var (width, height, data) = (image.Width, image.Height, image.Data);
        var values = new float[width * height * 4];
        for (int i = 0; i < values.Length; i++) values[i] = data[i];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        for (int c = 0; c < 4; c++)
        {
            var at = (y * width + x) * 4 + c;
            var levels = (1 << bits[c]) - 1;
            var old = Math.Clamp(values[at], 0, 255);
            var rounded = MathF.Round(old / 255 * levels) / levels * 255;
            data[at] = (byte)rounded;
            var error = old - rounded;
            void Spread(int dx, int dy, float share)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < width && ny < height) values[(ny * width + nx) * 4 + c] += error * share;
            }
            Spread(1, 0, 7 / 16f);
            Spread(-1, 1, 3 / 16f);
            Spread(0, 1, 5 / 16f);
            Spread(1, 1, 1 / 16f);
        }
    }

    /// <summary>Draws lines within an image joining each point to the next.</summary>
    public static void ImageDrawLineStrip(ref Image dst, Vector2[] points, Color color)
    {
        for (int i = 1; i < points.Length; i++) ImageDrawLineV(ref dst, points[i - 1], points[i], color);
    }

    /// <summary>Fills a triangle with a color at each corner, blended across it.</summary>
    public static void ImageDrawTriangleGradient(ref Image dst, Vector2 v1, Vector2 v2, Vector2 v3, Color c1, Color c2, Color c3)
    {
        var min = Vector2.Max(Vector2.Min(Vector2.Min(v1, v2), v3), Vector2.Zero);
        var max = Vector2.Min(Vector2.Max(Vector2.Max(v1, v2), v3), new Vector2(dst.Width - 1, dst.Height - 1));
        static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        var area = Edge(v1, v2, v3);
        if (MathF.Abs(area) < 1e-6f) return;
        for (int y = (int)min.Y; y <= (int)max.Y; y++)
        for (int x = (int)min.X; x <= (int)max.X; x++)
        {
            // Each corner's share at the pixel's center, which weighs its color.
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float a = Edge(v2, v3, p) / area, b = Edge(v3, v1, p) / area, c = Edge(v1, v2, p) / area;
            if (a < 0 || b < 0 || c < 0) continue;
            var mixed = c1.ToVector4() * a + c2.ToVector4() * b + c3.ToVector4() * c;
            var color = new Color((byte)MathF.Round(mixed.X * 255), (byte)MathF.Round(mixed.Y * 255), (byte)MathF.Round(mixed.Z * 255), (byte)MathF.Round(mixed.W * 255));
            SetPixel(dst, x, y, Blend(GetImageColor(dst, x, y), color));
        }
    }

    /// <summary>Draws a triangle's outline within an image.</summary>
    public static void ImageDrawTriangleLines(ref Image dst, Vector2 v1, Vector2 v2, Vector2 v3, Color color)
    {
        ImageDrawLineV(ref dst, v1, v2, color);
        ImageDrawLineV(ref dst, v2, v3, color);
        ImageDrawLineV(ref dst, v3, v1, color);
    }

    /// <summary>Fills triangles fanning out from the first point through each pair of the rest.</summary>
    public static void ImageDrawTriangleFan(ref Image dst, Vector2[] points, Color color)
    {
        for (int i = 2; i < points.Length; i++) ImageDrawTriangle(ref dst, points[0], points[i - 1], points[i], color);
    }

    /// <summary>Fills a strip of triangles, each from three points in a row.</summary>
    public static void ImageDrawTriangleStrip(ref Image dst, Vector2[] points, Color color)
    {
        for (int i = 2; i < points.Length; i++) ImageDrawTriangle(ref dst, points[i - 2], points[i - 1], points[i], color);
    }

    /// <summary>Fills a rectangle at a position, of a size.</summary>
    public static void ImageDrawRectangleV(ref Image dst, Vector2 position, Vector2 size, Color color) =>
        ImageDrawRectangle(ref dst, (int)position.X, (int)position.Y, (int)size.X, (int)size.Y, color);

    /// <summary>Fills a rectangle turned <paramref name="rotation"/> degrees around <paramref name="origin"/>, which is relative to its top left.</summary>
    public static void ImageDrawRectanglePro(ref Image dst, Rectangle rec, Vector2 origin, float rotation, Color color)
    {
        var turn = Matrix3x2.CreateRotation(float.DegreesToRadians(rotation));
        var at = new Vector2(rec.X, rec.Y);
        Vector2 Corner(float x, float y) => Vector2.Transform(new Vector2(x, y) - origin, turn) + at;
        Vector2 a = Corner(0, 0), b = Corner(rec.Width, 0), c = Corner(rec.Width, rec.Height), d = Corner(0, rec.Height);
        ImageDrawTriangle(ref dst, a, b, c, color);
        ImageDrawTriangle(ref dst, a, c, d, color);
    }

    /// <summary>Draws a rectangle's outline <paramref name="thick"/> pixels wide, inside its edge.</summary>
    public static void ImageDrawRectangleLinesEx(ref Image dst, Rectangle rec, int thick, Color color)
    {
        int x = (int)rec.X, y = (int)rec.Y, w = (int)rec.Width, h = (int)rec.Height, t = Math.Clamp(thick, 1, Math.Max(1, Math.Min(w, h) / 2));
        ImageDrawRectangle(ref dst, x, y, w, t, color);
        ImageDrawRectangle(ref dst, x, y + h - t, w, t, color);
        ImageDrawRectangle(ref dst, x, y + t, t, h - 2 * t, color);
        ImageDrawRectangle(ref dst, x + w - t, y + t, t, h - 2 * t, color);
    }

    /// <summary>Fills a rectangle with a color at each corner, blended across it, given top left, bottom left, bottom right and top right.</summary>
    public static void ImageDrawRectangleGradientEx(ref Image dst, Rectangle rec, Color topLeft, Color bottomLeft, Color bottomRight, Color topRight)
    {
        Vector2 a = new(rec.X, rec.Y), b = new(rec.X, rec.Y + rec.Height), c = new(rec.X + rec.Width, rec.Y + rec.Height), d = new(rec.X + rec.Width, rec.Y);
        ImageDrawTriangleGradient(ref dst, a, b, c, topLeft, bottomLeft, bottomRight);
        ImageDrawTriangleGradient(ref dst, a, c, d, topLeft, bottomRight, topRight);
    }

    /// <summary>Draws a circle's outline within an image.</summary>
    public static void ImageDrawCircleLinesV(ref Image dst, Vector2 center, int radius, Color color) =>
        ImageDrawCircleLines(ref dst, (int)center.X, (int)center.Y, radius, color);

    /// <summary>Fills a circle blending from <paramref name="inner"/> at its middle to <paramref name="outer"/> at its edge.</summary>
    public static void ImageDrawCircleGradient(ref Image dst, Vector2 center, float radius, Color inner, Color outer)
    {
        if (radius <= 0) return;
        for (int y = Math.Max(0, (int)(center.Y - radius)); y <= Math.Min(dst.Height - 1, (int)(center.Y + radius)); y++)
        for (int x = Math.Max(0, (int)(center.X - radius)); x <= Math.Min(dst.Width - 1, (int)(center.X + radius)); x++)
        {
            var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
            if (distance > radius) continue;
            SetPixel(dst, x, y, Blend(GetImageColor(dst, x, y), Lerp(inner, outer, distance / radius)));
        }
    }

    /// <summary>Draws a whole image within another with its top left at a pixel, multiplied by <paramref name="tint"/>.</summary>
    public static void ImageDrawImage(ref Image dst, Image src, int posX, int posY, Color tint) =>
        ImageDraw(ref dst, src, new Rectangle(0, 0, src.Width, src.Height), new Rectangle(posX, posY, src.Width, src.Height), tint);

    /// <summary>Draws a part of an image within another with its top left at a position.</summary>
    public static void ImageDrawImageRec(ref Image dst, Image src, Rectangle srcRec, Vector2 position, Color tint) =>
        ImageDraw(ref dst, src, srcRec, new Rectangle(position.X, position.Y, MathF.Abs(srcRec.Width), MathF.Abs(srcRec.Height)), tint);

    /// <summary>Draws a whole image within another, scaled and turned <paramref name="rotation"/> degrees around its top left.</summary>
    public static void ImageDrawImageEx(ref Image dst, Image src, Vector2 position, float rotation, float scale, Color tint) =>
        ImageDrawImagePro(ref dst, src, new Rectangle(0, 0, src.Width, src.Height),
            new Rectangle(position.X, position.Y, src.Width * scale, src.Height * scale), Vector2.Zero, rotation, tint);

    /// <summary>
    /// Draws a part of an image into a rectangle of another, turned <paramref name="rotation"/>
    /// degrees around <paramref name="origin"/>, which is relative to the rectangle's top left, each
    /// pixel read between its neighbors and blended over what is there, as <c>DrawTexturePro</c> draws.
    /// </summary>
    public static void ImageDrawImagePro(ref Image dst, Image src, Rectangle srcRec, Rectangle dstRec, Vector2 origin, float rotation, Color tint)
    {
        if (!src.IsValid || dstRec.Width <= 0 || dstRec.Height <= 0 || srcRec.Width == 0 || srcRec.Height == 0) return;
        var turn = Matrix3x2.CreateRotation(float.DegreesToRadians(rotation));
        Matrix3x2.Invert(turn, out var back);
        var at = new Vector2(dstRec.X, dstRec.Y);
        Vector2 Corner(float x, float y) => Vector2.Transform(new Vector2(x, y) - origin, turn) + at;
        Vector2[] corners = [Corner(0, 0), Corner(dstRec.Width, 0), Corner(dstRec.Width, dstRec.Height), Corner(0, dstRec.Height)];
        var min = Vector2.Max(corners.Aggregate(Vector2.Min), Vector2.Zero);
        var max = Vector2.Min(corners.Aggregate(Vector2.Max), new Vector2(dst.Width - 1, dst.Height - 1));
        var source = src;
        for (int y = (int)min.Y; y <= (int)max.Y; y++)
        for (int x = (int)min.X; x <= (int)max.X; x++)
        {
            // Where the pixel's center lands in the rectangle before it was turned, and so in the source.
            var local = Vector2.Transform(new Vector2(x + 0.5f, y + 0.5f) - at, back) + origin;
            if (local.X < 0 || local.Y < 0 || local.X >= dstRec.Width || local.Y >= dstRec.Height) continue;
            var u = srcRec.X + local.X / dstRec.Width * srcRec.Width - 0.5f;
            var v = srcRec.Y + local.Y / dstRec.Height * srcRec.Height - 0.5f;
            var over = Multiply(Sample(source, new Vector2(u, v)), tint);
            SetPixel(dst, x, y, Blend(GetImageColor(dst, x, y), over));
        }
    }

    /// <summary>
    /// Draws text within an image, turned <paramref name="rotation"/> degrees around
    /// <paramref name="origin"/>, which is relative to the text's top left.
    /// </summary>
    public static void ImageDrawTextPro(ref Image dst, Font font, string text, Vector2 position, Vector2 origin, float rotation,
        float fontSize, float spacing, Color tint)
    {
        var lettering = ImageTextEx(font, text, fontSize, spacing, tint);
        if (!lettering.IsValid) return;
        ImageDrawImagePro(ref dst, lettering, new Rectangle(0, 0, lettering.Width, lettering.Height),
            new Rectangle(position.X, position.Y, lettering.Width, lettering.Height), origin, rotation, Color.White);
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

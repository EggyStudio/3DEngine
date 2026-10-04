namespace Engine;

/// <summary>
/// Signed distance fields from coverage, for fonts drawn sharp at any size. A shape is taken to be
/// the pixels half covered or more, and each pixel gets its distance from the shape's edge.
/// </summary>
/// <remarks>
/// The distances are exact Euclidean ones, from the separable transform of Felzenszwalb and
/// Huttenlocher, which runs in time linear in the pixels, so a large atlas costs a frame or two
/// rather than the seconds a search around every pixel would.
/// </remarks>
internal static class DistanceField
{
    /// <summary>
    /// The distance in pixels from each pixel's center to the edge of the shape where
    /// <paramref name="coverage"/> is 128 or more, positive inside and negative outside.
    /// </summary>
    public static float[] Signed(ReadOnlySpan<byte> coverage, int width, int height)
    {
        var count = width * height;
        var toInside = new float[count];
        var toOutside = new float[count];
        for (int i = 0; i < count; i++)
        {
            var inside = coverage[i] >= 128;
            toInside[i] = inside ? 0 : float.PositiveInfinity;
            toOutside[i] = inside ? float.PositiveInfinity : 0;
        }
        SquaredDistances(toInside, width, height);
        SquaredDistances(toOutside, width, height);

        // A pixel beside the edge is a whole pixel from the nearest one across it, and the edge
        // lies halfway between their centers.
        var signed = new float[count];
        for (int i = 0; i < count; i++)
            signed[i] = coverage[i] >= 128 ? MathF.Sqrt(toOutside[i]) - 0.5f : 0.5f - MathF.Sqrt(toInside[i]);
        return signed;
    }

    /// <summary>
    /// Reads a distance field at the center of each <paramref name="factor"/> by
    /// <paramref name="factor"/> block into one pixel, dividing the distances by the factor so
    /// they stay in the smaller image's pixels. Blocks past the edge read the last row or column.
    /// </summary>
    /// <remarks>
    /// The center is read rather than the block averaged, because across a stroke thinner than a
    /// block the distances outside it would pull the average below the edge, and the stroke would
    /// break where it falls between two pixels.
    /// </remarks>
    public static float[] Shrink(float[] field, int width, int height, int factor, out int smallWidth, out int smallHeight)
    {
        smallWidth = (width + factor - 1) / factor;
        smallHeight = (height + factor - 1) / factor;
        var small = new float[smallWidth * smallHeight];
        // The pixels around the block's center, two across for an even factor and one for an odd.
        var (first, span) = factor % 2 == 0 ? (factor / 2 - 1, 2) : (factor / 2, 1);
        var scale = 1f / (span * span * factor);
        for (int y = 0; y < smallHeight; y++)
        for (int x = 0; x < smallWidth; x++)
        {
            float sum = 0;
            for (int dy = 0; dy < span; dy++)
            {
                var row = Math.Min(y * factor + first + dy, height - 1) * width;
                for (int dx = 0; dx < span; dx++) sum += field[row + Math.Min(x * factor + first + dx, width - 1)];
            }
            small[y * smallWidth + x] = sum * scale;
        }
        return small;
    }

    // Turns zeros and infinities into each pixel's squared distance to the nearest zero, down the
    // columns and then along the rows.
    private static void SquaredDistances(float[] grid, int width, int height)
    {
        var length = Math.Max(width, height);
        var line = new float[length];
        var result = new float[length];
        var parabolas = new int[length];
        var bounds = new float[length + 1];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++) line[y] = grid[y * width + x];
            Transform(line, height, result, parabolas, bounds);
            for (int y = 0; y < height; y++) grid[y * width + x] = result[y];
        }
        for (int y = 0; y < height; y++)
        {
            Array.Copy(grid, y * width, line, 0, width);
            Transform(line, width, result, parabolas, bounds);
            Array.Copy(result, 0, grid, y * width, width);
        }
    }

    // The one-dimensional transform: the lower envelope of a parabola rooted at each sample, read
    // back at every sample. Samples at infinity root no parabola.
    private static void Transform(float[] f, int n, float[] d, int[] v, float[] z)
    {
        var k = -1;
        for (int q = 0; q < n; q++)
        {
            if (float.IsPositiveInfinity(f[q])) continue;
            float s = float.NegativeInfinity;
            while (k >= 0)
            {
                var p = v[k];
                s = (f[q] + q * q - (f[p] + p * p)) / (2f * (q - p));
                if (s > z[k]) break;
                k--;
            }
            k++;
            v[k] = q;
            z[k] = k == 0 ? float.NegativeInfinity : s;
            z[k + 1] = float.PositiveInfinity;
        }

        if (k < 0)
        {
            Array.Fill(d, float.PositiveInfinity, 0, n);
            return;
        }
        var j = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[j + 1] < q) j++;
            var p = v[j];
            d[q] = (q - p) * (float)(q - p) + f[p];
        }
    }
}

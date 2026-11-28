using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Engine;

/// <summary>
/// What each pixel's gather from the split cascades was read by, as <c>gi.split_cause</c> writes
/// it, the module's <c>splitGatherCause</c> at each pixel's point (<c>gi_split.slang</c>), two PFMs
/// with their rows from the bottom as <c>gi.compare</c> writes a frame, and how many pixels each
/// cause holds.
/// </summary>
internal static class SplitCauses
{
    /// <summary>
    /// The frames since a pixel's youngest probe was taken under which it is counted as holding a
    /// fresh probe, the frames the store's age bins count one by one.
    /// </summary>
    internal const int FreshFrames = 16;

    /// <summary>
    /// Writes <paramref name="causes"/>, four floats a pixel with rows from the top, into
    /// <paramref name="path"/>-weights.pfm, the weights by the share and by the trilinear weights
    /// alone and the share lent from behind the plane, and <paramref name="path"/>-age.pfm, the
    /// frames since the youngest probe was taken, and says how many of the pixels seen meet each
    /// cause, each pixel counted by the first it meets.
    /// </summary>
    internal static string Write(string path, float[] causes, int width, int height)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var weights = new float[3 * width * height];
        var ages = new float[width * height];
        for (int i = 0; i < width * height; i++)
        {
            (weights[3 * i], weights[3 * i + 1], weights[3 * i + 2]) = (causes[4 * i], causes[4 * i + 1], causes[4 * i + 2]);
            ages[i] = causes[4 * i + 3];
        }
        WritePfm(full + "-weights.pfm", "PF", weights, 3, width, height);
        WritePfm(full + "-age.pfm", "Pf", ages, 1, width, height);
        var counts = Count(causes, width * height);
        return string.Create(CultureInfo.InvariantCulture,
            $"{counts.Seen} pixels seen, {counts.None} gathering nothing, {counts.Plain} taking their probes by their trilinear weights alone, "
            + $"{counts.Fresh} holding a probe younger than {FreshFrames} frames and {counts.Behind} lent half or more from behind their plane, "
            + $"into {full}-weights.pfm and -age.pfm");
    }

    /// <summary>
    /// How many of the pixels seen meet each cause, each counted by the first it meets of no weight
    /// either way, no weight by the share alone, a probe younger than <see cref="FreshFrames"/>
    /// and half or more of its weight lent from behind its plane, in that order.
    /// </summary>
    internal static (int Seen, int None, int Plain, int Fresh, int Behind) Count(float[] causes, int pixels)
    {
        var (seen, none, plain, fresh, behind) = (0, 0, 0, 0, 0);
        for (int i = 0; i < pixels; i++)
        {
            var (x, y, z, w) = (causes[4 * i], causes[4 * i + 1], causes[4 * i + 2], causes[4 * i + 3]);
            if (x < 0)
                continue;
            seen++;
            if (y <= 0) none++;
            else if (x <= 0) plain++;
            else if (w < FreshFrames) fresh++;
            else if (z >= 0.5f) behind++;
        }
        return (seen, none, plain, fresh, behind);
    }

    private static void WritePfm(string path, string kind, float[] values, int channels, int width, int height)
    {
        using var file = File.Create(path);
        file.Write(Encoding.ASCII.GetBytes($"{kind}\n{width} {height}\n-1.0\n"));
        for (int y = height - 1; y >= 0; y--)
            file.Write(MemoryMarshal.AsBytes(values.AsSpan(y * width * channels, width * channels)));
    }
}

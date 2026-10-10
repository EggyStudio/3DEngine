using System.Globalization;
using System.Numerics;
using System.Text;
using static Engine.Engine3D;

namespace Engine.Game;

/// <summary>
/// A recording of how much the picture changes from each frame to the next, before, during and after
/// the player turns or steps over some frames, to measure how long the light that bounces takes to
/// settle after the camera moves and how much it flickers meanwhile.
/// </summary>
/// <remarks>
/// Each frame's change is the mean over the window of each pixel's difference in red, green and blue
/// from the frame before, in sRGB levels from 0 to 255, and the share of pixels whose difference in
/// any of them passes 8 levels, read through <c>LoadImageFromScreen</c>, which waits for the GPU each
/// frame, so the frames come slower while it runs. Nothing else moves, so before the motion the
/// change is the bounce's own noise, during it the motion, and after it what is left is the bounce
/// settling.
/// </remarks>
public sealed class FlickerRun(int frames, int motionAt, int over, float turn, float step, string path)
{
    private readonly List<(double Mean, double Share)> _changes = [];
    private Image? _last, _stopped;
    private int _frame;

    /// <summary>Reads a frame and moves the player on the motion's frame.</summary>
    /// <returns>False once every frame is read and the recording is written to its file.</returns>
    public bool Step(VoxelGame game)
    {
        if (!Measurements.Keeping())
            return true;
        var image = LoadImageFromScreen();
        if (_last is { } last) _changes.Add((Measurements.MeanChange(last, image), Measurements.ShareChanged(last, image, 8)));
        _last = image;
        // The screen is read a frame late, so the first frame drawn after the motion ends is read two
        // frames after its last part.
        if (_frame == motionAt + over + 2) _stopped = image;
        if (_frame >= motionAt && _frame < motionAt + over)
        {
            // The motion in equal parts over its frames, the step along the way the player faced at its start.
            var player = game.Player;
            var ahead = new Vector3(-MathF.Sin(player.Yaw), 0, -MathF.Cos(player.Yaw));
            player.Heading += turn / over;
            player.Teleport(player.Body.Position + ahead * (step / over));
        }
        if (++_frame <= frames) return true;

        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"# the change of each frame from the one before, the mean in sRGB levels and the share of pixels past 8 levels, the player turning {turn} degrees and stepping {step} blocks over frames {motionAt} to {motionAt + over}");
        text.AppendLine("frame\tchange\tshare");
        for (int i = 0; i < _changes.Count; i++) text.AppendLine(CultureInfo.InvariantCulture, $"{i + 1}\t{_changes[i].Mean:0.0000}\t{_changes[i].Share:0.00000}");
        File.WriteAllText(path, text.ToString());

        // The first frame after the motion, the last, and their difference sixteen times over, which
        // shows where the picture went on changing.
        if (_stopped is { } stopped)
        {
            var stem = Path.ChangeExtension(path, null);
            ExportImage(stopped, stem + "-stopped.png");
            ExportImage(image, stem + "-settled.png");
            var difference = new byte[image.Data.Length];
            for (int i = 0; i < difference.Length; i += 4)
            {
                for (int c = 0; c < 3; c++) difference[i + c] = (byte)Math.Min(255, 16 * Math.Abs(stopped.Data[i + c] - image.Data[i + c]));
                difference[i + 3] = 255;
            }
            ExportImage(new Image(difference, image.Width, image.Height), stem + "-difference.png");
        }
        return false;
    }
}

/// <summary>Readings of the window's picture the game's measuring commands share.</summary>
public static class Measurements
{
    private static bool _keeping;

    /// <summary>
    /// Whether the engine keeps a copy of each frame, which it does from the first time the screen
    /// is read, so that first read is the clear color and is thrown away here.
    /// </summary>
    /// <returns>False on the first call, whose frame is not kept yet, and true after it.</returns>
    public static bool Keeping()
    {
        if (_keeping) return true;
        LoadImageFromScreen();
        _keeping = true;
        return false;
    }

    /// <summary>The mean over every pixel of the difference in red, green and blue between two pictures of one size, in sRGB levels.</summary>
    public static double MeanChange(Image a, Image b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return double.NaN;
        long sum = 0;
        var (x, y) = (a.Data, b.Data);
        for (int i = 0; i < x.Length; i += 4)
            sum += Math.Abs(x[i] - y[i]) + Math.Abs(x[i + 1] - y[i + 1]) + Math.Abs(x[i + 2] - y[i + 2]);
        return sum / (3.0 * a.Width * a.Height);
    }

    /// <summary>The share of pixels whose red, green or blue differs between two pictures of one size by more than <paramref name="levels"/>.</summary>
    public static double ShareChanged(Image a, Image b, int levels)
    {
        if (a.Width != b.Width || a.Height != b.Height) return double.NaN;
        long changed = 0;
        var (x, y) = (a.Data, b.Data);
        for (int i = 0; i < x.Length; i += 4)
            if (Math.Abs(x[i] - y[i]) > levels || Math.Abs(x[i + 1] - y[i + 1]) > levels || Math.Abs(x[i + 2] - y[i + 2]) > levels) changed++;
        return (double)changed / (a.Width * a.Height);
    }

    /// <summary>The mean luminance in sRGB levels of the pixels within <paramref name="radius"/> of a pixel, or NaN off the picture.</summary>
    public static double Luminance(Image image, int x, int y, int radius)
    {
        double sum = 0;
        var count = 0;
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int px = x + dx, py = y + dy;
                if ((uint)px >= image.Width || (uint)py >= image.Height) continue;
                var at = (py * image.Width + px) * 4;
                sum += 0.2126 * image.Data[at] + 0.7152 * image.Data[at + 1] + 0.0722 * image.Data[at + 2];
                count++;
            }
        return count == 0 ? double.NaN : sum / count;
    }

    /// <summary>
    /// The picture's luminance at points around a circle on a horizontal plane, by angle from +x
    /// toward +z, and how far it swings: its mean, its lowest and highest, the swing over the mean,
    /// and how many peaks stand above their neighbors.
    /// </summary>
    public static string Ring(Image image, Camera3D camera, Vector3 middle, float radius, int samples)
    {
        var values = new double[samples];
        for (int i = 0; i < samples; i++)
        {
            var angle = 2 * MathF.PI * i / samples;
            var at = GetWorldToScreen(middle + new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * radius, camera);
            values[i] = Luminance(image, (int)MathF.Round(at.X), (int)MathF.Round(at.Y), 2);
        }
        var seen = values.Where(v => !double.IsNaN(v)).ToArray();
        if (seen.Length == 0) return "no point of the circle is in the picture";
        double mean = seen.Average(), low = seen.Min(), high = seen.Max();
        // A peak is higher than the two samples on either side of it and than the mean.
        var peaks = 0;
        for (int i = 0; i < samples; i++)
        {
            var v = values[i];
            if (double.IsNaN(v) || v <= mean) continue;
            var higher = true;
            for (int d = 1; d <= 2 && higher; d++)
                higher = v > values[(i + d) % samples] && v > values[(i - d + samples) % samples];
            if (higher) peaks++;
        }
        var list = string.Join(" ", values.Select(v => v.ToString("0.0", CultureInfo.InvariantCulture)));
        return string.Create(CultureInfo.InvariantCulture,
            $"radius {radius}: mean {mean:0.00}, low {low:0.00}, high {high:0.00}, swing {(high - low) / mean:0.000} of the mean, {peaks} peaks; by angle {list}");
    }
}

using System.Globalization;

namespace Engine;

/// <summary>
/// The command that reads a rectangle of the frame drawn, so a script asserts a picture without
/// downloading it, its mean color and the largest step between neighboring pixels along its middle
/// row and its middle column.
/// </summary>
/// <remarks>
/// The frame is the next presented, through the capture a screenshot takes, and the colors are the
/// sRGB numbers the window shows, so a sky cleared to 81, 137 and 218 is read back as those. The
/// step is a channel's largest difference between two pixels side by side, so an edge between two
/// colors reads as the larger of their channels' differences and a smooth gradient as small steps.
/// </remarks>
internal static class FrameReadCommands
{
    [Command("frame.read", "Reads a rectangle of the next frame drawn, answering its mean sRGB color, the largest step between neighboring pixels along its middle row and column, where it is, and the frame's number: frame.read [<x> <y> <width> <height>]")]
    internal static string Read(string line)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var numbers = new int[words.Length];
        if (words.Length is not (0 or 4) || words.Where((word, i) => !int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i])).Any())
            return Refuse("BAD_ARGUMENT", "frame.read takes a rectangle in window pixels from the top left, x, y, width and height, or nothing for the whole frame.");
        if (words.Length == 4 && (numbers[0] < 0 || numbers[1] < 0 || numbers[2] < 1 || numbers[3] < 1))
            return Refuse("BAD_ARGUMENT", "frame.read takes a rectangle from the top left of the frame, a pixel or more wide and high.");

        // The frame drawn after this command is the one the time counted at the top of it, as shot
        // counts it.
        var frame = ConsoleHost.Time.FrameCount;
        string? answer = null, failure = null;
        var read = false;
        if (Screenshots.Capture(ConsoleHost.World!, (rgba, width, height) =>
            {
                var (x, y, w, h) = words.Length == 0 ? (0, 0, width, height) : (numbers[0], numbers[1], numbers[2], numbers[3]);
                if (x + w > width || y + h > height) failure = $"The rectangle {x} {y} {w} {h} reaches past the frame, which is {width} by {height}.";
                else answer = Say(Measure(rgba, width, x, y, w, h), frame);
                read = true;
            }) is { } refusal)
            return Refuse("NO_RENDERER", refusal);

        ConsoleHost.Later(() =>
        {
            if (!read) return null;
            if (failure is not null) ConsoleHost.Fail("BAD_ARGUMENT", failure);
            return failure ?? answer;
        });
        return "reading the next frame";
    }

    /// <summary>What a rectangle of a frame holds, its mean color and its largest step with the pixel the step starts at.</summary>
    internal readonly record struct Reading(byte Red, byte Green, byte Blue, int Step, int StepX, int StepY);

    /// <summary>
    /// The mean color of a rectangle of RGBA pixels, rows from the top, each channel rounded, and the
    /// largest difference of a channel between two pixels side by side along its middle row and down
    /// its middle column, with the first of the two, the leftmost or the highest of those found first.
    /// </summary>
    internal static Reading Measure(byte[] rgba, int width, int x, int y, int w, int h)
    {
        long red = 0, green = 0, blue = 0;
        for (int row = y; row < y + h; row++)
            for (int column = x; column < x + w; column++)
            {
                var at = (row * width + column) * 4;
                (red, green, blue) = (red + rgba[at], green + rgba[at + 1], blue + rgba[at + 2]);
            }
        var count = (double)w * h;
        static byte Mean(long sum, double count) => (byte)Math.Round(sum / count, MidpointRounding.AwayFromZero);

        var (step, stepX, stepY) = (0, x, y);
        void Compare(int ax, int ay, int bx, int by)
        {
            var (a, b) = ((ay * width + ax) * 4, (by * width + bx) * 4);
            var difference = Math.Max(Math.Abs(rgba[a] - rgba[b]), Math.Max(Math.Abs(rgba[a + 1] - rgba[b + 1]), Math.Abs(rgba[a + 2] - rgba[b + 2])));
            if (difference > step) (step, stepX, stepY) = (difference, ax, ay);
        }
        var (middleRow, middleColumn) = (y + h / 2, x + w / 2);
        for (int column = x; column + 1 < x + w; column++) Compare(column, middleRow, column + 1, middleRow);
        for (int row = y; row + 1 < y + h; row++) Compare(middleColumn, row, middleColumn, row + 1);

        return new Reading(Mean(red, count), Mean(green, count), Mean(blue, count), step, stepX, stepY);
    }

    /// <summary>A reading as the command answers it, a line of names and numbers a script splits.</summary>
    internal static string Say(Reading reading, ulong frame) =>
        $"mean {reading.Red} {reading.Green} {reading.Blue} step {reading.Step} at {reading.StepX} {reading.StepY} frame {frame}";

    private static string Refuse(string code, string message)
    {
        ConsoleHost.Fail(code, message);
        return message;
    }
}

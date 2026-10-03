using System.Text;

namespace Engine;

/// <summary>One line of the console log.</summary>
/// <param name="Index">How many lines were written before it.</param>
/// <param name="Frame">The frame it was written in, or last repeated in.</param>
/// <param name="Level">Its level.</param>
/// <param name="Text">What it says, with its category.</param>
/// <param name="Count">How many times in a row it was written.</param>
public readonly record struct LogLine(int Index, ulong Frame, LogLevel Level, string Text, int Count = 1);

/// <summary>
/// The last <see cref="Depth"/> lines the engine logged at <see cref="LogLevel.Info"/> or above,
/// and everything the program wrote to the console while the CLI serves, for <c>log.tail</c>.
/// </summary>
/// <remarks>
/// A line that repeats the one before it is counted rather than added, because what repeats is a
/// warning in something that runs every frame, and sixty copies a second would push out every
/// other line.
/// </remarks>
public static class ConsoleLog
{
    /// <summary>How many lines are kept.</summary>
    public const int Depth = 2000;

    private static readonly object Gate = new();
    private static readonly List<LogLine> Lines = [];
    private static bool _teeing;

    /// <summary>How many lines have been written since the process started.</summary>
    public static int Written { get; private set; }

    /// <summary>The frame lines are stamped with, which the CLI keeps current.</summary>
    public static ulong Frame { get; set; }

    /// <summary>Writes a line.</summary>
    public static void Write(LogLevel level, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        lock (Gate)
        {
            if (Lines.Count > 0 && Lines[^1] is { } last && last.Level == level && last.Text == text)
            {
                Lines[^1] = last with { Count = last.Count + 1, Frame = Frame };
                Written++;
                return;
            }

            Lines.Add(new LogLine(Written, Frame, level, text));
            Written++;
            if (Lines.Count > Depth) Lines.RemoveRange(0, Lines.Count - Depth);
        }
    }

    /// <summary>Every line kept, oldest first.</summary>
    public static LogLine[] All()
    {
        lock (Gate) return [.. Lines];
    }

    /// <summary>Forgets every line.</summary>
    public static void Clear()
    {
        lock (Gate) Lines.Clear();
    }

    /// <summary>
    /// Copies what the program writes to the console into the log as well. The engine's own lines
    /// arrive through the logger, so a teed line from the logger is skipped by its prefix.
    /// </summary>
    public static void TeeConsole()
    {
        if (_teeing) return;
        _teeing = true;
        Console.SetOut(new Tee(Console.Out, LogLevel.Info));
        Console.SetError(new Tee(Console.Error, LogLevel.Error));
    }

    private sealed class Tee(TextWriter inner, LogLevel level) : TextWriter
    {
        private readonly StringBuilder _pending = new();

        public override Encoding Encoding => inner.Encoding;

        public override void Write(char value)
        {
            inner.Write(value);
            if (value == '\r') return;
            if (value != '\n')
            {
                _pending.Append(value);
                return;
            }

            var line = _pending.ToString();
            _pending.Clear();
            // The engine's logger writes "[   1.2345s] [INFO ] [Category] ...", and those lines
            // are in the log already.
            if (!line.StartsWith("[ ", StringComparison.Ordinal) || !line.Contains("s] [", StringComparison.Ordinal))
                ConsoleLog.Write(level, line);
        }

        public override void Write(string? value)
        {
            if (value is null) return;
            foreach (var character in value) Write(character);
        }

        public override void WriteLine(string? value)
        {
            Write(value);
            Write('\n');
        }

        public override void Flush() => inner.Flush();
    }
}

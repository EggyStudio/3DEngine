namespace Engine;

/// <summary>Writes log messages to the console with elapsed time, level, and category. Flushes after every write for crash safety.</summary>
/// <seealso cref="FileLoggerProvider"/>
/// <seealso cref="LogConfig"/>
internal sealed class ConsoleLoggerProvider : ILoggerProvider
{
    /// <summary>Singleton instance of the console log provider.</summary>
    public static ConsoleLoggerProvider Instance { get; } = new();
    private ConsoleLoggerProvider() { }

    /// <inheritdoc />
    public void Log(LogLevel level, string category, string message, Exception? exception = null)
    {
        Console.WriteLine(Line(LogConfig.EngineTimer.Elapsed.TotalSeconds, level, category, message));
        if (exception != null)
            Console.WriteLine(exception);
        Console.Out.Flush();
    }

    /// <summary>A line as the console and the log file write it, the seconds since the engine started, the level, the category and the message.</summary>
    internal static string Line(double elapsed, LogLevel level, string category, string message)
    {
        var levelTag = level switch
        {
            LogLevel.Trace    => "TRACE",
            LogLevel.Debug    => "DEBUG",
            LogLevel.Info     => "INFO ",
            LogLevel.Warning  => "WARN ",
            LogLevel.Error    => "ERROR",
            LogLevel.Fatal    => "FATAL",
            _ => level.ToString().ToUpperInvariant()
        };
        return $"[{elapsed,10:F4}s] [{levelTag}] [{category}] {message}";
    }
}

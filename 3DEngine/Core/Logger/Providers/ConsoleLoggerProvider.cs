namespace Engine;

/// <summary>Writes log messages to the console with elapsed time, level, and category. Flushes after every write for crash safety.</summary>
/// <seealso cref="FileLoggerProvider"/>
/// <seealso cref="LogConfig"/>
public sealed class ConsoleLoggerProvider : ILoggerProvider
{
    /// <summary>Singleton instance of the console log provider.</summary>
    public static ConsoleLoggerProvider Instance { get; } = new();
    private ConsoleLoggerProvider() { }

    /// <inheritdoc />
    public void Log(LogLevel level, string category, string message, Exception? exception = null)
    {
        double elapsed = LogConfig.EngineTimer.Elapsed.TotalSeconds;
        var levelTag = level switch
        {
            LogLevel.Trace    => "TRACE",
            LogLevel.Debug    => "DEBUG",
            LogLevel.Info     => "INFO ",
            LogLevel.Warning  => "WARN ",
            LogLevel.Error    => "ERROR",
            LogLevel.Critical => "FATAL",
            _ => level.ToString().ToUpperInvariant()
        };
        Console.WriteLine($"[{elapsed,10:F4}s] [{levelTag}] [{category}] {message}");
        if (exception != null)
            Console.WriteLine(exception);
        Console.Out.Flush();
    }
}

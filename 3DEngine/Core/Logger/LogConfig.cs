using System.Diagnostics;

namespace Engine;

/// <summary>Global log configuration.</summary>
internal static class LogConfig
{
    /// <summary>Minimum severity written to the log file and any extra providers. Defaults to Trace, so every startup diagnostic reaches the file.</summary>
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    /// <summary>Minimum severity written to the console. Defaults to Info, which keeps the console readable while the log file has the full detail.</summary>
    public static LogLevel ConsoleMinimumLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Told each line that reaches the console's level, with its category in brackets before it, as
    /// raylib's trace log callback is, or null for none.
    /// </summary>
    public static Action<LogLevel, string>? Callback { get; set; }

    /// <summary>
    /// When true, per-frame repetitive diagnostics (stage timing, render steps) are emitted at Trace level.
    /// When false (default), only the logs of starting and stopping are shown, which keeps a running app quiet.
    /// Enable via <c>LogConfig.PerFrameLogging = true</c> or the <c>ENGINE_LOG_FRAMES=1</c> environment variable.
    /// </summary>
    public static bool PerFrameLogging { get; set; }
        = Environment.GetEnvironmentVariable("ENGINE_LOG_FRAMES") == "1";

    /// <summary>Maximum log file size in bytes. When exceeded the file logger writes a truncation notice and stops. Defaults to 50 MB.</summary>
    public static long MaxLogFileBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>Engine-wide stopwatch started at process launch for elapsed timestamps.</summary>
    internal static readonly Stopwatch EngineTimer = Stopwatch.StartNew();

    /// <summary>
    /// Directory where engine log files (Engine.log, Crash.log, ...) are written.
    /// Defaults to a <c>logs/</c> subfolder next to the executable. The directory is created
    /// on first access so callers can pass the returned path straight to a file writer.
    /// </summary>
    public static string LogsDirectory
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Convenience: returns <see cref="LogsDirectory"/>/<paramref name="fileName"/>, ensuring the directory exists.</summary>
    public static string GetLogFilePath(string fileName) => Path.Combine(LogsDirectory, fileName);
}

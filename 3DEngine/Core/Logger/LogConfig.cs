using System.Diagnostics;

namespace Engine;

/// <summary>Global log configuration.</summary>
public static class LogConfig
{
    /// <summary>Minimum severity written to the log file and any extra providers. Defaults to Trace - captures all startup diagnostics to disk.</summary>
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    /// <summary>Minimum severity written to the console. Defaults to Info - keeps the console readable while the log file gets the full detail.</summary>
    public static LogLevel ConsoleMinimumLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// When true, per-frame repetitive diagnostics (stage timing, render steps) are emitted at Trace level.
    /// When false (default), only one-time startup/lifecycle logs are shown - keeping output clean at runtime.
    /// Enable via <c>LogConfig.PerFrameLogging = true</c> or the <c>ENGINE_LOG_FRAMES=1</c> environment variable.
    /// </summary>
    public static bool PerFrameLogging { get; set; }
        = Environment.GetEnvironmentVariable("ENGINE_LOG_FRAMES") == "1";

    /// <summary>Maximum log file size in bytes. When exceeded the file logger writes a truncation notice and stops. Defaults to 50 MB.</summary>
    public static long MaxLogFileBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>Engine-wide stopwatch started at process launch for elapsed timestamps.</summary>
    internal static readonly Stopwatch EngineTimer = Stopwatch.StartNew();

    /// <summary>
    /// Directory where engine log files (Engine.log, Crash.log, ultralight.log, ...) are written.
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

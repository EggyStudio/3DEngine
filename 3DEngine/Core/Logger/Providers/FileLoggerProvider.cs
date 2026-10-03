using System.Text;

namespace Engine;

/// <summary>
/// Writes log messages to a file with auto-flush for crash safety. Initialized lazily on first use.
/// Truncates output when the file exceeds <see cref="LogConfig.MaxLogFileBytes"/>.
/// </summary>
/// <seealso cref="ConsoleLoggerProvider"/>
/// <seealso cref="LogConfig"/>
public sealed class FileLoggerProvider : ILoggerProvider, IDisposable
{
    private static FileLoggerProvider? _instance;

    /// <summary>The active file logger instance, or <c>null</c> if not yet initialized.</summary>
    public static FileLoggerProvider? Instance => _instance;

    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private long _bytesWritten;
    private bool _truncated;

    private FileLoggerProvider(StreamWriter writer) => _writer = writer;

    /// <summary>
    /// Initializes the file logger writing to the specified path.
    /// Safe to call multiple times; subsequent calls are ignored.
    /// </summary>
    /// <param name="logFilePath">The absolute path of the log file to create or overwrite.</param>
    public static void Initialize(string logFilePath)
    {
        if (_instance != null) return;
        try
        {
            var dir = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var stream = new FileStream(logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            _instance = new FileLoggerProvider(writer);
        }
        catch
        {
            // Silently degrade - file logging is best-effort.
        }
    }

    /// <inheritdoc />
    public void Log(LogLevel level, string category, string message, Exception? exception = null)
    {
        lock (_lock)
        {
            if (_truncated) return;

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

            var line = $"[{elapsed,10:F4}s] [{levelTag}] [{category}] {message}";
            _writer.WriteLine(line);
            _bytesWritten += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;

            if (exception != null)
            {
                var exStr = exception.ToString();
                _writer.WriteLine(exStr);
                _bytesWritten += Encoding.UTF8.GetByteCount(exStr) + Environment.NewLine.Length;
            }

            if (_bytesWritten >= LogConfig.MaxLogFileBytes)
            {
                _writer.WriteLine("--- LOG TRUNCATED (size limit reached) ---");
                _truncated = true;
            }
        }
    }

    /// <summary>Flushes and closes the underlying file stream.</summary>
    public void Dispose()
    {
        lock (_lock) { _writer.Dispose(); }
    }
}

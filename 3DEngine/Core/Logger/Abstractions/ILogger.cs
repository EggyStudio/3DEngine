namespace Engine;

/// <summary>Category-scoped logging interface providing convenience methods for each severity level.</summary>
public interface ILogger
{
    /// <summary>Writes a log entry at the specified severity level.</summary>
    /// <param name="level">The severity of the message.</param>
    /// <param name="message">The log message text.</param>
    /// <param name="exception">Optional exception associated with the message.</param>
    void Log(LogLevel level, string message, Exception? exception = null);

    /// <summary>Writes a trace-level message for detailed diagnostic information.</summary>
    /// <param name="message">The log message text.</param>
    void Trace(string message);

    /// <summary>Trace-level log that only emits when <see cref="LogConfig.PerFrameLogging"/> is enabled. Use for per-frame repetitive diagnostics (stage timing, render steps, etc.).</summary>
    /// <param name="message">The log message text.</param>
    void FrameTrace(string message);

    /// <summary>Writes a debug-level message for internal development diagnostics.</summary>
    /// <param name="message">The log message text.</param>
    void Debug(string message);

    /// <summary>Writes an informational message about normal application flow.</summary>
    /// <param name="message">The log message text.</param>
    void Info(string message);

    /// <summary>Writes a warning about a potentially harmful situation.</summary>
    /// <param name="message">The log message text.</param>
    void Warn(string message);

    /// <summary>Writes an error message about a recoverable failure.</summary>
    /// <param name="message">The log message text.</param>
    /// <param name="exception">Optional exception that caused the error.</param>
    void Error(string message, Exception? exception = null);

    /// <summary>Writes a critical error message about a failure that may terminate the application.</summary>
    /// <param name="message">The log message text.</param>
    /// <param name="exception">Optional exception that caused the critical failure.</param>
    void Critical(string message, Exception? exception = null);
}

namespace Engine;

/// <summary>Sink that receives formatted log messages from the logging infrastructure.</summary>
public interface ILoggerProvider
{
    /// <summary>Writes a log entry.</summary>
    /// <param name="level">The severity of the message.</param>
    /// <param name="category">The logger category (e.g., <c>"Engine.World"</c>).</param>
    /// <param name="message">The log message text.</param>
    /// <param name="exception">Optional exception associated with the message.</param>
    void Log(LogLevel level, string category, string message, Exception? exception = null);
}

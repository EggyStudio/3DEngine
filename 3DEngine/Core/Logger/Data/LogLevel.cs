namespace Engine;

/// <summary>Severity levels for log messages, ordered from least to most severe.</summary>
public enum LogLevel
{
    /// <summary>Finest-grained diagnostic detail (variable dumps, method entry/exit).</summary>
    Trace,
    /// <summary>Internal development diagnostics not shown to end users.</summary>
    Debug,
    /// <summary>Informational messages about normal application flow.</summary>
    Info,
    /// <summary>Potentially harmful situations that do not prevent operation.</summary>
    Warning,
    /// <summary>Recoverable failures that may degrade functionality.</summary>
    Error,
    /// <summary>Unrecoverable failures that will terminate the application.</summary>
    Critical
}

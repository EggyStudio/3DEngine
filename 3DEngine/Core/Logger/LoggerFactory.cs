namespace Engine;

/// <summary>Creates and caches <see cref="Logger"/> instances by category name.</summary>
public sealed class LoggerFactory
{
    private readonly Dictionary<string, Logger> _loggers = new();

    /// <summary>Creates or retrieves a cached logger for the given category.</summary>
    /// <param name="category">The category name for the logger.</param>
    /// <returns>A <see cref="Logger"/> instance for the specified category.</returns>
    public Logger CreateLogger(string category)
    {
        if (_loggers.TryGetValue(category, out var existing))
            return existing;
        var logger = new Logger(category);
        _loggers[category] = logger;
        return logger;
    }
}

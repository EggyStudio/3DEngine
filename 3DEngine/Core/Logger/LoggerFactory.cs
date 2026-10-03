using System.Collections.Concurrent;

namespace Engine;

/// <summary>Creates and caches <see cref="Logger"/> instances by category name.</summary>
/// <remarks>
/// Safe on any number of threads at once. Loggers are made in static initializers, which run on
/// whichever thread first touches a type, and xUnit runs test classes side by side. The cache was a
/// plain dictionary until a run corrupted it from two such initializers at once, which threw from
/// <c>World</c>'s initializer and so failed every test after it that made an <c>App</c>.
/// </remarks>
public sealed class LoggerFactory
{
    private readonly ConcurrentDictionary<string, Logger> _loggers = new();

    /// <summary>Creates or retrieves a cached logger for the given category.</summary>
    /// <param name="category">The category name for the logger.</param>
    /// <returns>A <see cref="Logger"/> instance for the specified category, the same one every call.</returns>
    public Logger CreateLogger(string category) => _loggers.GetOrAdd(category, static c => new Logger(c));
}

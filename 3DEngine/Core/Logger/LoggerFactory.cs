using System.Collections.Concurrent;

namespace Engine;

/// <summary>Creates and caches <see cref="Logger"/> instances by category name.</summary>
/// <remarks>
/// Safe on any number of threads at once. Loggers are made in static initializers, which run on
/// whichever thread first touches a type, and xUnit runs test classes side by side, so two
/// initializers can ask for a logger at once. A plain dictionary written from both can corrupt and
/// throw from a type's initializer, as <c>World</c>'s, which leaves the type unusable for the rest
/// of the process, so every later test that makes an <c>App</c> fails.
/// </remarks>
public sealed class LoggerFactory
{
    private readonly ConcurrentDictionary<string, Logger> _loggers = new();

    /// <summary>Creates or retrieves a cached logger for the given category.</summary>
    /// <param name="category">The category name for the logger.</param>
    /// <returns>A <see cref="Logger"/> instance for the specified category, the same one every call.</returns>
    public Logger CreateLogger(string category) => _loggers.GetOrAdd(category, static c => new Logger(c));
}

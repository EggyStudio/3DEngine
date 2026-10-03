namespace Engine;

/// <summary>
/// Default logger implementation that dispatches to the console provider, file provider,
/// and any additional user-added providers.
/// </summary>
/// <seealso cref="ConsoleLoggerProvider"/>
/// <seealso cref="FileLoggerProvider"/>
/// <seealso cref="LogConfig"/>
public sealed class Logger : ILogger
{
    private readonly string _category;
    private readonly List<ILoggerProvider> _extraProviders = new();

    /// <summary>Creates a new logger for the specified category.</summary>
    /// <param name="category">A hierarchical category name, e.g. <c>"Engine.World"</c>.</param>
    public Logger(string category)
    {
        _category = category;
    }

    /// <summary>
    /// Adds an extra log provider to this logger. Console and file providers are always included
    /// and cannot be added via this method.
    /// </summary>
    /// <param name="provider">The provider to add.</param>
    /// <returns>This <see cref="Logger"/> instance for fluent chaining.</returns>
    public Logger UseProvider(ILoggerProvider provider)
    {
        if (!_extraProviders.Contains(provider)
            && provider != ConsoleLoggerProvider.Instance
            && provider is not FileLoggerProvider)
            _extraProviders.Add(provider);
        return this;
    }

    /// <inheritdoc />
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        // Console: respects ConsoleMinimumLevel (defaults to Info - no startup traces on console).
        if (level >= LogConfig.ConsoleMinimumLevel)
            ConsoleLoggerProvider.Instance.Log(level, _category, message, exception);

        // File: respects MinimumLevel (defaults to Trace - all startup diagnostics captured to disk).
        // Late-bound lookup so loggers created before Initialize() still write to the file.
        if (level >= LogConfig.MinimumLevel && FileLoggerProvider.Instance is { } file)
            file.Log(level, _category, message, exception);

        // Any extra user-added providers.
        foreach (var provider in _extraProviders)
            if (level >= LogConfig.MinimumLevel)
                provider.Log(level, _category, message, exception);
    }

    /// <inheritdoc />
    public void Trace(string message) => Log(LogLevel.Trace, message);
    /// <inheritdoc />
    public void FrameTrace(string message) { if (LogConfig.PerFrameLogging) Log(LogLevel.Trace, message); }
    /// <inheritdoc />
    public void Debug(string message) => Log(LogLevel.Debug, message);
    /// <inheritdoc />
    public void Info(string message) => Log(LogLevel.Info, message);
    /// <inheritdoc />
    public void Warn(string message) => Log(LogLevel.Warning, message);
    /// <inheritdoc />
    public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
    /// <inheritdoc />
    public void Critical(string message, Exception? exception = null) => Log(LogLevel.Critical, message, exception);
}

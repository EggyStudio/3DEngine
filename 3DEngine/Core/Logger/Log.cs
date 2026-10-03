using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Static entry point for creating category-scoped loggers.
/// </summary>
/// <example>
/// <code>
/// // Create a category logger (cached on subsequent calls)
/// private static readonly ILogger Logger = Log.Category("Game.Physics");
///
/// // Or derive the category from a type name
/// private static readonly ILogger Logger = Log.For&lt;PhysicsSystem&gt;();
///
/// // Use severity helpers
/// Logger.Info("Simulation started");
/// Logger.Warn("Gravity is zero - objects will float");
/// Logger.Error("Collision solver diverged", ex);
///
/// // Tune output levels
/// LogConfig.ConsoleMinimumLevel = LogLevel.Warning;  // quieter console
/// LogConfig.PerFrameLogging = true;                  // enable per-frame trace
/// </code>
/// </example>
/// <seealso cref="Logger"/>
/// <seealso cref="LoggerFactory"/>
public static class Log
{
    /// <summary>Shared logger factory that caches instances by category.</summary>
    public static LoggerFactory Factory { get; } = new();

    /// <summary>Creates or retrieves a logger for the specified category name.</summary>
    /// <param name="category">The category name, e.g. <c>"Engine.Schedule"</c>.</param>
    /// <returns>A cached <see cref="ILogger"/> for the category.</returns>
    public static ILogger Category(string category) => Factory.CreateLogger(category);

    /// <summary>Creates or retrieves a logger using the full name of <typeparamref name="T"/> as category.</summary>
    /// <typeparam name="T">The type whose full name becomes the logger category.</typeparam>
    /// <returns>A cached <see cref="ILogger"/> for the type.</returns>
    public static ILogger For<T>() => Category(typeof(T).FullName ?? typeof(T).Name);

    /// <summary>Writes a startup banner with runtime, OS, and architecture information.</summary>
    public static void PrintStartupBanner()
    {
        var logger = Category("Engine");
        logger.Info("========================================================");
        logger.Info("  3DEngine - Initializing");
        logger.Info("========================================================");
        logger.Info($"Runtime:      {RuntimeInformation.FrameworkDescription}");
        logger.Info($"OS:           {RuntimeInformation.OSDescription}");
        logger.Info($"Architecture: {RuntimeInformation.ProcessArchitecture}");
        logger.Info($"Processors:   {Environment.ProcessorCount}");
        logger.Info($"Working Dir:  {Environment.CurrentDirectory}");
        logger.Info($"Base Dir:     {AppContext.BaseDirectory}");
        logger.Info($"Process ID:   {Environment.ProcessId}");
        logger.Info($"Timestamp:    {DateTime.UtcNow:O}");
        logger.Info($"Console log:  {LogConfig.ConsoleMinimumLevel}+");
        logger.Info($"File log:     {LogConfig.MinimumLevel}+ → {LogConfig.GetLogFilePath("Engine.log")}");
        logger.Info($"File cap:     {LogConfig.MaxLogFileBytes / (1024 * 1024)} MB");
        logger.Info($"Frame logs:   {(LogConfig.PerFrameLogging ? "ENABLED" : "DISABLED (set ENGINE_LOG_FRAMES=1 to enable)")}");
        logger.Info("========================================================");
    }
}

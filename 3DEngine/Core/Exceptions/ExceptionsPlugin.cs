using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Engine;

/// <summary>
/// Installs global handlers for unhandled and unobserved exceptions.
/// Exceptions are routed through the engine logger (console + Engine.log) and,
/// on fatal crashes, a focused <c>Crash.log</c> is written next to the executable.
/// </summary>
/// <remarks>
/// <para>
/// Hooks into <see cref="AppDomain.UnhandledException"/> and
/// <see cref="TaskScheduler.UnobservedTaskException"/>. Unobserved task exceptions are
/// observed after logging so they do not tear down the process in modern .NET.
/// </para>
/// <para>
/// A <see cref="Stage.Cleanup"/> system unsubscribes both handlers so they do not outlive
/// the application. The <see cref="ExceptionHandlerInstalled"/> marker resource prevents
/// double-registration if the plugin is added more than once.
/// </para>
/// </remarks>
/// <seealso cref="ExceptionHandlerInstalled"/>
/// <seealso cref="LogConfig"/>
public sealed class ExceptionsPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Exceptions");

    /// <inheritdoc />
    public void Build(App app)
    {
        if (app.World.ContainsResource<ExceptionHandlerInstalled>())
            return;

        Logger.Info("Installing global exception handlers (AppDomain + TaskScheduler)...");

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        app.World.InsertResource(new ExceptionHandlerInstalled());

        // Unsubscribe during Cleanup so handlers don't outlive the app.
        app.AddSystem(Stage.Cleanup, new SystemDescriptor(static world =>
            {
                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
                Logger.Debug("Global exception handlers unsubscribed.");
            }, "ExceptionsPlugin.Cleanup")
            .MainThreadOnly());

        Logger.Info("Exception handlers installed - unhandled exceptions will be logged to Engine.log and Crash.log.");
    }

    /// <summary>Handles <see cref="AppDomain.UnhandledException"/> events by logging and writing a crash dump.</summary>
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.Critical($"Unhandled exception (isTerminating={e.IsTerminating})", ex);
            WriteCrashLog(ex, fatal: e.IsTerminating);
        }
        else
        {
            Logger.Critical($"Unhandled non-Exception object: {e.ExceptionObject}");
        }
    }

    /// <summary>Handles <see cref="TaskScheduler.UnobservedTaskException"/> by logging and marking observed.</summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Log but observe so it doesn't tear down the process in modern .NET.
        Logger.Error("Unobserved task exception - this usually indicates a fire-and-forget Task that threw.", e.Exception);
        e.SetObserved();
    }

    /// <summary>
    /// Writes a focused crash dump to <c>Crash.log</c> containing exception details,
    /// stack trace with source locations, and basic environment info.
    /// This file is overwritten each crash so it always reflects the most recent failure.
    /// </summary>
    /// <param name="exception">The exception to dump.</param>
    /// <param name="fatal">Whether the exception is terminating the process.</param>
    private static void WriteCrashLog(Exception exception, bool fatal)
    {
        try
        {
            var path = LogConfig.GetLogFilePath("Crash.log");
            var sb = new StringBuilder();

            sb.AppendLine("========================================================");
            sb.AppendLine(fatal ? "  3DEngine - FATAL CRASH" : "  3DEngine - UNHANDLED EXCEPTION");
            sb.AppendLine("========================================================");
            sb.AppendLine($"Timestamp:  {DateTime.UtcNow:O}");
            sb.AppendLine($"Uptime:     {LogConfig.EngineTimer.Elapsed}");
            sb.AppendLine($"Runtime:    {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"OS:         {RuntimeInformation.OSDescription}");
            sb.AppendLine($"Process:    {Environment.ProcessId}");
            sb.AppendLine();

            // Walk the exception chain (including inners and AggregateException).
            FormatExceptionChain(sb, exception, depth: 0);

            File.WriteAllText(path, sb.ToString());
        }
        catch
        {
            // Best-effort - if we can't write the crash log, don't mask the original exception.
        }
    }

    /// <summary>Recursively formats an exception and its inner exceptions into a crash log string.</summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="ex">The exception to format.</param>
    /// <param name="depth">Current nesting depth (controls indentation).</param>
    private static void FormatExceptionChain(StringBuilder sb, Exception ex, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}[{ex.GetType().FullName}] {ex.Message}");

        // Attempt to include source file/line from the top stack frame.
        var trace = new StackTrace(ex, fNeedFileInfo: true);
        if (trace.FrameCount > 0)
        {
            var top = trace.GetFrame(0);
            var file = top?.GetFileName();
            var line = top?.GetFileLineNumber() ?? 0;
            var method = top?.GetMethod();
            if (method is not null)
            {
                var location = file is not null ? $"{file}:{line}" : "no source info";
                sb.AppendLine($"{indent}  at {method.DeclaringType?.FullName}.{method.Name} ({location})");
            }
        }

        sb.AppendLine($"{indent}Stack trace:");
        foreach (var traceLine in (ex.StackTrace ?? "  <no stack trace>").Split('\n'))
            sb.AppendLine($"{indent}  {traceLine.TrimEnd()}");

        // Recurse into inner exceptions.
        if (ex is AggregateException agg)
        {
            for (int i = 0; i < agg.InnerExceptions.Count; i++)
            {
                sb.AppendLine();
                sb.AppendLine($"{indent}--- Inner exception [{i}] ---");
                FormatExceptionChain(sb, agg.InnerExceptions[i], depth + 1);
            }
        }
        else if (ex.InnerException is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}--- Inner exception ---");
            FormatExceptionChain(sb, ex.InnerException, depth + 1);
        }
    }
}

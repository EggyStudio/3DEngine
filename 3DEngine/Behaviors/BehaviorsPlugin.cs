namespace Engine;

/// <summary>Invokes the source-generated behavior registrations to wire systems into the app.</summary>
/// <remarks>
/// Each assembly with behaviors adds its generated registration to <see cref="GeneratedBehaviors"/>
/// from a module initializer as it loads, and each is invoked here with the <see cref="App"/>, so
/// generated code registers systems, conditions and resources with no search through types, which
/// trimming and native AOT would break.
/// </remarks>
/// <example>
/// <code>
/// // Any [Behavior] struct in a loaded assembly is registered:
/// [Behavior]
/// public partial struct EnemyAI
/// {
///     [OnUpdate]
///     public static void Think(BehaviorContext ctx) { /* ... */ }
/// }
/// </code>
/// </example>
/// <seealso cref="BehaviorAttribute"/>
/// <seealso cref="GeneratedBehaviorRegistrationAttribute"/>
/// <seealso cref="EcsPlugin"/>
public sealed class BehaviorsPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Behaviors");

    /// <summary>
    /// Optional directory the <see cref="RuntimeBehaviorCompiler"/> watches for hot-reloadable
    /// behavior scripts. When <see langword="null"/>, only the compile-time registrations run and no
    /// runtime compiler is started.
    /// </summary>
    public string? ScriptsDirectory { get; init; } = Path.Combine(AppContext.BaseDirectory, "source", "behaviors");

    /// <summary>
    /// Provenance tag passed to <see cref="RuntimeBehaviorCompiler.SourceTag"/>. Used by
    /// <see cref="App.RemoveSystemsBySource"/> when swapping generations on hot-reload.
    /// </summary>
    public string DynamicSourceTag { get; init; } = "Dynamic.Behaviors";

    /// <inheritdoc />
    public void Build(App app)
    {
        // Static contribution: tag every descriptor registered through generated methods so a
        // future hot-reload of those same behaviors (under DynamicSourceTag) does not collide.
        var registrations = GeneratedBehaviors.All;
        using (new SystemRegistrationSourceScope("Static.Behaviors"))
            foreach (var register in registrations)
                register(app);
        Logger.Info($"BehaviorsPlugin: {registrations.Count} generated behavior registration(s) invoked.");

        // Optional dynamic contribution: hot-reload via Roslyn from a scripts directory. A native
        // build cannot load an assembly it compiles, and the AOT compiler takes this check as the
        // constant false, so it drops the compiler and Roslyn with it from the executable.
        if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported && !string.IsNullOrEmpty(ScriptsDirectory))
        {
            Logger.Info($"BehaviorsPlugin: Starting RuntimeBehaviorCompiler at '{ScriptsDirectory}'.");
            var compiler = new RuntimeBehaviorCompiler(app, DynamicSourceTag).WatchDirectory(ScriptsDirectory);
            var initial = compiler.Start();
            Logger.Info($"  Initial behavior compile: {initial.Message} ({initial.RegisteredCount} system(s)).");
            foreach (var err in initial.Errors)
                Logger.Error($"    {err.FileName}({err.Line},{err.Column}): {err.Message}");

            compiler.CompilationCompleted += result =>
            {
                Logger.Info($"  Hot-reload behaviors: {result.Message} ({result.RegisteredCount} system(s)).");
                foreach (var err in result.Errors)
                    Logger.Error($"    {err.FileName}({err.Line},{err.Column}): {err.Message}");
            };

            // Expose for tests / diagnostics; dispose on Cleanup stage.
            app.World.InsertResource(compiler);
            app.AddSystem(Stage.Cleanup, new SystemDescriptor(_ =>
            {
                try { compiler.Dispose(); }
                catch (Exception ex) { Logger.Warn($"RuntimeBehaviorCompiler dispose failed: {ex.Message}"); }
            }, "BehaviorsPlugin.DisposeCompiler").MainThreadOnly());
        }
    }
}

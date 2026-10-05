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
internal sealed class BehaviorsPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Behaviors");

    /// <summary>
    /// Optional directory the <see cref="RuntimeBehaviorCompiler"/> watches for hot-reloadable
    /// behavior scripts. When <see langword="null"/>, only the compile-time registrations run and no
    /// runtime compiler is started.
    /// </summary>
    /// <remarks>
    /// By default it is <c>source/behaviors</c> beside the program, or, for a program run from the
    /// build folder of a project that has that folder, the project's own, so a script saved where it
    /// is written takes hold in the running game rather than waiting for the next build to copy it.
    /// </remarks>
    public string? ScriptsDirectory { get; init; } = DefaultScriptsDirectory(AppContext.BaseDirectory);

    // The project's scripts when the program runs from bin/<configuration>/<framework> under a
    // project that has them, and those beside the program otherwise, as a shipped game has.
    internal static string DefaultScriptsDirectory(string programFolder)
    {
        var project = Path.GetFullPath(Path.Combine(programFolder, "..", "..", ".."));
        var theirs = Path.Combine(project, "source", "behaviors");
        if (Directory.Exists(theirs) && Directory.EnumerateFiles(project, "*.csproj").Any()) return theirs;
        return Path.Combine(programFolder, "source", "behaviors");
    }

    /// <summary>
    /// Provenance tag passed to <see cref="RuntimeBehaviorCompiler.SourceTag"/>. Used by
    /// <see cref="App.RemoveSystemsBySource"/> when swapping generations on hot-reload.
    /// </summary>
    public string DynamicSourceTag { get; init; } = "Dynamic.Behaviors";

    /// <inheritdoc />
    public void Build(App app)
    {
        // Every descriptor the generated methods register is tagged, so a hot reload of the same
        // behaviors (under DynamicSourceTag) does not collide with it.
        var registrations = GeneratedBehaviors.All;
        using (new SystemRegistrationSourceScope("Static.Behaviors"))
            foreach (var register in registrations)
                register(app);
        Logger.Info($"BehaviorsPlugin: {registrations.Count} generated behavior registration(s) invoked.");

        // Behaviors are also reloaded through Roslyn from a scripts directory when one is named. A native
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

namespace Engine;

/// <summary>Discovers and invokes source-generated behavior registration methods to wire systems into the app.</summary>
/// <remarks>
/// Scans all loaded assemblies for static methods annotated with
/// <see cref="GeneratedBehaviorRegistrationAttribute"/>. Each discovered method is invoked with the
/// <see cref="App"/> instance, allowing generated code to register systems, conditions, and resources.
/// </remarks>
/// <example>
/// <code>
/// // Any [Behavior] struct in loaded assemblies is auto-discovered and registered:
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
    /// behavior scripts. When <see langword="null"/> (default), only compile-time
    /// <c>[GeneratedBehaviorRegistration]</c> methods are scanned and no runtime compiler is started.
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
        Logger.Info("BehaviorsPlugin: Scanning assemblies for generated behavior registrations...");
        int found = 0;
        // Static contribution: tag every descriptor registered through generated methods so a
        // future hot-reload of those same behaviors (under DynamicSourceTag) does not collide.
        using (new SystemRegistrationSourceScope("Static.Behaviors"))
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                try
                {
                    var bindingFlags = System.Reflection.BindingFlags.Public |
                                       System.Reflection.BindingFlags.NonPublic |
                                       System.Reflection.BindingFlags.Static;
                    var attributeType = typeof(GeneratedBehaviorRegistrationAttribute);
                    foreach (var type in asm.GetTypes())
                    foreach (var m in type.GetMethods(bindingFlags))
                    {
                        if (m.GetCustomAttributes(attributeType, inherit: false).Length == 0) continue;
                        var ps = m.GetParameters();
                        if (ps.Length == 1 && ps[0].ParameterType == typeof(App))
                        {
                            Logger.Debug($"  Invoking behavior registration: {type.Name}.{m.Name}");
                            m.Invoke(null, [app]);
                            found++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"  Failed to scan assembly {asm.GetName().Name}: {ex.Message}");
                }
            }
        }
        Logger.Info($"BehaviorsPlugin: {found} static behavior registration(s) discovered and invoked.");

        // Optional dynamic contribution: hot-reload via Roslyn from a scripts directory.
        if (!string.IsNullOrEmpty(ScriptsDirectory))
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

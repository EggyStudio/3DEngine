using System.Collections.Immutable;
using System.Reflection;
using Engine.Files.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Engine;

/// <summary>
/// Runtime Roslyn compiler for hot-reloadable <c>[Behavior]</c> structs. Mirrors the shell hot-reload
/// pipeline: watches a directory tree, recompiles on change, loads the result into an isolated
/// collectible <see cref="System.Runtime.Loader.AssemblyLoadContext"/>, and registers the behaviors
/// into the live <see cref="App"/>.
/// </summary>
/// <remarks>
/// <para>
/// Source generation parity: at compile time the <c>BehaviorGenerator</c> incremental generator
/// (<c>Modules/Engine.Entities.Behaviors/Generator/</c>) is consumed by the analyzer DLL; at runtime
/// the <em>same</em> generator type is also compiled into <c>3DEngine.dll</c> (see the engine csproj's
/// <c>Modules\**\Generator\**</c> Compile glob) so this compiler can attach it via
/// <see cref="CSharpGeneratorDriver"/> and produce the same <c>BehaviorsRegistration.g.cs</c>
/// + per-behavior system files for hot-loaded scripts.
/// </para>
/// <para>
/// Hot-reload contract: every successful compile evicts any prior generation of dynamic systems via
/// <see cref="App.RemoveSystemsBySource"/> (using <see cref="SourceTag"/>), then invokes the
/// generated <c>[GeneratedBehaviorRegistration]</c> method discovered on the new assembly under a
/// <see cref="SystemRegistrationSourceScope"/> so newly added descriptors inherit the same tag.
/// </para>
/// <para>
/// Limitation: behavior structs in a recompiled assembly are <em>new</em> CLR types in a fresh
/// load context, so any entity components of the previous generation's struct type are stranded
/// in the ECS until a re-spawn. v1 logs a warning; future versions can clear those component stores
/// on swap.
/// </para>
/// </remarks>
public sealed class RuntimeBehaviorCompiler : RuntimeAssemblyCompiler<BehaviorCompilationResult>
{
    private static readonly ILogger Logger = Log.Category("Engine.Behaviors.HotReload");

    private readonly App _app;

    /// <summary>Provenance tag applied to every system descriptor registered by this compiler.</summary>
    /// <remarks>Used by <see cref="App.RemoveSystemsBySource"/> to drop the previous generation on swap.</remarks>
    public string SourceTag { get; }

    /// <inheritdoc />
    protected override string AssemblyNamePrefix => "BehaviorScripts";

    /// <inheritdoc />
    protected override string LoadContextPrefix => "Behaviors";

    /// <summary>Creates a new behavior hot-reload compiler bound to <paramref name="app"/>.</summary>
    /// <param name="app">The live application; freshly compiled behaviors are registered into its <see cref="App.Schedule"/>.</param>
    /// <param name="sourceTag">Provenance tag for hot-reloadable systems. Defaults to <c>"Dynamic.Behaviors"</c>.</param>
    public RuntimeBehaviorCompiler(App app, string sourceTag = "Dynamic.Behaviors")
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        SourceTag = sourceTag;

        // Engine assembly carries App, EcsWorld, BehaviorAttribute, and the BehaviorGenerator type itself.
        AddReference(typeof(App).Assembly);
    }

    /// <summary>Fluent <see cref="RuntimeAssemblyCompiler{TResult}.WatchDirectory"/> typed for chaining.</summary>
    public new RuntimeBehaviorCompiler WatchDirectory(string path)
    {
        base.WatchDirectory(path);
        return this;
    }

    /// <summary>Fluent <see cref="RuntimeAssemblyCompiler{TResult}.AddReference(Assembly)"/> typed for chaining.</summary>
    public new RuntimeBehaviorCompiler AddReference(Assembly assembly)
    {
        base.AddReference(assembly);
        return this;
    }

    /// <inheritdoc />
    protected override void OnNoSourceFiles(BehaviorCompilationResult result)
    {
        // Empty scripts directory => evict any prior dynamic generation so behaviors can be removed by deleting files.
        var removed = _app.RemoveSystemsBySource(SourceTag);
        result.Success = true;
        result.RegisteredCount = 0;
        result.Message = removed > 0
            ? $"No script files found; unregistered {removed} dynamic behavior system(s)."
            : "No script files found.";
    }

    /// <inheritdoc />
    protected override CSharpCompilation RunGenerators(CSharpCompilation compilation, BehaviorCompilationResult result)
    {
        // Re-run BehaviorGenerator (the same incremental generator that runs at engine compile time)
        // against the user's hot-loaded sources so [Behavior] structs get their per-stage system code
        // and the [GeneratedBehaviorRegistration] entry-point for free.
        ISourceGenerator generator;
        try
        {
            generator = new BehaviorGenerator().AsSourceGenerator();
        }
        catch (Exception ex)
        {
            result.Errors.Add(new RuntimeCompilationError
            {
                FileName = "BehaviorGenerator",
                Message = $"Failed to instantiate BehaviorGenerator at runtime: {ex.Message}",
            });
            return compilation;
        }

        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var updated, out var diags);

        foreach (var diag in diags)
        {
            if (diag.Severity == DiagnosticSeverity.Error)
            {
                var lineSpan = diag.Location.GetMappedLineSpan();
                result.Errors.Add(new RuntimeCompilationError
                {
                    FileName = Path.GetFileName(lineSpan.Path ?? "BehaviorGenerator"),
                    Message = diag.GetMessage(),
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                });
            }
            else if (diag.Severity == DiagnosticSeverity.Warning)
            {
                result.Warnings.Add($"BehaviorGenerator: {diag.GetMessage()}");
            }
        }

        return (CSharpCompilation)updated;
    }

    /// <inheritdoc />
    protected override void OnAssemblyLoaded(Assembly assembly, BehaviorCompilationResult result)
    {
        // 1) Drop the previous generation's hot-reloaded systems before registering the new ones.
        var removed = _app.RemoveSystemsBySource(SourceTag);
        if (removed > 0)
            Logger.Debug($"Hot-reload: removed {removed} previous dynamic behavior system(s).");

        // 2) Discover and invoke every [GeneratedBehaviorRegistration]-tagged static method.
        //    The ambient SystemRegistrationSourceScope auto-tags new descriptors with SourceTag
        //    so the next swap can evict them.
        int invoked = 0;
        var attrType = typeof(GeneratedBehaviorRegistrationAttribute);
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        using (new SystemRegistrationSourceScope(SourceTag))
        {
            foreach (var type in SafeGetTypes(assembly, result))
            {
                foreach (var m in type.GetMethods(bindingFlags))
                {
                    if (m.GetCustomAttributes(attrType, inherit: false).Length == 0) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(App))
                    {
                        try
                        {
                            m.Invoke(null, [_app]);
                            invoked++;
                        }
                        catch (Exception ex)
                        {
                            result.Warnings.Add($"Failed to invoke {type.Name}.{m.Name}: {ex.Message}");
                        }
                    }
                }
            }
        }

        result.RegisteredCount = invoked;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly asm, BehaviorCompilationResult result)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            result.Warnings.Add($"Type load failures: {ex.LoaderExceptions.Length}");
            return ex.Types.Where(t => t != null)!;
        }
    }
}
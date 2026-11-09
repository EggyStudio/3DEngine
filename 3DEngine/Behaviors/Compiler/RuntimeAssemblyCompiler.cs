using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Engine.Files.Compiler;

/// <summary>
/// Reusable abstract base for runtime Roslyn compilers that watch a directory tree, recompile on
/// change with debounce, load the result into an isolated collectible <see cref="AssemblyLoadContext"/>,
/// and hand the freshly loaded assembly off to a domain-specific specialization
/// (e.g. shell discovery, behavior generator-driven registration).
/// </summary>
/// <remarks>
/// <para>
/// Concrete features owned here:
/// </para>
/// <list type="bullet">
///   <item><description>File watcher with 300ms debounce coalescing.</description></item>
///   <item><description>Collectible <see cref="AssemblyLoadContext"/> per generation
///         (<see cref="ScriptLoadContext"/>) so the previous generation's assembly can be GC'd.</description></item>
///   <item><description>Default <c>System.*</c> / <c>Microsoft.*</c> reference set sourced from
///         <c>TRUSTED_PLATFORM_ASSEMBLIES</c>, read at the first compilation and shared by every
///         compiler in the process.</description></item>
///   <item><description>In-memory Roslyn <c>.cs</c> compilation pipeline (<see cref="CompileWithRoslyn"/>).</description></item>
/// </list>
/// <para>
/// Subclasses provide:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="OnAssemblyLoaded"/> takes the loaded assembly,
///         tag/register their domain artifacts, populate the result.</description></item>
///   <item><description>Optional <see cref="OnNoSourceFiles"/> hook for "scripts directory empty" handling.</description></item>
///   <item><description>Optional <see cref="RunGenerators"/> hook to attach Roslyn source generators
///         (used by <c>RuntimeBehaviorCompiler</c> to re-run <c>BehaviorGenerator</c> at runtime).</description></item>
/// </list>
/// </remarks>
/// <typeparam name="TResult">Domain-specific result type derived from <see cref="RuntimeCompilationResult"/>.</typeparam>
internal abstract partial class RuntimeAssemblyCompiler<TResult> : IDisposable
    where TResult : RuntimeCompilationResult, new()
{
    /// <summary>Directories that contribute source files to every compilation cycle.</summary>
    protected readonly List<string> _scriptDirectories = [];

    /// <summary>Assemblies read as Roslyn metadata references at the first compilation, the default and the user-added.</summary>
    protected readonly List<string> _referencePaths = [];

    /// <summary>Roslyn metadata references added as they are, beside those read from <see cref="_referencePaths"/>.</summary>
    protected readonly List<MetadataReference> _references = [];

    /// <summary>File system paths of user-added assemblies .</summary>
    protected readonly List<string> _userAssemblyPaths = [];

    /// <summary>The compiler's places among those watching its directories, which <see cref="DirectoryWatches"/> shares.</summary>
    protected readonly List<IDisposable> _watchers = [];

    /// <summary>Serializes <see cref="CompileAndLoad"/> across debounce-timer threads and manual <see cref="Recompile"/>.</summary>
    protected readonly Lock _compileLock = new();

    /// <summary>Pending recompile timer (debounce window).</summary>
    protected Timer? _debounceTimer;

    /// <summary>Currently loaded script context, replaced on every successful compile.</summary>
    protected ScriptLoadContext? _currentContext;

    /// <summary>Monotonic compilation generation counter (for unique assembly names).</summary>
    protected int _generation;

    /// <summary>Fired when a compilation cycle completes (success or failure).</summary>
    public event Action<TResult>? CompilationCompleted;

    /// <summary>
    /// File-extension globs the file watcher subscribes to. Defaults to <c>["*.cs"]</c>.
    /// </summary>
    protected virtual string[] WatchedExtensions => ["*.cs"];

    /// <summary>Compiled assembly name prefix (each generation is suffixed with <c>_Gen{n}</c>).</summary>
    protected virtual string AssemblyNamePrefix => "DynamicScripts";

    /// <summary>Display-name prefix for the per-generation <see cref="AssemblyLoadContext"/>.</summary>
    protected virtual string LoadContextPrefix => "Scripts";

    /// <summary>Initializes the default reference set. Subclasses should call <see cref="AddReference(Assembly)"/>
    /// from their own constructor for any assemblies their domain needs at compile time.</summary>
    protected RuntimeAssemblyCompiler()
    {
        AddDefaultReferences();
    }

    /// <summary>Domain-specific hook invoked after a freshly compiled assembly is loaded.</summary>
    /// <param name="assembly">The loaded assembly from the new <see cref="ScriptLoadContext"/>.</param>
    /// <param name="result">Result instance to populate with domain-specific metadata / warnings.</param>
    protected abstract void OnAssemblyLoaded(Assembly assembly, TResult result);

    /// <summary>
    /// Hook invoked when no source files are present in any watched directory. Default behavior
    /// marks the result as a successful no-op. Override to clear any prior dynamic registrations
    /// (e.g. unregister all hot-reloaded behaviors / shells when scripts are deleted).
    /// </summary>
    /// <param name="result">Result instance to populate.</param>
    protected virtual void OnNoSourceFiles(TResult result)
    {
        result.Success = true;
        result.Message = "No script files found.";
    }

    /// <summary>
    /// Hook invoked just before <c>compilation.Emit</c> in the in-memory Roslyn path. Override to
    /// attach a <c>CSharpGeneratorDriver</c> with one or more <see cref="ISourceGenerator"/>s and
    /// return the generator-augmented compilation. Default returns <paramref name="compilation"/> unchanged.
    /// </summary>
    /// <param name="compilation">The user-source compilation about to be emitted.</param>
    /// <param name="result">Compilation result to append generator diagnostics to.</param>
    /// <returns>The compilation to actually emit (may include generator outputs).</returns>
    protected virtual CSharpCompilation RunGenerators(CSharpCompilation compilation, TResult result) => compilation;
}
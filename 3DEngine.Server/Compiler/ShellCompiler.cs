using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Editor.Shell;

/// <summary>
/// Runtime Roslyn compiler that watches a scripts directory, compiles .cs files
/// on change, loads the resulting assembly into an isolated <see cref="AssemblyLoadContext"/>,
/// discovers <see cref="IEditorShellBuilder"/> implementations, and pushes the
/// rebuilt <see cref="ShellDescriptor"/> into the <see cref="ShellRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// The compiler is split across several partial files:
/// <list>
///   <item><description><c>ShellCompiler.cs</c> core fields, constructor, and event.</description></item>
///   <item><description><c>ShellCompiler.Configuration.cs</c> <see cref="WatchDirectory"/> and <c>AddReference</c> methods.</description></item>
///   <item><description><c>ShellCompiler.Compilation.cs</c> Roslyn compilation and <see cref="IEditorShellBuilder"/> discovery.</description></item>
///   <item><description><c>ShellCompiler.FileWatcher.cs</c> <see cref="FileSystemWatcher"/> event handlers with debounce timer.</description></item>
///   <item><description><c>ShellCompiler.Lifecycle.cs</c> <see cref="Start"/>, <see cref="Recompile"/>, and <see cref="Dispose"/>.</description></item>
///   <item><description><c>ShellCompiler.LoadContext.cs</c> isolated <see cref="AssemblyLoadContext"/> and default reference setup.</description></item>
/// </list>
/// </para>
/// <para>
/// Each compilation produces a new collectible <see cref="AssemblyLoadContext"/>, enabling
/// previous script assemblies to be garbage-collected after hot-reload. A 300ms debounce
/// timer coalesces rapid file changes into a single recompilation.
/// </para>
/// </remarks>
/// <seealso cref="ShellRegistry"/>
/// <seealso cref="ShellDescriptor"/>
/// <seealso cref="ShellCompilationResult"/>
/// <seealso cref="IEditorShellBuilder"/>
public sealed partial class ShellCompiler : IDisposable
{
    private readonly ShellRegistry _registry;
    private readonly List<string> _scriptDirectories = [];
    private readonly List<MetadataReference> _references = [];
    private readonly List<string> _userAssemblyPaths = [];
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Lock _compileLock = new();

    private Timer? _debounceTimer;
    private ScriptLoadContext? _currentContext;
    private int _generation;

    /// <summary>Fired when compilation completes (success or failure).</summary>
    public event Action<ShellCompilationResult>? CompilationCompleted;

    /// <summary>Creates a new <see cref="ShellCompiler"/> targeting the specified registry.</summary>
    /// <param name="registry">The shell registry to update with compiled descriptors.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    public ShellCompiler(ShellRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        AddDefaultReferences();
    }
}


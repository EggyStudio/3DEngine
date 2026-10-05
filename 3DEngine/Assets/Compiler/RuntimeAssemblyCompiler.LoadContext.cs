using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Engine.Files.Compiler;

internal abstract partial class RuntimeAssemblyCompiler<TResult>
{
    /// <summary>Unloads the current script <see cref="AssemblyLoadContext"/>, allowing GC of its assembly.</summary>
    protected void UnloadCurrent()
    {
        if (_currentContext == null) return;
        _currentContext.Unload();
        _currentContext = null;
    }

    /// <summary>
    /// Adds the paths of the essential .NET runtime assemblies (System.*, Microsoft.*, mscorlib,
    /// netstandard) from <c>TRUSTED_PLATFORM_ASSEMBLIES</c> to <c>_referencePaths</c>.
    /// </summary>
    private void AddDefaultReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trustedPlatformAssemblies is null) return;

        foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            var fileName = Path.GetFileName(path);
            if (fileName.StartsWith("System.") || fileName.StartsWith("Microsoft.") ||
                fileName == "mscorlib.dll" || fileName == "netstandard.dll")
                _referencePaths.Add(path);
        }
    }

    // Each assembly read once for the process and shared by every compiler. A reference holds its
    // file's whole image in native memory, which only its finalizer gives back, so read at every
    // compiler's start, as they were, each app with the behaviors plugin took some 60 MB the GC
    // did not count, scripts or none, and a test run making hundreds of apps reached 16 GB before
    // a full collection came. An assembly that cannot be read is left out, as it was.
    private static readonly ConcurrentDictionary<string, MetadataReference?> SharedReferences = new();

    /// <summary>The references a compilation is given, read from <c>_referencePaths</c> the first time any compiler asks.</summary>
    protected List<MetadataReference> References()
    {
        var references = new List<MetadataReference>(_referencePaths.Count + _references.Count);
        foreach (var path in _referencePaths)
            if (SharedReferences.GetOrAdd(path, Read) is { } reference)
                references.Add(reference);
        references.AddRange(_references);
        return references;

        static MetadataReference? Read(string path)
        {
            try
            {
                return MetadataReference.CreateFromFile(path);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>Collectible <see cref="AssemblyLoadContext"/> used to isolate compiled script assemblies.</summary>
    protected sealed class ScriptLoadContext(string name) : AssemblyLoadContext(name, isCollectible: true)
    {
        /// <summary>Falls through to the default context for all dependency resolution.</summary>
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
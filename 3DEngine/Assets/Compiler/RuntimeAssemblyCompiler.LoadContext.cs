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
    private void AddDefaultReferences() => _referencePaths.AddRange(CompilerReferences.Platform());

    /// <summary>The references a compilation is given, read from <c>_referencePaths</c> the first time any compiler asks.</summary>
    protected List<MetadataReference> References()
    {
        var references = new List<MetadataReference>(_referencePaths.Count + _references.Count);
        foreach (var path in _referencePaths)
            if (CompilerReferences.Of(path) is { } reference)
                references.Add(reference);
        references.AddRange(_references);
        return references;
    }

    /// <summary>Collectible <see cref="AssemblyLoadContext"/> used to isolate compiled script assemblies.</summary>
    protected sealed class ScriptLoadContext(string name) : AssemblyLoadContext(name, isCollectible: true)
    {
        /// <summary>Falls through to the default context for all dependency resolution.</summary>
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
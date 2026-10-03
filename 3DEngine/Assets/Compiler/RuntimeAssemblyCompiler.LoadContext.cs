using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Engine.Files.Compiler;

public abstract partial class RuntimeAssemblyCompiler<TResult>
{
    /// <summary>Unloads the current script <see cref="AssemblyLoadContext"/>, allowing GC of its assembly.</summary>
    protected void UnloadCurrent()
    {
        if (_currentContext == null) return;
        _currentContext.Unload();
        _currentContext = null;
    }

    /// <summary>
    /// Populates <c>_references</c> with essential .NET runtime assemblies (System.*, Microsoft.*,
    /// mscorlib, netstandard) sourced from <c>TRUSTED_PLATFORM_ASSEMBLIES</c>.
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
            {
                try
                {
                    _references.Add(MetadataReference.CreateFromFile(path));
                }
                catch
                {
                    /* skip inaccessible assemblies */
                }
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
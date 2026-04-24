using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Editor.Shell;

public sealed partial class ShellCompiler
{
    /// <summary>Unloads the current script <see cref="AssemblyLoadContext"/> (if any), allowing the
    /// previous generation's assembly to be garbage-collected.</summary>
    private void UnloadCurrent()
    {
        if (_currentContext == null) return;
        _currentContext.Unload();
        _currentContext = null;
    }

    /// <summary>
    /// Populates <c>_references</c> with essential .NET runtime assemblies (System.*, Microsoft.*, mscorlib, netstandard)
    /// from the trusted platform assemblies list, plus the Editor.Shell assembly itself.
    /// </summary>
    private void AddDefaultReferences()
    {
        // Core runtime assemblies
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trustedPlatformAssemblies != null)
        {
            foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                var fileName = Path.GetFileName(path);
                // Include essential runtime assemblies
                if (fileName.StartsWith("System.") || fileName.StartsWith("Microsoft.") ||
                    fileName == "mscorlib.dll" || fileName == "netstandard.dll")
                {
                    try { _references.Add(MetadataReference.CreateFromFile(path)); }
                    catch { /* skip inaccessible assemblies */ }
                }
            }
        }

        // Add Editor.Shell itself (so scripts can reference the builder API)
        AddReference(typeof(ShellRegistry).Assembly);
    }

    /// <summary>
    /// Collectible <see cref="AssemblyLoadContext"/> used to isolate compiled script assemblies.
    /// Each generation creates a new context so the previous one can be unloaded.
    /// </summary>
    /// <param name="name">Display name for the load context (e.g. <c>"Scripts_Gen3"</c>).</param>
    private sealed class ScriptLoadContext(string name) : AssemblyLoadContext(name, isCollectible: true)
    {
        /// <summary>Returns <see langword="null"/> to fall through to the default context for all dependencies.</summary>
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}

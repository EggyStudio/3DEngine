using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Editor.Shell;

public sealed partial class ShellCompiler
{
    /// <summary>Adds a directory to watch for .cs script files.</summary>
    /// <param name="path">Directory path. Created if it does not exist.</param>
    /// <returns>This compiler for fluent configuration chaining.</returns>
    public ShellCompiler WatchDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        _scriptDirectories.Add(Path.GetFullPath(path));
        return this;
    }

    /// <summary>Adds an assembly to the compilation reference set.</summary>
    /// <param name="assembly">The assembly whose metadata to reference. Skipped if the location is empty or inaccessible.</param>
    /// <returns>This compiler for fluent configuration chaining.</returns>
    public ShellCompiler AddReference(Assembly assembly)
    {
        var location = assembly.Location;
        if (!string.IsNullOrEmpty(location) && File.Exists(location))
        {
            _references.Add(MetadataReference.CreateFromFile(location));
            _userAssemblyPaths.Add(location);
        }
        return this;
    }

    /// <summary>Adds a metadata reference directly.</summary>
    /// <param name="reference">The Roslyn <see cref="MetadataReference"/> to add.</param>
    /// <returns>This compiler for fluent configuration chaining.</returns>
    public ShellCompiler AddReference(MetadataReference reference)
    {
        _references.Add(reference);
        return this;
    }
}

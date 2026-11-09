using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace Engine.Files.Compiler;

/// <summary>The assemblies Roslyn compiles scripts and the console's fragments against.</summary>
internal static class CompilerReferences
{
    // Each assembly read once for the process and shared by every compiler. A reference holds its
    // file's whole image in native memory, which only its finalizer gives back, so read at every
    // compiler's start, as they were, each app with the behaviors plugin took some 60 MB the GC
    // did not count, scripts or none, and a test run making hundreds of apps reached 16 GB before
    // a full collection came. An assembly that cannot be read is left out, as it was.
    private static readonly ConcurrentDictionary<string, MetadataReference?> Shared = new();

    /// <summary>
    /// The paths of the platform's own assemblies (System.*, Microsoft.*, mscorlib, netstandard)
    /// from <c>TRUSTED_PLATFORM_ASSEMBLIES</c>.
    /// </summary>
    public static IEnumerable<string> Platform()
    {
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string trusted) yield break;
        foreach (var path in trusted.Split(Path.PathSeparator))
        {
            var fileName = Path.GetFileName(path);
            if (fileName.StartsWith("System.") || fileName.StartsWith("Microsoft.") ||
                fileName == "mscorlib.dll" || fileName == "netstandard.dll")
                yield return path;
        }
    }

    /// <summary>The reference for the assembly at <paramref name="path"/>, read the first time it is asked for, or <c>null</c> where it cannot be read.</summary>
    public static MetadataReference? Of(string path) => Shared.GetOrAdd(path, Read);

    private static MetadataReference? Read(string path)
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

using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Engine.Tests;

/// <summary>The engine's assemblies as the tests read them, the flat API's and each area's.</summary>
internal static class TestEngine
{
    /// <summary>Every assembly of the engine, in the order <see cref="EngineAssemblies.Names"/> lists them.</summary>
    public static IReadOnlyList<Assembly> Assemblies { get; } = [.. EngineAssemblies.Names.Select(name => Assembly.Load(new AssemblyName(name)))];

    /// <summary>A reference to each, for a compilation a test makes of a program on the engine.</summary>
    public static IEnumerable<MetadataReference> References() => Assemblies.Select(a => MetadataReference.CreateFromFile(a.Location));
}

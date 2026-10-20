using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Engine.Tests.Scenes;

/// <summary>
/// Scene files written before a component or a field was renamed, read through the codecs the
/// generator writes for components marked with their former names, compiled and loaded as a
/// program's own would be.
/// </summary>
[Trait("Category", "Integration")]
public class SceneMigrationTests
{
    private const string Source = """
        using Engine;

        namespace Renamed;

        // Once Chest, its coins once Gold.
        [SceneComponent, FormerName("Chest")]
        public struct Coffer { [FormerName("Gold")] public int Coins; public bool Opened; }

        // Moved here from another namespace.
        [SceneComponent, FormerName("Old.Place.Barrel")]
        public struct Keg { public int Litres; }

        // Once Lamp, a name another type has since taken.
        [SceneComponent, FormerName("Lamp")]
        public struct Lantern { public float Glow; }

        [SceneComponent]
        public struct Lamp { public float Watts; }

        public static class Probe
        {
            public static string Read(EcsWorld ecs, int entity) =>
                ecs.Has<Coffer>(entity) ? $"Coffer {ecs.GetRef<Coffer>(entity).Coins} {ecs.GetRef<Coffer>(entity).Opened}"
                : ecs.Has<Keg>(entity) ? $"Keg {ecs.GetRef<Keg>(entity).Litres}"
                : ecs.Has<Lantern>(entity) ? $"Lantern {ecs.GetRef<Lantern>(entity).Glow}"
                : ecs.Has<Lamp>(entity) ? $"Lamp {ecs.GetRef<Lamp>(entity).Watts}"
                : "none";
        }
        """;

    private static readonly Lazy<MethodInfo> Read = new(() => CompileAndLoad().GetType("Renamed.Probe")!.GetMethod("Read")!);

    // A scene file of one entity holding one component as given, read into a world of its own, and
    // the component it read as the probe says it.
    private static (string Read, string Saved) Load(string component)
    {
        var json = $$"""{ "format": "3dengine-scene", "version": 1, "entities": [ { "id": "a", "components": { {{component}} } } ] }""";
        var world = new World();
        world.InsertResource(new EcsWorld());
        var read = Read.Value;
        var spawned = SceneFile.Read(world, json);
        var ecs = world.Resource<EcsWorld>();
        return ((string)read.Invoke(null, [ecs, spawned[0]])!, SceneFile.Write(ecs));
    }

    [Fact]
    public void A_Component_Renamed_Is_Read_From_Its_Old_Name_And_Saved_Under_Its_New_One()
    {
        var (read, saved) = Load("\"Chest\": { \"Gold\": 7, \"Opened\": true }");

        read.Should().Be("Coffer 7 True", "the type and its field are read from the names the file has");
        saved.Should().Contain("\"Coffer\"").And.Contain("\"Coins\": 7").And.NotContain("Chest").And.NotContain("Gold");
    }

    [Fact]
    public void A_Type_Moved_From_Another_Namespace_Is_Read_From_Its_Old_Full_Name_And_Its_Old_Name_Alone()
    {
        Load("\"Old.Place.Barrel\": { \"Litres\": 30 }").Read.Should().Be("Keg 30");
        Load("\"Barrel\": { \"Litres\": 40 }").Read.Should().Be("Keg 40", "a file wrote a component under its name alone unless two shared it");
    }

    [Fact]
    public void A_Field_Is_Read_From_Its_Own_Name_Before_A_Former_One()
    {
        Load("\"Coffer\": { \"Coins\": 3, \"Gold\": 9 }").Read.Should().Be("Coffer 3 False");
    }

    [Fact]
    public void A_Former_Name_Another_Type_Has_Since_Taken_Goes_To_That_Type()
    {
        Load("\"Lamp\": { \"Watts\": 60 }").Read.Should().Be("Lamp 60");
        Load("\"Renamed.Lamp\": { \"Watts\": 40 }").Read.Should().Be("Lamp 40");
    }

    // Runs the scene component generator over the source, emits the result and loads it, running
    // its module initializer, which registers the codecs, into a context that can be let go.
    private static Assembly CompileAndLoad()
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(App).Assembly.Location));
        var compilation = CSharpCompilation.Create($"Renamed{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(Source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver.Create(new SceneComponentGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        emitted.Success.Should().BeTrue(string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        image.Position = 0;
        var assembly = new AssemblyLoadContext(compilation.AssemblyName, isCollectible: true).LoadFromStream(image);
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        return assembly;
    }
}

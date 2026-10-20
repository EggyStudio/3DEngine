using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Engine.Tests.Scenes;

/// <summary>
/// Scene files written before a component or a field was renamed, or under an earlier or a later
/// version of a component's layout, read through the codecs the generator writes for components
/// marked with their former names and versions, compiled and loaded as a program's own would be.
/// </summary>
[Trait("Category", "Integration")]
public class SceneMigrationTests
{
    private const string Source = """
        using System.Text.Json.Nodes;
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

        // Saved as one array of what is left and the most at version 1, as two fields at 2, and
        // with the most counted ten times finer at 3.
        [SceneComponent, DataVersion(3)]
        public struct Health
        {
            public int Current;
            public int Most;

            public static JsonObject Migrate(int from, JsonObject value)
            {
                if (from < 2)
                {
                    var life = value["Life"]!.AsArray();
                    value.Remove("Life");
                    value["Current"] = (int)life[0]!;
                    value["Most"] = (int)life[1]!;
                }
                if (from < 3) value["Most"] = (int)value["Most"]! * 10;
                return value;
            }
        }

        public static class Probe
        {
            public static string Read(EcsWorld ecs, int entity) =>
                ecs.Has<Coffer>(entity) ? $"Coffer {ecs.GetRef<Coffer>(entity).Coins} {ecs.GetRef<Coffer>(entity).Opened}"
                : ecs.Has<Keg>(entity) ? $"Keg {ecs.GetRef<Keg>(entity).Litres}"
                : ecs.Has<Lantern>(entity) ? $"Lantern {ecs.GetRef<Lantern>(entity).Glow}"
                : ecs.Has<Lamp>(entity) ? $"Lamp {ecs.GetRef<Lamp>(entity).Watts}"
                : ecs.Has<Health>(entity) ? $"Health {ecs.GetRef<Health>(entity).Current} {ecs.GetRef<Health>(entity).Most}"
                : "none";

            public static void Hurt(EcsWorld ecs, int entity) => ecs.GetRef<Health>(entity).Current -= 1;
        }
        """;

    private static readonly Lazy<Type> Probe = new(() => CompileAndLoad().GetType("Renamed.Probe")!);

    // A scene file of one entity holding one component as given, read into a world of its own, and
    // the component it read as the probe says it, with the file saved from the world after the
    // probe's method of the name given, if any, has changed it.
    private static (string Read, string Saved) Load(string component, string? change = null)
    {
        var json = $$"""{ "format": "3dengine-scene", "version": 1, "entities": [ { "id": "a", "components": { {{component}} } } ] }""";
        var world = new World();
        world.InsertResource(new EcsWorld());
        var probe = Probe.Value;
        var spawned = SceneFile.Read(world, json);
        var ecs = world.Resource<EcsWorld>();
        var read = (string)probe.GetMethod("Read")!.Invoke(null, [ecs, spawned[0]])!;
        if (change is not null) probe.GetMethod(change)!.Invoke(null, [ecs, spawned[0]]);
        return (read, SceneFile.Write(ecs));
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

    [Fact]
    public void A_Component_Saved_At_An_Earlier_Version_Is_Brought_Through_Each_Change_Since()
    {
        var (read, saved) = Load("\"Health\": { \"Life\": [3, 9] }");
        read.Should().Be("Health 3 90", "a file with no version is version 1, split at 2 and scaled at 3");
        saved.Should().Contain("\"$version\": 3").And.Contain("\"Most\": 90").And.NotContain("Life");

        Load("\"Health\": { \"$version\": 2, \"Current\": 3, \"Most\": 9 }").Read.Should().Be("Health 3 90");
        Load("\"Health\": { \"$version\": 3, \"Current\": 3, \"Most\": 9 }").Read.Should().Be("Health 3 9");
    }

    [Fact]
    public void A_Component_Saved_At_A_Later_Version_Is_Saved_Back_At_It_With_What_This_Build_Does_Not_Know()
    {
        var (read, saved) = Load("\"Health\": { \"$version\": 4, \"Current\": 2, \"Most\": 50, \"Shield\": 7 }", "Hurt");

        read.Should().Be("Health 2 50", "the fields this build knows are read as the file holds them");
        saved.Should().Contain("\"$version\": 4").And.Contain("\"Shield\": 7")
            .And.Contain("\"Current\": 1", "what this build changed is saved").And.NotContain("\"$version\": 3");
    }

    [Fact]
    public void A_Migration_That_Fails_Names_The_Type_And_The_Version_It_Moved_From()
    {
        var load = () => Load("\"Health\": { \"Current\": 3 }");

        load.Should().Throw<InvalidDataException>().WithMessage("Renamed.Health's migration from version 1 failed*");
    }

    [Fact]
    public void A_Versioned_Component_Without_A_Migration_Is_Warned_Of_And_Still_Saved_With_Its_Version()
    {
        var compilation = CSharpCompilation.Create("Unmigrated",
            [CSharpSyntaxTree.ParseText("using Engine; [SceneComponent, DataVersion(2)] public struct Armour { public int Points; }")],
            References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver.Create(new SceneComponentGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        diagnostics.Should().ContainSingle().Which.Should().Match<Diagnostic>(d => d.Id == "E3D200" && d.Severity == DiagnosticSeverity.Warning);
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var generated = output.SyntaxTrees.Last().ToString();
        generated.Should().Contain("WriteNumber(global::Engine.SceneJson.VersionName, 2)").And.NotContain("Migrated");
    }

    private static IEnumerable<MetadataReference> References =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .Append(MetadataReference.CreateFromFile(typeof(App).Assembly.Location));

    // Runs the scene component generator over the source, emits the result and loads it, running
    // its module initializer, which registers the codecs, into a context that can be let go.
    private static Assembly CompileAndLoad()
    {
        var compilation = CSharpCompilation.Create($"Renamed{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(Source)], References,
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

using System.Numerics;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Engine.Tests.Scenes;

[Trait("Category", "Unit")]
public class SceneFileTests
{
    private static World NewWorld()
    {
        var world = new World();
        world.InsertResource(new EcsWorld());
        return world;
    }

    [Fact]
    public void A_Level_Comes_Back_With_Its_Names_Hierarchy_And_Components()
    {
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var rig = ecs.Spawn();
        ecs.SetName(rig, "Rig");
        ecs.Add(rig, new Transform(new Vector3(1, 2, 3)));
        var eye = ecs.Spawn();
        ecs.SetName(eye, "Eye");
        ecs.Add(eye, new Transform(new Vector3(0, 1, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f), Vector3.One));
        ecs.Add(eye, new Camera(75f, 0.5f, 200f));
        ecs.SetParent(eye, rig);
        var lamp = ecs.Spawn();
        ecs.Add(lamp, Light.Spot(new Vector3(1, 0.5f, 0.25f), 4, innerAngle: 15, outerAngle: 35, range: 12));
        ecs.Add(lamp, new Material(new Vector4(0.1f, 0.2f, 0.3f, 1)));

        var json = SceneFile.Write(ecs);
        var loaded = NewWorld();
        var spawned = SceneFile.Read(loaded, json);

        var back = loaded.Resource<EcsWorld>();
        spawned.Should().HaveCount(3);
        var rigBack = back.FindByName("Rig");
        var eyeBack = back.FindByName("Eye");
        back.ParentOf(eyeBack).Should().Be(rigBack);
        back.GetRef<Transform>(rigBack).Position.Should().Be(new Vector3(1, 2, 3));
        back.GetRef<Transform>(eyeBack).Rotation.Should().Be(Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f));
        back.GetRef<Camera>(eyeBack).FovY.Should().BeApproximately(float.DegreesToRadians(75), 1e-6f);
        back.GetRef<Camera>(eyeBack).Far.Should().Be(200);

        var lampBack = spawned.Single(e => back.Has<Light>(e));
        back.GetRef<Light>(lampBack).Kind.Should().Be(LightKind.Spot);
        back.GetRef<Light>(lampBack).Color.Should().Be(new Vector3(1, 0.5f, 0.25f));
        (back.GetRef<Light>(lampBack).InnerAngle, back.GetRef<Light>(lampBack).OuterAngle, back.GetRef<Light>(lampBack).Range).Should().Be((15f, 35f, 12f));
        back.GetRef<Material>(lampBack).Albedo.Should().Be(new Vector4(0.1f, 0.2f, 0.3f, 1));
        back.GetRef<SceneId>(eyeBack).Value.Should().Be(ecs.GetRef<SceneId>(eye).Value, "an entity keeps its id");
    }

    [Fact]
    public void Entities_Spawned_From_A_Model_Are_Left_For_Their_Model_To_Bring_Back()
    {
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var crate = ecs.Spawn();
        ecs.Add(crate, new ModelRef { Path = "models/crate.glb" });
        var part = ecs.Spawn();
        ecs.Add(part, new SceneInstance { SourcePath = "models/crate.glb" });
        ecs.SetParent(part, crate);

        var loaded = NewWorld();
        var spawned = SceneFile.Read(loaded, SceneFile.Write(ecs));

        spawned.Should().ContainSingle();
        loaded.Resource<EcsWorld>().GetRef<ModelRef>(spawned[0]).Path.Should().Be("models/crate.glb");
    }

    [Fact]
    public void A_Missing_Field_Takes_The_Component_Default_And_An_Unknown_Component_Is_Skipped()
    {
        const string json = """
            { "format": "3dengine-scene", "version": 1, "entities": [
              { "id": "a", "components": { "Transform": { "Position": [5, 0, 0] }, "NoSuchComponent": { } } } ] }
            """;
        var world = NewWorld();

        var spawned = SceneFile.Read(world, json);

        var transform = world.Resource<EcsWorld>().GetRef<Transform>(spawned[0]);
        transform.Position.Should().Be(new Vector3(5, 0, 0));
        transform.Scale.Should().Be(Vector3.One, "Transform starts from Transform.Identity");
        transform.Rotation.Should().Be(Quaternion.Identity);
    }

    [Fact]
    public void A_File_That_Is_Not_A_Scene_Is_Refused()
    {
        var read = () => SceneFile.Read(NewWorld(), """{ "entities": [] }""");
        read.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void The_Generator_Writes_Codecs_For_Every_Field_Kind_It_Holds()
    {
        var source = """
            using System.Numerics;
            using Engine;
            namespace Game;
            public enum Mood { Calm, Angry }
            [SceneComponent]
            public struct Everything
            {
                public bool Flag; public int Count; public float Speed; public double Exact; public string Label;
                public Mood Mood; public Vector2 V2; public Vector3 V3; public Vector4 V4; public Quaternion Q;
                public Matrix4x4 M; public Color Tint; public Entity Target; public Handle<Texture> Skin;
                public float? Maybe; public Vector3? Somewhere; public int[] Skipped; public readonly int Fixed;
            }
            [Behavior]
            public struct Wanderer { public float Heading; [OnUpdate] public void Step(BehaviorContext ctx) { } }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(App).Assembly.Location));
        var compilation = CSharpCompilation.Create("Scenes", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver.Create(new SceneComponentGenerator(), new BehaviorGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        diagnostics.Should().BeEmpty();
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var generated = output.SyntaxTrees.Last(t => t.ToString().Contains("SceneComponentRegistration")).ToString();
        generated.Should().Contain("\"Everything\"").And.Contain("\"Wanderer\"").And.Contain("ctx.IdOf(v.Target)")
            .And.Contain("ctx.Load<global::Engine.Texture>").And.Contain("Enum.Parse<global::Game.Mood>")
            .And.NotContain("Skipped").And.NotContain("Fixed");
    }

    public static class Left { public struct Twin { public int Value; } }

    public static class Right { public struct Twin { public float Value; } }

    [Fact]
    public void Two_Components_With_One_Name_Are_Written_By_Full_Name_And_Read_Back_As_Themselves()
    {
        SceneComponents.Add(new SceneCodec<Left.Twin>("Twin",
            static (System.Text.Json.Utf8JsonWriter w, in Left.Twin v, SceneWriteContext _) => w.WriteNumber("Value", v.Value),
            static (e, _) => new Left.Twin { Value = e.GetProperty("Value").GetInt32() }));
        SceneComponents.Add(new SceneCodec<Right.Twin>("Twin",
            static (System.Text.Json.Utf8JsonWriter w, in Right.Twin v, SceneWriteContext _) => w.WriteNumber("Value", v.Value),
            static (e, _) => new Right.Twin { Value = e.GetProperty("Value").GetSingle() }));
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var entity = ecs.Spawn();
        ecs.Add(entity, new Left.Twin { Value = 3 });
        ecs.Add(entity, new Right.Twin { Value = 0.5f });

        var json = SceneFile.Write(ecs, [entity]);
        var loaded = NewWorld();
        var back = SceneFile.Read(loaded, json).Single();

        json.Should().Contain(typeof(Left.Twin).FullName!).And.Contain(typeof(Right.Twin).FullName!).And.NotContain("\"Twin\":");
        loaded.Resource<EcsWorld>().GetRef<Left.Twin>(back).Value.Should().Be(3);
        loaded.Resource<EcsWorld>().GetRef<Right.Twin>(back).Value.Should().Be(0.5f);
        SceneComponents.Find("Twin").Should().BeNull("a name two types share names neither");
    }

    public static class Game { public struct Light { public int Lumens; } }

    [Fact]
    public void An_Engine_Component_Keeps_Its_Name_When_A_Game_Type_Shares_It()
    {
        // A level saved before the game had a Light of its own.
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var lamp = ecs.Spawn();
        ecs.Add(lamp, Light.Point(Vector3.One, 2f));
        var before = SceneFile.Write(ecs, [lamp]);

        SceneComponents.Add(new SceneCodec<Game.Light>("Light",
            static (System.Text.Json.Utf8JsonWriter w, in Game.Light v, SceneWriteContext _) => w.WriteNumber("Lumens", v.Lumens),
            static (e, _) => new Game.Light { Lumens = e.GetProperty("Lumens").GetInt32() }));
        ecs.Add(lamp, new Game.Light { Lumens = 800 });
        var after = SceneFile.Write(ecs, [lamp]);

        var loaded = NewWorld();
        var back = SceneFile.Read(loaded, before).Single();
        loaded.Resource<EcsWorld>().Has<Light>(back).Should().BeTrue("the older file's Light still means the engine's");

        after.Should().Contain("\"Light\":").And.Contain(typeof(Game.Light).FullName!);
        var again = NewWorld();
        var backAgain = SceneFile.Read(again, after).Single();
        again.Resource<EcsWorld>().GetRef<Light>(backAgain).Intensity.Should().Be(2f);
        again.Resource<EcsWorld>().GetRef<Game.Light>(backAgain).Lumens.Should().Be(800);
        SceneComponents.Find("Light")!.Type.Should().Be(typeof(Light));
    }
}

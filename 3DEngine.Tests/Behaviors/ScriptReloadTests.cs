using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using FluentAssertions;

namespace Engine.Tests.Behaviors;

/// <summary>A script changed while its game runs, which finds the game where it was.</summary>
// With the other tests whose app has Dear ImGui, whose one context for the process two apps side
// by side would both ask for.
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class ScriptReloadTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-script-reload-");

    public void Dispose() => _folder.Dispose();

    // A counter behavior on an entity it spawns, which keeps the entity, a mood, a history and
    // steps of a type of its own, beside a resource of the script's own. The second version counts
    // in tens, has its mood's values in another order and a field of its own more, and declares no
    // Leftover, which the first puts on the entity too.
    private static string Script(int step, bool second) => $$"""
        using System.Collections.Generic;
        using Engine;

        public enum Mood { {{(second ? "Angry, Calm" : "Calm, Angry")}} }
        public struct Step { public int Size; }
        public sealed class Score { public int Points; {{(second ? "public string Note = \"new\";" : "")}} }
        {{(second ? "" : "public struct Leftover { public int Value; }")}}

        [Behavior]
        public struct Counter
        {
            public int Ticks;
            public Entity Target;
            public Mood Mood;
            public int[] History;
            public List<Step> Steps;
            {{(second ? "public float Added;" : "")}}

            [OnStartup]
            public static void Spawn(BehaviorContext ctx)
            {
                var e = ctx.Ecs.Spawn();
                ctx.Ecs.Add(e, new Counter { Target = ctx.Ecs.Handle(e), Mood = Mood.Calm, History = [1, 2], Steps = [new Step { Size = 3 }] });
                {{(second ? "" : "ctx.Ecs.Add(e, new Leftover { Value = 9 });")}}
                ctx.World.InsertResource(new Score { Points = 42 });
            }

            [OnUpdate]
            public void Tick(BehaviorContext ctx) => Ticks += {{step}};
        }
        """;

    // The value of a field of a component or resource of a type the scripts declare.
    private static object? Field(object value, string name) =>
        value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public)!.GetValue(value);

    [Fact]
    public void A_Script_Compiled_Again_Mid_Game_Finds_Its_Entities_And_Resources_Where_They_Were()
    {
        File.WriteAllText(_folder.File("Counter.cs"), Script(1, second: false));
        var app = new App(Config.Default with { Headless = true });
        app.AddPlugin(new BehaviorsPlugin { ScriptsDirectory = _folder.Path }).AddPlugin(new DefaultPlugins());
        try
        {
            for (int frame = 0; frame < 5; frame++)
            {
                app.BeginFrame();
                app.EndFrame();
            }
            var ecs = app.World.Resource<EcsWorld>();
            var (first, entity) = Counted(ecs);

            File.WriteAllText(_folder.File("Counter.cs"), Script(10, second: true));
            app.World.Resource<RuntimeBehaviorCompiler>().Recompile().Success.Should().BeTrue();
            app.BeginFrame();
            app.EndFrame();

            var counterType = ecs.ComponentTypes.Single(t => t.Name == "Counter");
            ecs.EntitiesOf(counterType).Should().Equal([entity], "the entity keeps its counter, now of the new generation's type");
            var counter = ecs.GetBoxed(entity, counterType)!;
            Field(counter, "Ticks").Should().Be(5 + 10, "five frames counted one, and the frame after the reload counted ten");
            Field(counter, "Target").Should().Be(ecs.Handle(entity), "the entity it keeps is the same");
            Field(counter, "Mood")!.ToString().Should().Be("Calm", "an enum is carried by the name of its value");
            ((int[])Field(counter, "History")!).Should().Equal(1, 2);
            var steps = (System.Collections.IList)Field(counter, "Steps")!;
            Field(steps[0]!, "Size").Should().Be(3, "a list of the script's own type is made again element by element");
            Field(counter, "Added").Should().Be(0f, "a field the new generation adds starts at its default");
            ecs.ComponentTypes.Should().NotContain(t => t.Name == "Leftover", "a component whose type the new scripts no longer declare is dropped");

            var score = app.World.ResourceTypes.Single(t => t.Name == "Score");
            var resource = app.World.ResourceOf(score)!;
            Field(resource, "Points").Should().Be(42, "the script's resource keeps its value");
            Field(resource, "Note").Should().Be("new", "a field the new generation adds is as its constructor left it");

            for (int i = 0; i < 20 && first.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            first.IsAlive.Should().BeFalse("nothing of the world keeps the first generation's types once its components are carried");
        }
        finally
        {
            app.Shutdown();
        }
    }

    // The load context of the first generation's counter, held weakly, and the entity counting,
    // from a method of its own so no local of the caller's keeps the generation.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Generation, int Entity) Counted(EcsWorld ecs)
    {
        var type = ecs.ComponentTypes.Single(t => t.Name == "Counter");
        return (new WeakReference(AssemblyLoadContext.GetLoadContext(type.Assembly)), ecs.EntitiesOf(type).Single());
    }
}

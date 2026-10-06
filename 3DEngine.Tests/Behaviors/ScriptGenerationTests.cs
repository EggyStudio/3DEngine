using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using FluentAssertions;

namespace Engine.Tests.Behaviors;

/// <summary>A generation of a script compiled while the app runs, let go once the script is compiled again.</summary>
// With the other tests whose app has Dear ImGui, whose one context for the process two apps
// side by side would both ask for.
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class ScriptGenerationTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-script-generations-");

    public void Dispose() => _folder.Dispose();

    // A behavior whose system throws a type of the script's own in every frame, or does nothing.
    private static string Script(string exception, bool throws) => $$"""
        using Engine;

        public sealed class {{exception}}(string message) : System.Exception(message);

        [Behavior]
        public struct Thrower
        {
            [OnUpdate]
            public static void Run(BehaviorContext ctx) { {{(throws ? $"throw new {exception}(\"a script's own\");" : "")}} }
        }
        """;

    [Fact]
    [ExpectsError("Engine.Schedule", "'Thrower_Generated_Update_Run'")]
    public void A_Generation_Whose_System_Threw_A_Type_Of_Its_Own_Is_Unloaded_When_The_Script_Is_Compiled_Again()
    {
        // A name of its own, by which this test's generation is found among every load context.
        var exception = $"ScriptBroke{Guid.NewGuid():N}";
        File.WriteAllText(_folder.File("Thrower.cs"), Script(exception, throws: true));
        var app = new App(Config.Default with { Headless = true });
        app.AddPlugin(new BehaviorsPlugin { ScriptsDirectory = _folder.Path }).AddPlugin(new DefaultPlugins());
        try
        {
            for (int frame = 0; frame < 20; frame++)
            {
                app.BeginFrame();
                app.EndFrame();
            }
            var first = Generation(exception);
            first.IsAlive.Should().BeTrue("the script was compiled and its system ran");
            GeneratedBehaviors.All.Should().NotContain(register => register.Method.Module.Assembly.IsCollectible,
                "the script's compiler registers it into its own app, and the process's list would keep it for every app after");
            var bare = new App();
            new EcsPlugin().Build(bare);
            bare.Schedule.Systems().Should().NotContain(system => system.System.Method.Module.Assembly.IsCollectible,
                "an app made after, bare with no Time, takes no system of the script the first app compiled");
            bare.Shutdown();

            File.WriteAllText(_folder.File("Thrower.cs"), Script(exception + "Again", throws: false));
            app.World.Resource<RuntimeBehaviorCompiler>().Recompile().Success.Should().BeTrue();
            app.BeginFrame();
            app.EndFrame();

            for (int i = 0; i < 20 && first.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            first.IsAlive.Should().BeFalse("nothing of the app holds the first generation's types, the one its system threw among them");
        }
        finally
        {
            app.Shutdown();
        }
    }

    // The load context holding the assembly that defines the type, held weakly, from a method of
    // its own so no local of the caller's keeps it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Generation(string type) =>
        new(AssemblyLoadContext.All.Single(context => context.Assemblies.Any(assembly => assembly.GetType(type) is not null)));
}

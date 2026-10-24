using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Engine.Tests.Behaviors;

/// <summary>
/// Every attribute the source generators read, compiled through them, loaded and run in an app for
/// a few frames, each asserted to run when it should and not when it should not.
/// </summary>
/// <remarks>
/// The attributes come from the generators' own tables, and each needs a case below, so one added
/// to a generator without a test fails here. A behavior whose generated code stopped compiling, as
/// <c>[Changed]</c>'s did once with nothing in the repository using it, fails here too. Its app
/// is built with the default plugins, whose ImGui context is one for the process, so it runs in the
/// collection of the other tests that build one, never beside them.
/// </remarks>
[Collection("E3D")]
[Trait("Category", "Integration")]
public class GeneratorAttributeTests
{
    // One source with a use of every attribute. Each method counts its runs in Probe, and only in
    // the app this test made.
    private const string Source = """
        using Engine;

        namespace GeneratorProbe;

        public enum Mode { A, B }

        [SubStateOf(Mode.B)] public enum Sub { One, Two }

        public enum Derived { InB }

        public static class Derive
        {
            [ComputedState] public static Derived? FromMode(Mode mode) => mode == Mode.B ? Derived.InB : null;
        }
        public struct Health { public int Value; }
        public struct Tag;

        public static class Probe
        {
            public static World? World;
            public static bool Allowed;
            public static int MainThreadId;
            public static readonly System.Collections.Generic.Dictionary<string, int> Runs = new();
            public static readonly System.Collections.Generic.List<string> Order = new();

            public static void Ran(BehaviorContext ctx, string name)
            {
                if (!ReferenceEquals(ctx.World, World)) return;
                lock (Runs) Runs[name] = Runs.TryGetValue(name, out var n) ? n + 1 : 1;
            }

            public static void Ordered(BehaviorContext ctx, string name)
            {
                if (!ReferenceEquals(ctx.World, World)) return;
                lock (Order) Order.Add(name);
            }
        }

        // Three systems added A, B and C, whose attributes order them C, B, A, A after B by its
        // type and method, and C before B by its system's name.
        [Behavior]
        public struct OrderA
        {
            [OnUpdate, After(typeof(OrderB), nameof(OrderB.Run))] public static void Run(BehaviorContext ctx) => Probe.Ordered(ctx, "A");
        }

        [Behavior]
        public struct OrderB
        {
            [OnUpdate] public static void Run(BehaviorContext ctx) => Probe.Ordered(ctx, "B");
        }

        [Behavior]
        public struct OrderC
        {
            [OnUpdate, Before("OrderB_Generated_Update_Run")] public static void Run(BehaviorContext ctx) => Probe.Ordered(ctx, "C");
        }

        [Behavior]
        public struct Stages
        {
            public static bool Allow => Probe.Allowed;

            [OnStartup] public static void Startup(BehaviorContext ctx) => Probe.Ran(ctx, "Startup");
            [OnFirst] public static void First(BehaviorContext ctx) => Probe.Ran(ctx, "First");
            [OnPreUpdate] public static void PreUpdate(BehaviorContext ctx) => Probe.Ran(ctx, "PreUpdate");
            [OnFixedUpdate] public static void FixedUpdate(BehaviorContext ctx) => Probe.Ran(ctx, "FixedUpdate");
            [OnUpdate] public static void Update(BehaviorContext ctx) => Probe.Ran(ctx, "Update");
            [OnPostUpdate] public static void PostUpdate(BehaviorContext ctx) => Probe.Ran(ctx, "PostUpdate");
            [OnRender] public static void Render(BehaviorContext ctx) => Probe.Ran(ctx, "Render");
            [OnLast] public static void Last(BehaviorContext ctx) => Probe.Ran(ctx, "Last");
            [OnCleanup] public static void Cleanup(BehaviorContext ctx) => Probe.Ran(ctx, "Cleanup");
            [OnEnter(Mode.B)] public static void Enter(BehaviorContext ctx) => Probe.Ran(ctx, "Enter");
            [OnExit(Mode.B)] public static void Exit(BehaviorContext ctx) => Probe.Ran(ctx, "Exit");
            [OnTransition(Mode.A, Mode.B)] public static void AToB(BehaviorContext ctx) => Probe.Ran(ctx, "Transition");
            [OnEnter(Sub.One)] public static void SubEntered(BehaviorContext ctx) => Probe.Ran(ctx, "SubState");
            [OnEnter(Derived.InB)] public static void DerivedEntered(BehaviorContext ctx) => Probe.Ran(ctx, "ComputedState");
            [OnUpdate, InState(Mode.B)] public static void InB(BehaviorContext ctx) => Probe.Ran(ctx, "InState");
            [OnUpdate, InState(Mode.B), InState(Sub.Two)] public static void InBoth(BehaviorContext ctx) => Probe.Ran(ctx, "InBoth");
            [OnUpdate, RunIf(nameof(Allow))] public static void Allowed(BehaviorContext ctx) => Probe.Ran(ctx, "RunIf");
            [OnUpdate, ToggleKey(Key.T)] public static void Toggled(BehaviorContext ctx) => Probe.Ran(ctx, "ToggleKey");
        }

        [Behavior]
        public struct Filtered
        {
            [OnUpdate, With(typeof(Tag))] public void Tagged(BehaviorContext ctx) => Probe.Ran(ctx, "With");
            [OnUpdate, Without(typeof(Tag))] public void Untagged(BehaviorContext ctx) => Probe.Ran(ctx, "Without");
            [OnUpdate, Changed(typeof(Health))] public readonly void Hurt(BehaviorContext ctx) => Probe.Ran(ctx, "Changed");
            [OnUpdate, Added(typeof(Health))] public readonly void Born(BehaviorContext ctx) => Probe.Ran(ctx, "Added");
            [OnUpdate, MainThread] public readonly void OnMain(BehaviorContext ctx)
            {
                if (System.Environment.CurrentManagedThreadId == Probe.MainThreadId) Probe.Ran(ctx, "MainThread");
            }
            [OnUpdate] public static void Rule(BehaviorContext ctx)
            {
                if (System.Environment.CurrentManagedThreadId == Probe.MainThreadId) Probe.Ran(ctx, "StaticOnMain");
            }
        }

        // The entity's other components as parameters, ref to write one, which marks it changed,
        // and in to read one, which does not. A parameter is also a filter.
        public struct Velocity { public float X; }

        [Behavior]
        public struct Mover
        {
            [OnUpdate]
            public readonly void Move(BehaviorContext ctx, ref Transform transform, in Velocity velocity)
            {
                transform.Position.X += velocity.X;
                Probe.Ran(ctx, "RefParameter");
            }

            [OnUpdate] public readonly void Look(BehaviorContext ctx, in Health health) => Probe.Ran(ctx, "InParameter");
            [OnPostUpdate, Changed(typeof(Transform))] public readonly void Moved(BehaviorContext ctx) => Probe.Ran(ctx, "TransformChanged");
        }

        public static class Commands
        {
            [Command("probe.ping", "Answers pong")] public static string Ping() => "pong";
            // Defaults with no literal of their own, which the generated registration once printed as names.
            [Command("probe.far", "Answers its numbers")]
            public static string Far(float at = float.NaN, double to = double.PositiveInfinity, float from = float.NegativeInfinity) => $"{at} {to} {from}";
            public static string NotACommand() => "never";
        }

        [SceneComponent] public struct Marker { public int Value; }
        [DataAsset] public struct Stats { public int Value; }
        public struct Unmarked { public int Value; }
        [SceneComponent, FormerName("Formerly")] public struct Renamed { public int Value; }
        [SceneComponent, DataVersion(2)]
        public struct Versioned
        {
            public int Value;
            public static System.Text.Json.Nodes.JsonObject Migrate(int from, System.Text.Json.Nodes.JsonObject value) => value;
        }
        """;

    // What each attribute's case checks, by the attribute's full name. A name the generators read
    // that is missing here fails the coverage test.
    private static readonly Dictionary<string, string> Cases = new()
    {
        ["Engine.BehaviorAttribute"] = "the behaviors register at all",
        ["Engine.OnStartupAttribute"] = "Startup",
        ["Engine.OnFirstAttribute"] = "First",
        ["Engine.OnPreUpdateAttribute"] = "PreUpdate",
        ["Engine.OnFixedUpdateAttribute"] = "FixedUpdate",
        ["Engine.OnUpdateAttribute"] = "Update",
        ["Engine.OnPostUpdateAttribute"] = "PostUpdate",
        ["Engine.OnRenderAttribute"] = "Render",
        ["Engine.OnLastAttribute"] = "Last",
        ["Engine.OnCleanupAttribute"] = "Cleanup",
        ["Engine.OnEnterAttribute"] = "Enter",
        ["Engine.OnExitAttribute"] = "Exit",
        ["Engine.OnTransitionAttribute"] = "Transition",
        ["Engine.SubStateOfAttribute"] = "SubState",
        ["Engine.ComputedStateAttribute"] = "ComputedState",
        ["Engine.InStateAttribute"] = "InState",
        ["Engine.AfterAttribute"] = "OrderA",
        ["Engine.BeforeAttribute"] = "OrderC",
        ["Engine.WithAttribute"] = "With",
        ["Engine.WithoutAttribute"] = "Without",
        ["Engine.ChangedAttribute"] = "Changed",
        ["Engine.AddedAttribute"] = "Added",
        ["Engine.RunIfAttribute"] = "RunIf",
        ["Engine.ToggleKeyAttribute"] = "ToggleKey",
        ["Engine.MainThreadAttribute"] = "MainThread",
        ["Engine.CommandAttribute"] = "probe.ping",
        ["Engine.SceneComponentAttribute"] = "Marker",
        ["Engine.FormerNameAttribute"] = "Formerly",
        ["Engine.DataVersionAttribute"] = "Versioned",
        ["Engine.DataAssetAttribute"] = "Stats",
    };

    private static IEnumerable<string> GeneratorAttributes =>
        BehaviorGenerator.Attributes.Concat(CommandGenerator.Attributes).Concat(SceneComponentGenerator.Attributes).Distinct();

    [Fact]
    public void Every_Attribute_A_Generator_Reads_Has_A_Case()
    {
        string.Join(", ", GeneratorAttributes.Where(a => !Cases.ContainsKey(a)))
            .Should().BeEmpty("an attribute the generators read needs a case in this test");
    }

    [Fact]
    public void Every_Attribute_The_Engine_Declares_Is_Read_By_A_Generator_Or_Emitted_By_One()
    {
        // Emitted onto generated code rather than read from a program's.
        string[] emitted = ["Engine.GeneratedBehaviorRegistrationAttribute"];
        var declared = typeof(App).Assembly.GetTypes()
            .Where(t => t.IsPublic && typeof(Attribute).IsAssignableFrom(t) && t.Namespace == "Engine")
            .Select(t => t.FullName!);

        string.Join(", ", declared.Except(GeneratorAttributes).Except(emitted).Order())
            .Should().BeEmpty("an attribute in the engine is read by a generator, and so has a case here");
    }

    [Fact]
    public void Every_Attribute_Runs_When_It_Should_And_Not_Otherwise()
    {
        var app = new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins());
        var assembly = CompileAndLoad();
        var probe = assembly.GetType("GeneratorProbe.Probe")!;
        probe.GetField("World")!.SetValue(null, app.World);
        probe.GetField("MainThreadId")!.SetValue(null, Environment.CurrentManagedThreadId);
        var runs = (Dictionary<string, int>)probe.GetField("Runs")!.GetValue(null)!;
        int Runs(string name) { lock (runs) return runs.GetValueOrDefault(name); }

        // The state, then the behaviors, as a program's registrations would add them.
        var mode = assembly.GetType("GeneratorProbe.Mode")!;
        typeof(App).GetMethod(nameof(App.AddState))!.MakeGenericMethod(mode).Invoke(app, [Enum.ToObject(mode, 0)]);
        var register = assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Single(m => m.GetCustomAttribute<GeneratedBehaviorRegistrationAttribute>() is not null);
        register.Invoke(null, [app]);

        // One entity tagged and one not, each with the filtered behavior and health.
        var ecs = app.World.Resource<EcsWorld>();
        var add = typeof(EcsWorld).GetMethods().Single(m => m.Name == nameof(EcsWorld.Add) && m.GetParameters()[0].ParameterType == typeof(int));
        void Give(int entity, string type) =>
            add.MakeGenericMethod(assembly.GetType($"GeneratorProbe.{type}")!).Invoke(ecs, [entity, Activator.CreateInstance(assembly.GetType($"GeneratorProbe.{type}")!)]);
        var tagged = ecs.Spawn();
        foreach (var type in new[] { "Filtered", "Tag", "Health" }) Give(tagged, type);
        var untagged = ecs.Spawn();
        foreach (var type in new[] { "Filtered" }) Give(untagged, type);

        // One mover with a velocity and one without, which its Move skips, and the tagged entity's
        // health read through Look.
        var velocity = assembly.GetType("GeneratorProbe.Velocity")!;
        var moving = ecs.Spawn();
        Give(moving, "Mover");
        ecs.Add(moving, new Transform(System.Numerics.Vector3.Zero));
        var oneAStep = Activator.CreateInstance(velocity)!;
        velocity.GetField("X")!.SetValue(oneAStep, 1f);
        add.MakeGenericMethod(velocity).Invoke(ecs, [moving, oneAStep]);
        var still = ecs.Spawn();
        Give(still, "Mover");
        ecs.Add(still, new Transform(System.Numerics.Vector3.Zero));
        Give(tagged, "Mover");

        var fixedTime = app.World.Resource<FixedTime>();
        void Frame()
        {
            fixedTime.Accumulate(fixedTime.StepSeconds);
            app.BeginFrame();
            app.EndFrame();
        }

        Frame();
        foreach (var stage in new[] { "Startup", "First", "PreUpdate", "FixedUpdate", "Update", "PostUpdate", "Render", "Last" })
            Runs(stage).Should().BeGreaterThan(0, $"[On{stage}] runs in its stage");
        Runs("Cleanup").Should().Be(0, "[OnCleanup] waits for the app to shut down");
        Runs("With").Should().Be(1, "[With] visits the tagged entity alone");
        Runs("Without").Should().Be(1, "[Without] visits the untagged entity alone");
        Runs("Added").Should().Be(1, "[Added] sees the health the tagged entity got before the first run");
        Runs("Changed").Should().Be(0, "[Changed] sees nothing, since adding a component is not changing it");
        Runs("RunIf").Should().Be(0, "[RunIf] waits for its condition");
        Runs("InState").Should().Be(0, "[InState] waits for its state");
        Runs("Enter").Should().Be(0);
        Runs("ToggleKey").Should().Be(1, "[ToggleKey] runs until its key is pressed");
        Runs("MainThread").Should().Be(2, "[MainThread] visits both entities with the behavior on the main thread");
        Runs("StaticOnMain").Should().Be(1, "a static method runs on the main thread");
        Runs("RefParameter").Should().Be(1, "a method taking components runs only for the entity that has them");
        Runs("InParameter").Should().Be(1, "and Look only for the tagged entity, which has health");
        ecs.GetReadOnly<Transform>(moving).Position.X.Should().Be(1, "the transform taken by ref is the entity's own");
        Runs("TransformChanged").Should().Be(1, "a component written through a ref parameter is seen as changed, and the still mover's is not");

        // Allow the condition, move to state B, press the toggle key and hurt the tagged entity.
        probe.GetField("Allowed")!.SetValue(null, true);
        var next = app.World.GetType().GetMethod(nameof(World.Resource))!.MakeGenericMethod(typeof(NextState<>).MakeGenericType(mode)).Invoke(app.World, null)!;
        next.GetType().GetMethod("Set")!.Invoke(next, [Enum.ToObject(mode, 1)]);
        var input = app.World.Resource<Input>();
        input.SetKey(Key.T, true);
        var health = assembly.GetType("GeneratorProbe.Health")!;
        var update = typeof(EcsWorld).GetMethods().Single(m => m.Name == nameof(EcsWorld.Update) && m.GetParameters()[0].ParameterType == typeof(int));
        update.MakeGenericMethod(health).Invoke(ecs, [tagged, Activator.CreateInstance(health)]);
        Frame();
        input.SetKey(Key.T, false);

        Runs("RunIf").Should().Be(1, "[RunIf] runs once its condition holds");
        Runs("Enter").Should().Be(1, "[OnEnter] runs on entering its state");
        Runs("Transition").Should().Be(1, "[OnTransition] runs on the move from A to B");
        Runs("SubState").Should().Be(1, "a [SubStateOf] enum comes into being, at its first value, when its parent enters B");
        Runs("ComputedState").Should().Be(1, "a [ComputedState] method's state is entered when its source moves to B");
        Runs("InState").Should().Be(1, "[InState] runs in its state");
        Runs("ToggleKey").Should().Be(1, "pressing the key turned it off");
        Runs("Changed").Should().Be(1, "[Changed] sees the health updated since it last ran");
        Runs("Added").Should().Be(1, "[Added] saw the health once");
        Runs("Exit").Should().Be(0);

        next.GetType().GetMethod("Set")!.Invoke(next, [Enum.ToObject(mode, 0)]);
        Frame();
        Runs("Exit").Should().Be(1, "[OnExit] runs on leaving its state");
        Runs("Transition").Should().Be(1, "the move back from B to A is another transition");
        Runs("InState").Should().Be(1, "[InState] stops outside its state");
        Runs("ToggleKey").Should().Be(1, "it stays off until the key is pressed again");
        Runs("Changed").Should().Be(1, "nothing changed again, though Look read the health through in every frame");
        Runs("TransformChanged").Should().Be(3, "the mover's transform changed in each of the three frames");

        app.Shutdown();
        Runs("Cleanup").Should().Be(1, "[OnCleanup] runs when the app shuts down");

        // An app made after, bare as the state tests make theirs with no Time, takes none of the
        // probe's behaviors, whose startup system would throw for the Time it lacks.
        GeneratedBehaviors.All.Should().NotContain(registration => registration.Method.Module.Assembly == assembly);
        var bare = new App();
        new EcsPlugin().Build(bare);
        bare.Schedule.Systems().Should().NotContain(system => system.System.Method.Module.Assembly == assembly,
            "an app takes the behaviors of the program's assemblies and of no assembly a test compiled for its own app");
        bare.Shutdown();

        ConsoleCommands.Run("probe.ping").Should().Be("pong", "[Command] puts the method in the console's catalog");
        ConsoleCommands.Find("notacommand").Should().BeNull("a method without [Command] is not one");
        SceneComponents.Find("GeneratorProbe.Marker").Should().NotBeNull("[SceneComponent] registers the type for scene files");
        SceneComponents.Find("GeneratorProbe.Unmarked").Should().BeNull("a struct without it is not registered");
        SceneComponents.Find("GeneratorProbe.Formerly")?.Type.Name.Should().Be("Renamed", "[FormerName] finds a type by the name it had");
        SceneComponents.Find("GeneratorProbe.Versioned")?.Version.Should().Be(2, "[DataVersion] gives the codec its version");

        // A data asset is saved to a file of its own by its codec, and is no component a scene holds.
        SceneComponents.Find("GeneratorProbe.Stats").Should().BeNull("[DataAsset] registers the type for data files, not scene files");
        var stats = assembly.GetType("GeneratorProbe.Stats")!;
        var save = typeof(DataAssets).GetMethods().Single(m => m.Name == nameof(DataAssets.Save) && m.GetParameters()[0].ParameterType == typeof(string));
        using var folder = new TestFolder("engine-probe-data-");
        save.MakeGenericMethod(stats).Invoke(null, [folder.File("stats.json"), Activator.CreateInstance(stats)]);
        File.ReadAllText(folder.File("stats.json")).Should().Contain("\"type\": \"GeneratorProbe.Stats\"", "[DataAsset] registers the type for data files");
    }

    [Fact]
    public void After_And_Before_Order_Behaviors_Against_The_Stages_Own_Order_And_InStates_Gate_On_Every_One()
    {
        var app = new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins());
        var assembly = CompileAndLoad();
        var probe = assembly.GetType("GeneratorProbe.Probe")!;
        probe.GetField("World")!.SetValue(null, app.World);
        var runs = (Dictionary<string, int>)probe.GetField("Runs")!.GetValue(null)!;
        var order = (List<string>)probe.GetField("Order")!.GetValue(null)!;
        int Runs(string name) { lock (runs) return runs.GetValueOrDefault(name); }
        var mode = assembly.GetType("GeneratorProbe.Mode")!;
        var sub = assembly.GetType("GeneratorProbe.Sub")!;
        typeof(App).GetMethod(nameof(App.AddState))!.MakeGenericMethod(mode).Invoke(app, [Enum.ToObject(mode, 0)]);
        assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Single(m => m.GetCustomAttribute<GeneratedBehaviorRegistrationAttribute>() is not null).Invoke(null, [app]);
        void Set(Type state, int value)
        {
            var next = app.World.GetType().GetMethod(nameof(World.Resource))!.MakeGenericMethod(typeof(NextState<>).MakeGenericType(state)).Invoke(app.World, null)!;
            next.GetType().GetMethod("Set")!.Invoke(next, [Enum.ToObject(state, value)]);
        }
        void Frame()
        {
            app.BeginFrame();
            app.EndFrame();
        }

        Frame();
        order.Should().Equal(["C", "B", "A"], "the attributes order the three against the order they were added in, A, B and C");
        Runs("InBoth").Should().Be(0, "neither of its states holds");
        Set(mode, 1);
        Frame();
        Runs("InBoth").Should().Be(0, "the mode is B and the sub-state, entered at its first value, One");
        Set(sub, 1);
        Frame();
        Runs("InBoth").Should().Be(1, "both of its states hold");
        Set(mode, 0);
        Frame();
        Runs("InBoth").Should().Be(1, "the mode left B");
        order.Should().Equal(["C", "B", "A", "C", "B", "A", "C", "B", "A", "C", "B", "A"], "each frame runs them in that order");
        app.Shutdown();
    }

    // Runs every generator over Source, emits the result and loads it, running its module
    // initializers, where the command and scene component generators register what they found. It
    // loads into a context that can be let go, as a script compiled while an app runs does, so the
    // process's list of behaviors passes its registration over and the test registers it into its
    // own app alone. Loaded with Assembly.Load, it registered its behaviors into every app the
    // suite made after, which on macOS reached the state tests' bare apps.
    private static Assembly CompileAndLoad()
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(App).Assembly.Location));
        var compilation = CSharpCompilation.Create($"GeneratorProbe{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(Source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver.Create(new BehaviorGenerator(), new CommandGenerator(), new SceneComponentGenerator())
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

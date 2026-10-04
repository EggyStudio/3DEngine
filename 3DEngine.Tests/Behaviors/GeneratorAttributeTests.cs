using System.Reflection;
using System.Runtime.CompilerServices;
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
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public class GeneratorAttributeTests
{
    // One source with a use of every attribute. Each method counts its runs in Probe, but only in
    // the app this test made, since another test's app scans loaded assemblies for behaviors and
    // would run these as well.
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
            public static readonly System.Collections.Generic.Dictionary<string, int> Runs = new();

            public static void Ran(BehaviorContext ctx, string name)
            {
                if (!ReferenceEquals(ctx.World, World)) return;
                lock (Runs) Runs[name] = Runs.TryGetValue(name, out var n) ? n + 1 : 1;
            }
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
        }

        public static class Commands
        {
            [Command("probe.ping", "Answers pong")] public static string Ping() => "pong";
            public static string NotACommand() => "never";
        }

        [SceneComponent] public struct Marker { public int Value; }
        public struct Unmarked { public int Value; }
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
        ["Engine.WithAttribute"] = "With",
        ["Engine.WithoutAttribute"] = "Without",
        ["Engine.ChangedAttribute"] = "Changed",
        ["Engine.AddedAttribute"] = "Added",
        ["Engine.RunIfAttribute"] = "RunIf",
        ["Engine.ToggleKeyAttribute"] = "ToggleKey",
        ["Engine.CommandAttribute"] = "probe.ping",
        ["Engine.SceneComponentAttribute"] = "Marker",
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
        Runs("Changed").Should().Be(1, "nothing changed again");

        app.Shutdown();
        Runs("Cleanup").Should().Be(1, "[OnCleanup] runs when the app shuts down");

        ConsoleCommands.Run("probe.ping").Should().Be("pong", "[Command] puts the method in the console's catalog");
        ConsoleCommands.Find("notacommand").Should().BeNull("a method without [Command] is not one");
        SceneComponents.Find("GeneratorProbe.Marker").Should().NotBeNull("[SceneComponent] registers the type for scene files");
        SceneComponents.Find("GeneratorProbe.Unmarked").Should().BeNull("a struct without it is not registered");
    }

    // Runs every generator over Source, emits the result and loads it, running its module
    // initializers, where the command and scene component generators register what they found.
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
        var assembly = Assembly.Load(image.ToArray());
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        return assembly;
    }
}

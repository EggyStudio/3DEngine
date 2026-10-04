using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Engine.Tests.Behaviors;

/// <summary>Runs the behavior generator over small sources and compiles what it emits.</summary>
[Trait("Category", "Unit")]
public class BehaviorGeneratorTests
{
    private static (ImmutableArrayResult Result, Compilation Output) Generate(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(App).Assembly.Location));

        var compilation = CSharpCompilation.Create("Generated",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver.Create(new BehaviorGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        return (new ImmutableArrayResult(diagnostics.ToArray(), output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray()), output);
    }

    private sealed record ImmutableArrayResult(Diagnostic[] Generator, Diagnostic[] Compile);

    [Fact]
    public void Two_Methods_On_One_Stage_Become_Two_Systems()
    {
        var (result, output) = Generate("""
            using Engine;
            [Behavior]
            public struct Twice
            {
                public static bool Never => false;
                [OnUpdate] public static void A(BehaviorContext ctx) { }
                [OnUpdate, RunIf(nameof(Never))] public static void B(BehaviorContext ctx) { }
            }
            """);

        result.Generator.Should().BeEmpty();
        result.Compile.Should().BeEmpty();
        var generated = string.Concat(output.SyntaxTrees.Select(t => t.ToString()));
        generated.Should().Contain("Twice_Generated_Update_A").And.Contain("Twice_Generated_Update_B");
    }

    [Fact]
    public void Changed_And_Added_Filters_Compile_Against_The_Stores()
    {
        var (result, output) = Generate("""
            using Engine;
            public struct Health { public int Value; }
            [Behavior]
            public struct Watcher
            {
                public int Seen;
                [OnUpdate, Changed(typeof(Health))] public void Hurt(BehaviorContext ctx) => Seen++;
                [OnUpdate, Added(typeof(Health))] public void Born(BehaviorContext ctx) => Seen++;
            }
            """);

        result.Generator.Should().BeEmpty();
        result.Compile.Should().BeEmpty();
        var generated = string.Concat(output.SyntaxTrees.Select(t => t.ToString()));
        generated.Should().Contain(".Changed(entity)").And.Contain(".Added(entity)");
    }

    [Fact]
    public void A_Filter_On_A_Type_No_Entity_Can_Have_Is_Reported_On_The_Method()
    {
        var (result, _) = Generate("""
            using Engine;
            public interface IThing { }
            public static class Helpers { }
            [Behavior]
            public struct Picky
            {
                [OnUpdate, With(typeof(IThing))] public void Never(BehaviorContext ctx) { }
                [OnUpdate, Without(typeof(Helpers))] public void Nor(BehaviorContext ctx) { }
                [OnUpdate, With(typeof(Transform))] public void Fine(BehaviorContext ctx) { }
            }
            """);

        result.Generator.Where(d => d.Id == "E3D005").Select(d => d.GetMessage())
            .Should().HaveCount(2).And.Contain(m => m.Contains("an interface")).And.Contain(m => m.Contains("a static class"));
        result.Compile.Should().BeEmpty("the reported methods are left out");
    }

    [Fact]
    public void A_Field_Holding_A_Reference_Is_Warned_Of_And_A_String_Is_Not()
    {
        var (result, _) = Generate("""
            using Engine;
            [Behavior]
            public struct Holder
            {
                public System.Collections.Generic.List<int> Shared;
                public string Name;
                public int Count;
                [OnUpdate] public void Tick(BehaviorContext ctx) { }
            }
            """);

        result.Generator.Where(d => d.Id == "E3D006").Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Holder.Shared");
        result.Compile.Should().BeEmpty();
    }

    [Fact]
    public void A_Fixed_Update_Method_Registers_On_The_Fixed_Stage()
    {
        var (result, output) = Generate("""
            using Engine;
            [Behavior]
            public struct Stepper
            {
                [OnFixedUpdate] public static void Step(BehaviorContext ctx) { }
            }
            """);

        result.Compile.Should().BeEmpty();
        string.Concat(output.SyntaxTrees.Select(t => t.ToString())).Should().Contain("Engine.Stage.FixedUpdate");
    }

    [Fact]
    public void State_Attributes_Register_Transitions_And_A_Condition()
    {
        var (result, output) = Generate("""
            using Engine;
            namespace Game;
            public enum Screen { Menu, Playing }
            [Behavior]
            public struct Flow
            {
                [OnEnter(Screen.Playing)] public static void Load(BehaviorContext ctx) { }
                [OnExit(Screen.Menu)] public void Hide(BehaviorContext ctx) { }
                [OnUpdate, InState(Screen.Playing)] public static void Tick(BehaviorContext ctx) { }
            }
            """);

        result.Generator.Should().BeEmpty();
        result.Compile.Should().BeEmpty();
        var generated = string.Concat(output.SyntaxTrees.Select(t => t.ToString()));
        generated.Should().Contain("app.OnEnter(global::Game.Screen.Playing,")
            .And.Contain("app.OnExit(global::Game.Screen.Menu,")
            .And.Contain("BehaviorConditions.InState(global::Game.Screen.Playing)");
    }

    [Theory]
    [InlineData("[OnUpdate] public static int Wrong(BehaviorContext ctx) => 0;", "E3D001")]
    [InlineData("[OnUpdate] public static void Wrong() { }", "E3D001")]
    [InlineData("[OnUpdate, OnRender] public static void Wrong(BehaviorContext ctx) { }", "E3D002")]
    [InlineData("[OnUpdate, RunIf(\"Missing\")] public static void Wrong(BehaviorContext ctx) { }", "E3D003")]
    [InlineData("public bool NotStatic; [OnUpdate, RunIf(nameof(NotStatic))] public static void Wrong(BehaviorContext ctx) { }", "E3D003")]
    [InlineData("[OnEnter(3)] public static void Wrong(BehaviorContext ctx) { }", "E3D004")]
    [InlineData("[OnUpdate, InState(\"Playing\")] public static void Wrong(BehaviorContext ctx) { }", "E3D004")]
    [InlineData("public enum A { X } public enum B { Y } [OnTransition(A.X, B.Y)] public static void Wrong(BehaviorContext ctx) { }", "E3D004")]
    [InlineData("public enum A { X } [ComputedState] public static int Wrong(A a) => 0;", "E3D007")]
    [InlineData("public enum A { X } public enum C { Z } [ComputedState] public C? Wrong(A a) => null;", "E3D007")]
    [InlineData("public enum A { X } [SubStateOf(A.X, Initial = A.X)] public enum Wrong { P }", "E3D007")]
    public void A_Method_That_Cannot_Run_Is_Reported_On_The_Method(string member, string id)
    {
        var (result, _) = Generate($$"""
            using Engine;
            [Behavior]
            public struct Faulty
            {
                {{member}}
            }
            """);

        result.Generator.Should().ContainSingle(d => d.Id == id)
            .Which.Location.SourceTree.Should().NotBeNull();
        result.Compile.Should().BeEmpty("a reported method is left out of what is generated");
    }

    [Fact]
    public void A_Method_That_May_Write_Marks_Its_Behavior_Changed_And_A_Readonly_One_Does_Not()
    {
        var (result, output) = Generate("""
            using Engine;
            [Behavior]
            public struct Counter
            {
                public int Count;
                [OnUpdate] public void Tick(BehaviorContext ctx) => Count++;
                [OnPostUpdate] public readonly void Show(BehaviorContext ctx) { }
            }
            """);

        result.Generator.Should().BeEmpty();
        result.Compile.Should().BeEmpty();
        var systems = string.Concat(output.SyntaxTrees.Select(t => t.ToString())).Split("private static void ");
        var tick = systems.Single(s => s.StartsWith("Counter_Generated_Update_Tick("));
        var show = systems.Single(s => s.StartsWith("Counter_Generated_PostUpdate_Show("));
        tick.Should().Contain("MarkChangedByDenseIndex");
        show.Should().NotContain("MarkChangedByDenseIndex");
    }
}

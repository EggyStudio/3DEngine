using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Engine.CodeFixes;

namespace Engine.Tests.Behaviors;

[Trait("Category", "Unit")]
public class GeneratorCodeFixTests
{
    // Runs a generator over source as an editor would hold it, applies the fix of the given title
    // to its first diagnostic of the given id, and returns the fixed source with the diagnostics
    // the generator reports on it.
    private static async Task<(string Text, Diagnostic[] After)> Fix(ISourceGenerator generator, string source, string id, string title)
    {
        using var workspace = new AdhocWorkspace();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(App).Assembly.Location));
        var project = workspace.AddProject("Fixed", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(references);
        var document = project.AddDocument("Game.cs", SourceText.From(source));

        var diagnostic = (await Diagnose(document.Project, generator)).First(d => d.Id == id);
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await new GeneratorCodeFixes().RegisterCodeFixesAsync(context);
        actions.Select(a => a.Title).Should().Contain(title);

        var operations = await actions.First(a => a.Title == title).GetOperationsAsync(CancellationToken.None);
        var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var fixedDocument = solution.GetDocument(document.Id)!;
        return ((await fixedDocument.GetTextAsync()).ToString(), await Diagnose(fixedDocument.Project, generator));
    }

    private static async Task<Diagnostic[]> Diagnose(Project project, ISourceGenerator generator)
    {
        var compilation = await project.GetCompilationAsync();
        CSharpGeneratorDriver.Create(generator).RunGeneratorsAndUpdateCompilation(compilation!, out _, out var diagnostics);
        return [.. diagnostics];
    }

    private static ISourceGenerator Behaviors => new BehaviorGenerator().AsSourceGenerator();
    private static ISourceGenerator Commands => new CommandGenerator().AsSourceGenerator();

    [Fact]
    public async Task A_Stage_Method_Is_Given_The_Context_It_Takes()
    {
        var (text, after) = await Fix(Behaviors, """
            using Engine;
            [Behavior]
            public struct Spin
            {
                [OnUpdate] public static void Turn(float speed) { }
            }
            """, GeneratorCodeFixes.BadSignature, "Take the BehaviorContext a stage method is given");

        text.Should().Contain("[OnUpdate] public static void Turn(BehaviorContext ctx) { }");
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task The_Context_Moves_First_And_The_Components_Stay_After_It()
    {
        var (text, after) = await Fix(Behaviors, """
            using Engine;
            [Behavior]
            public struct Spin
            {
                [OnUpdate] public void Turn(ref Transform transform, BehaviorContext context, float speed) { }
            }
            """, GeneratorCodeFixes.BadSignature, "Take the BehaviorContext a stage method is given");

        text.Should().Contain("[OnUpdate] public void Turn(BehaviorContext context, ref Transform transform) { }");
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task One_Stage_Is_Kept_Of_Several_Whether_In_One_List_Or_Two()
    {
        var (text, after) = await Fix(Behaviors, """
            using Engine;
            [Behavior]
            public struct Twice
            {
                [OnUpdate, OnRender, RunIf("Ready")] public static void Both(BehaviorContext ctx) { }
                public static bool Ready => true;
            }
            """, GeneratorCodeFixes.SeveralStages, "Run in OnRender alone");
        text.Should().Contain("[OnRender, RunIf(\"Ready\")] public static void Both(BehaviorContext ctx) { }");
        after.Should().BeEmpty();

        (text, after) = await Fix(Behaviors, """
            using Engine;
            [Behavior]
            public struct Twice
            {
                [OnUpdate]
                [OnRender]
                public static void Both(BehaviorContext ctx) { }
            }
            """, GeneratorCodeFixes.SeveralStages, "Run in OnUpdate alone");
        text.Should().Contain("""
                [OnUpdate]
                public static void Both(BehaviorContext ctx) { }
            """);
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task A_Command_Is_Made_Static()
    {
        var (text, after) = await Fix(Commands, """
            using Engine;
            public class Tools
            {
                [Command("tools.ping", "Answers pong")]
                public string Ping() => "pong";
            }
            """, GeneratorCodeFixes.NotStatic, "Make the command static");

        text.Should().Contain("""
                [Command("tools.ping", "Answers pong")]
                public static string Ping() => "pong";
            """);
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task A_Command_Is_Made_Internal_Whether_Private_Or_Bare()
    {
        var (text, after) = await Fix(Commands, """
            using Engine;
            public static class Tools
            {
                [Command("tools.ping", "Answers pong")]
                private static string Ping() => "pong";
            }
            """, GeneratorCodeFixes.NotReachable, "Make the command internal");
        text.Should().Contain("""
                [Command("tools.ping", "Answers pong")]
                internal static string Ping() => "pong";
            """);
        after.Should().BeEmpty();

        (text, after) = await Fix(Commands, """
            using Engine;
            public static class Tools
            {
                [Command("tools.ping", "Answers pong")]
                static string Ping() => "pong";
            }
            """, GeneratorCodeFixes.NotReachable, "Make the command internal");
        text.Should().Contain("""
                [Command("tools.ping", "Answers pong")]
                internal static string Ping() => "pong";
            """);
        after.Should().BeEmpty();
    }
}

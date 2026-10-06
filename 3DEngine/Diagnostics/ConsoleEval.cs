using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using Engine.Files.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine;

/// <summary>
/// The console's <c>eval</c>: C# compiled against the running program and run between frames, for
/// looking at and changing a game while it runs where no command was written for it.
/// </summary>
/// <remarks>
/// <para>
/// A fragment is a program's top-level statements, so it may begin with using directives, declare
/// local functions and types after its statements and await. It sees the namespaces an example's
/// file does, the flat API among them, and the running program as <c>world</c>, <c>ecs</c> and
/// <c>app</c>. A last statement that is an expression with no semicolon answers with its value, as
/// C# Interactive does, so <c>ecs.EntityCount</c> is answered with the count, and a fragment that
/// ends in any other statement answers <c>null</c>.
/// </para>
/// <para>
/// It is compiled against every assembly the process has loaded, so a game's own types are in
/// reach, into a collectible load context let go after its one run. It runs on the main thread
/// between frames, as every command does, so it may touch whatever a behavior may, and its compile
/// holds that frame, longest the first time, while Roslyn's own code is compiled. A native build
/// cannot load code compiled while it runs, and answers so.
/// </para>
/// </remarks>
internal static class ConsoleEval
{
    // Why reading a fragment's assembly by reflection is safe where a build is trimmed (N 2.5).
    private const string Fragment = "A fragment is compiled while the program runs, so no trimmer has seen or cut its assembly, and its "
        + "scope and entry point are read from that assembly alone. A native build answers before compiling, and in a trimmed build "
        + "a fragment naming what the trimmer cut fails to compile, with the compiler's own error.";

    private static readonly ILogger Logger = Log.Category("Engine.Console");
    private static readonly CSharpParseOptions Parsing = new(LanguageVersion.Latest);
    private static int _generation;

    // What every fragment is compiled beside: the usings an example's file has, with the implicit
    // ones of a .NET project, and the running program by name, set before the fragment runs.
    private const string Scope = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Numerics;
        global using System.Threading.Tasks;
        global using Engine;
        global using static Engine.Engine3D;
        global using static FragmentScope;

        internal static class FragmentScope
        {
            public static World world = null!;
            public static EcsWorld ecs = null!;
            public static App? app;
            public static object? Answer;
        }
        """;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = Fragment)]
    [Command("eval", "Compiles C# against the running program and runs it between frames, answering with the value of a last expression: eval <code>")]
    internal static string Eval(string code)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported)
            return Refuse("NO_COMPILER", "A native build cannot load code compiled while it runs, so eval needs a build that runs on the JIT.", "not run");
        if (string.IsNullOrWhiteSpace(code))
            return Refuse("BAD_ARGUMENT", "There is no code to run: eval <code>.", "not run");

        var (fragment, bare) = Ended(code);
        var name = $"Eval_{Interlocked.Increment(ref _generation)}";
        var compilation = CSharpCompilation.Create(name,
            [CSharpSyntaxTree.ParseText(Scope, Parsing, "<scope>"), fragment],
            References(),
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithNullableContextOptions(NullableContextOptions.Enable)
                .WithAllowUnsafe(true));
        if (bare) compilation = Answering(compilation);

        using var image = new MemoryStream();
        var built = compilation.Emit(image);
        if (!built.Success) return Refuse("EVAL_COMPILE_FAILED", Errors(built.Diagnostics), "not compiled");

        var context = new FragmentContext(name);
        try
        {
            image.Position = 0;
            return Run(context.LoadFromStream(image));
        }
        finally
        {
            // Let go once nothing holds it, which a type the fragment declared and left on an
            // entity or in an event puts off for as long as it stays there.
            context.Unload();
        }
    }

    // The fragment parsed, with the semicolon its last statement lacks where it was left off, and
    // whether that statement is an expression written bare, whose value is the answer. A
    // semicolon written after an expression keeps its value back, as C# Interactive has it.
    private static (SyntaxTree Tree, bool Bare) Ended(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code, Parsing, "<eval>");
        if (LastStatement(tree) is not { } last || last.GetLastToken(includeZeroWidth: true) is not { RawKind: (int)SyntaxKind.SemicolonToken, IsMissing: true })
            return (tree, false);
        // Read again once ended, since a name alone parses as the type of a declaration left
        // unfinished until its semicolon is there.
        var ended = CSharpSyntaxTree.ParseText(code.Insert(last.GetLastToken().Span.End, ";"), Parsing, "<eval>");
        return (ended, LastStatement(ended) is ExpressionStatementSyntax);
    }

    // The fragment's last statement, the local functions it declares after it passed over.
    private static StatementSyntax? LastStatement(SyntaxTree fragment) =>
        ((CompilationUnitSyntax)fragment.GetRoot()).Members.OfType<GlobalStatementSyntax>()
            .LastOrDefault(g => g.Statement is not LocalFunctionStatementSyntax)?.Statement;

    // The compilation with the fragment's last statement, an expression, handing its value to
    // FragmentScope.Answer when it has one, or as it was.
    private static CSharpCompilation Answering(CSharpCompilation compilation)
    {
        var fragment = compilation.SyntaxTrees[1];
        if (LastStatement(fragment) is not ExpressionStatementSyntax { Expression: var expression } statement) return compilation;

        var type = compilation.GetSemanticModel(fragment).GetTypeInfo(expression).Type;
        if (type is null or IErrorTypeSymbol || type.SpecialType == SpecialType.System_Void) return compilation;

        var text = fragment.GetText().ToString();
        var answered = string.Concat(text.AsSpan(0, statement.SpanStart), $"FragmentScope.Answer = (object?)({expression});",
            text.AsSpan(statement.Span.End));
        return compilation.ReplaceSyntaxTree(fragment, CSharpSyntaxTree.ParseText(answered, Parsing, "<eval>"));
    }

    // What a fragment compiles against: the platform's assemblies and every other the process has
    // loaded from a file, the engine and the game among them.
    [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "An assembly with no file has an empty Location and is left out.")]
    private static List<MetadataReference> References()
    {
        var paths = new HashSet<string>(CompilerReferences.Platform());
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (!assembly.IsDynamic && assembly.Location is { Length: > 0 } location)
                paths.Add(location);
        return [.. paths.Select(CompilerReferences.Of).OfType<MetadataReference>()];
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = Fragment)]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = Fragment)]
    private static string Run(Assembly assembly)
    {
        var scope = assembly.GetType("FragmentScope")!;
        var world = ConsoleHost.World!;
        scope.GetField("world")!.SetValue(null, world);
        scope.GetField("ecs")!.SetValue(null, world.TryGetResource<EcsWorld>(out var ecs) ? ecs : null);
        scope.GetField("app")!.SetValue(null, ConsoleHost.App);

        var entry = assembly.EntryPoint!;
        try
        {
            var returned = entry.Invoke(null, entry.GetParameters().Length == 0 ? null : [Array.Empty<string>()]);
            if (returned is Task task) task.GetAwaiter().GetResult();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // What the fragment threw rather than the reflection's wrapper around it, its stack to
            // the log and one line to the caller.
            var thrown = error is TargetInvocationException { InnerException: { } inner } ? inner : error;
            Logger.Error("An eval fragment threw.", thrown);
            return Refuse("EVAL_THREW", $"{thrown.GetType().Name}: {thrown.Message}", "threw");
        }

        return Describe(scope.GetField("Answer")!.GetValue(null));
    }

    // The first errors, each with its place in the fragment.
    private static string Errors(IEnumerable<Diagnostic> diagnostics)
    {
        var text = new StringBuilder();
        foreach (var diagnostic in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(5))
        {
            var span = diagnostic.Location.GetLineSpan();
            if (span.Path == "<eval>")
                text.Append(span.StartLinePosition.Line + 1).Append(':').Append(span.StartLinePosition.Character + 1).Append(' ');
            text.Append(diagnostic.Id).Append(' ').AppendLine(diagnostic.GetMessage());
        }
        return text.ToString().TrimEnd();
    }

    // An answer as text, a collection by its first items and a value with no text of its own by
    // its fields, as entity.get shows a component.
    private static string Describe(object? answer)
    {
        const int Shown = 50;
        switch (answer)
        {
            case null:
                return "null";
            case string text:
                return text;
            case System.Collections.IEnumerable items:
                var listed = items.Cast<object?>().Take(Shown + 1).Select(item => item?.ToString() ?? "null").ToList();
                return listed.Count > Shown ? string.Join(", ", listed.Take(Shown)) + ", ..." : string.Join(", ", listed);
            default:
                var own = answer.ToString();
                return own is null || own == answer.GetType().ToString() ? ConsoleBuiltins.Describe(answer) : own;
        }
    }

    // The failure for the caller, and the word the command answers with beside it.
    private static string Refuse(string code, string message, string answer)
    {
        ConsoleHost.Fail(code, message);
        return answer;
    }

    // One fragment's assembly, in a context let go after its run. Its dependencies resolve in the
    // default context, so it sees the program's own types rather than copies.
    private sealed class FragmentContext(string name) : AssemblyLoadContext(name, isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}

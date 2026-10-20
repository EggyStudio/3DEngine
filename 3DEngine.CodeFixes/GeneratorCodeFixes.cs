using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.CodeFixes;

/// <summary>
/// The fixes an editor offers for the behavior, command and scene component generators'
/// diagnostics, where the right change is clear from the diagnostic alone, a stage method given the
/// context it takes, one stage kept of several, a command made static or reachable, and a versioned
/// scene component given a migration to fill in.
/// </summary>
/// <remarks>
/// A stage method returning a value (E3D001), a <c>[RunIf]</c> naming nothing (E3D003) and the rest
/// have no fix, since what was meant is the program's to say.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeneratorCodeFixes)), Shared]
public sealed class GeneratorCodeFixes : CodeFixProvider
{
    /// <summary>A stage method that does not take a <c>BehaviorContext</c> first.</summary>
    public const string BadSignature = "E3D001";

    /// <summary>A method with more than one stage attribute.</summary>
    public const string SeveralStages = "E3D002";

    /// <summary>A command that is not static.</summary>
    public const string NotStatic = "E3D100";

    /// <summary>A command that is neither public nor internal.</summary>
    public const string NotReachable = "E3D101";

    /// <summary>A scene component past data version 1 with no <c>Migrate</c>.</summary>
    public const string NoMigration = "E3D200";

    // The attributes that place a method, by their short names, as a program writes them.
    private static readonly string[] StageAttributes =
    [
        "OnStartup", "OnFirst", "OnPreUpdate", "OnFixedUpdate", "OnUpdate", "OnPostUpdate", "OnRender", "OnLast", "OnCleanup",
        "OnEnter", "OnExit", "OnTransition",
    ];

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [BadSignature, SeveralStages, NotStatic, NotReachable, NoMigration];

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var node = root?.FindNode(context.Span);
        if (root is null || node is null) return;

        // E3D200 is reported on the struct, and the rest on a method.
        if (node is StructDeclarationSyntax type)
        {
            foreach (var diagnostic in context.Diagnostics.Where(d => d.Id == NoMigration))
                Register(context, diagnostic, root, "Add a Migrate method to move each version forward", type, WithMigration(type));
            return;
        }
        if (node.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } method) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            switch (diagnostic.Id)
            {
                case BadSignature when method.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword }:
                    Register(context, diagnostic, root, "Take the BehaviorContext a stage method is given", method,
                        method.WithParameterList(ContextFirst(method)));
                    break;

                case SeveralStages:
                    foreach (var keep in StagesOn(method))
                        Register(context, diagnostic, root, $"Run in {keep} alone", method, KeepingStage(method, keep));
                    break;

                case NotStatic:
                    Register(context, diagnostic, root, "Make the command static", method, WithModifier(method, SyntaxKind.StaticKeyword, first: false));
                    break;

                case NotReachable:
                    Register(context, diagnostic, root, "Make the command internal", method, Internal(method));
                    break;
            }
        }
    }

    private static void Register(CodeFixContext context, Diagnostic diagnostic, SyntaxNode root, string title,
        SyntaxNode before, SyntaxNode after)
    {
        context.RegisterCodeFix(
            CodeAction.Create(title, _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(before, after))), title),
            diagnostic);
    }

    // The struct with a Migrate added after its members, one step in from the struct and after a
    // blank line, giving the object back as it came for the program to change. Its types are
    // written in full, since the file may not use System.Text.Json.Nodes.
    private static StructDeclarationSyntax WithMigration(StructDeclarationSyntax type)
    {
        var indent = type.GetLeadingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).ToString();
        var step = indent + "    ";
        var line = type.DescendantTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia)).ToString() is { Length: > 0 } found
            ? found
            : "\n";
        const string node = "System.Text.Json.Nodes.JsonObject";
        var text = string.Concat(
            (type.Members.Count > 0 ? line : ""),
            step, "// Brings a value saved at version from up to this one.", line,
            step, $"public static {node} Migrate(int from, {node} value)", line,
            step, "{", line,
            step, "    return value;", line,
            step, "}", line);
        return type.AddMembers(SyntaxFactory.ParseMemberDeclaration(text)!);
    }

    // The stage attributes on the method, by short name, in the order written.
    private static IEnumerable<string> StagesOn(MethodDeclarationSyntax method) =>
        method.AttributeLists.SelectMany(list => list.Attributes).Select(ShortName).Where(StageAttributes.Contains).Distinct();

    private static string ShortName(AttributeSyntax attribute)
    {
        var name = attribute.Name switch
        {
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax a => a.Name.Identifier.ValueText,
            SimpleNameSyntax s => s.Identifier.ValueText,
            _ => attribute.Name.ToString(),
        };
        return name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Attribute".Length) : name;
    }

    // The method with every stage attribute but keep taken out, and lists left empty removed.
    private static MethodDeclarationSyntax KeepingStage(MethodDeclarationSyntax method, string keep)
    {
        var lists = new List<AttributeListSyntax>();
        foreach (var list in method.AttributeLists)
        {
            var kept = list.Attributes.Where(a => ShortName(a) is var n && (!StageAttributes.Contains(n) || n == keep)).ToList();
            if (kept.Count > 0) lists.Add(list.WithAttributes(SyntaxFactory.SeparatedList(kept)));
        }
        var result = method.WithAttributeLists(SyntaxFactory.List(lists));
        // An attribute list removed from the front takes the method's leading trivia with it.
        return lists.Count == method.AttributeLists.Count ? result : result.WithLeadingTrivia(method.GetLeadingTrivia());
    }

    // The method with its accessibility replaced by internal, or internal put first where it had none.
    private static MethodDeclarationSyntax Internal(MethodDeclarationSyntax method)
    {
        var access = method.Modifiers.Where(m => m.IsKind(SyntaxKind.PrivateKeyword) || m.IsKind(SyntaxKind.ProtectedKeyword)).ToList();
        if (access.Count == 0) return WithModifier(method, SyntaxKind.InternalKeyword, first: true);

        var internalToken = SyntaxFactory.Token(SyntaxKind.InternalKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        var modifiers = method.Modifiers.Replace(access[0], internalToken.WithLeadingTrivia(access[0].LeadingTrivia));
        foreach (var extra in access.Skip(1)) modifiers = modifiers.Remove(modifiers.First(m => m.IsKind(extra.Kind())));
        return method.WithModifiers(modifiers);
    }

    // The method with a modifier added first or last among its modifiers. The indentation before
    // the method's first word stays first, on the new modifier when it goes in front.
    private static MethodDeclarationSyntax WithModifier(MethodDeclarationSyntax method, SyntaxKind kind, bool first)
    {
        var token = SyntaxFactory.Token(kind).WithTrailingTrivia(SyntaxFactory.Space);
        if (method.Modifiers.Count > 0)
        {
            if (!first) return method.WithModifiers(method.Modifiers.Add(token));
            var head = method.Modifiers[0];
            return method.WithModifiers(method.Modifiers.Replace(head, head.WithLeadingTrivia()).Insert(0, token.WithLeadingTrivia(head.LeadingTrivia)));
        }

        var returnType = method.ReturnType;
        return method.WithReturnType(returnType.WithLeadingTrivia())
            .WithModifiers(SyntaxFactory.TokenList(token.WithLeadingTrivia(returnType.GetLeadingTrivia())));
    }

    // The parameters with the context first, the one the method takes kept by its name or one named
    // ctx added, followed by those an instance method takes by ref or in, which may be its entity's
    // components. Any other could not be one and is dropped, as a static method's all are.
    private static ParameterListSyntax ContextFirst(MethodDeclarationSyntax method)
    {
        var list = method.ParameterList;
        static bool IsContext(ParameterSyntax p) => p.Type?.ToString() is "BehaviorContext" or "Engine.BehaviorContext";
        var isStatic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        var context = list.Parameters.FirstOrDefault(IsContext)?.ToString() ?? "BehaviorContext ctx";
        var rest = list.Parameters
            .Where(p => !isStatic && !IsContext(p) && p.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword) || m.IsKind(SyntaxKind.InKeyword)))
            .Select(p => p.ToString());
        return SyntaxFactory.ParseParameterList($"({string.Join(", ", rest.Prepend(context))})")
            .WithTrailingTrivia(list.GetTrailingTrivia());
    }
}

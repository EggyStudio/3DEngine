using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.Tests.Api;

/// <summary>
/// Writes CHEATSHEET.md's lists of the flat API's functions from the engine's source, each a line
/// of the function's declaration and the first sentence of its summary, and keeps the rest of the
/// page as it is.
/// </summary>
/// <remarks>
/// <para>
/// A list is a C# block under a line naming the files of <c>3DEngine/Api</c> it is written from,
/// as <c>&lt;!-- written by build/cheatsheet.sh from E3D.Window.cs, E3D.Display.cs --&gt;</c>. It
/// holds each file's public functions in the order the file declares them, the files in the order
/// the line names them, with a blank line between two files and where a banner starts a part of a
/// file. Everything outside those blocks is written by hand and kept.
/// </para>
/// <para>
/// A declaration is written as the source writes it, without its modifiers and attributes, except a
/// default value naming a constant or an enum member, which is written as the built engine holds
/// it, so the page says the number. A summary's references are written as the names they refer to.
/// Every file declaring a public function of <see cref="E3D"/> is named by one line, so a file
/// added fails until its functions have a place on the page.
/// </para>
/// </remarks>
internal static partial class CheatsheetWriter
{
    // Where a line's comment starts after a shorter declaration, so a function added or changed
    // moves its own line alone.
    private const int CommentColumn = 57;

    /// <summary>The page with each list written again from the source under <paramref name="root"/>.</summary>
    /// <exception cref="InvalidDataException">A list names a file with no functions, or twice, or a file is named by no list.</exception>
    public static string Write(string root, string page)
    {
        var files = Functions(Path.Combine(root, "3DEngine", "Api"));
        var lines = page.ReplaceLineEndings("\n").Split('\n');
        var output = new List<string>(lines.Length);
        var named = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < lines.Length; i++)
        {
            output.Add(lines[i]);
            if (Marker().Match(lines[i]) is not { Success: true } marker) continue;
            var at = $"CHEATSHEET.md:{i + 1}";
            if (i + 1 >= lines.Length || lines[i + 1] != "```csharp")
                throw new InvalidDataException($"{at}: the line naming files is not followed by a C# block.");
            output.Add(lines[++i]);
            while (++i < lines.Length && lines[i] != "```") { }
            if (i >= lines.Length) throw new InvalidDataException($"{at}: its C# block is not closed.");

            // A break is a blank line where a function follows another in the block.
            bool written = false, broken = false;
            foreach (var file in marker.Groups["files"].Value.Split(", "))
            {
                if (!files.TryGetValue(file, out var entries))
                    throw new InvalidDataException($"{at}: {file} declares no public function of E3D in 3DEngine/Api.");
                if (!named.Add(file)) throw new InvalidDataException($"{at}: {file} is named by another list too.");
                broken = true;
                foreach (var entry in entries)
                {
                    if (entry is null)
                    {
                        broken = true;
                        continue;
                    }
                    if (broken && written) output.Add("");
                    output.Add(entry);
                    written = true;
                    broken = false;
                }
            }
            output.Add(lines[i]);
        }

        var unnamed = files.Keys.Where(f => !named.Contains(f)).Order(StringComparer.Ordinal).ToArray();
        if (unnamed.Length > 0)
            throw new InvalidDataException($"No list of CHEATSHEET.md names {string.Join(", ", unnamed)}, so its functions have no place on the page.");
        return string.Join("\n", output);
    }

    // Each file's lines in the order it declares its public functions, null where a banner starts a
    // part of it, by the file's name.
    private static Dictionary<string, List<string?>> Functions(string folder)
    {
        var parsed = Directory.EnumerateFiles(folder, "*.cs")
            .Select(path => (File: Path.GetFileName(path), Root: CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetCompilationUnitRoot()))
            .Select(f => (f.File, Methods: f.Root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(c => c.Identifier.Text == nameof(E3D) && c.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
                .SelectMany(c => c.Members).ToArray()))
            .ToArray();
        var all = parsed.SelectMany(f => f.Methods).OfType<MethodDeclarationSyntax>().Where(IsPublicFunction).ToArray();

        var files = new Dictionary<string, List<string?>>(StringComparer.Ordinal);
        foreach (var (file, members) in parsed)
        {
            var entries = new List<string?>();
            foreach (var member in members)
            {
                if (member.GetLeadingTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) && t.ToString().StartsWith("// -- ", StringComparison.Ordinal)))
                    entries.Add(null);
                if (member is MethodDeclarationSyntax method && IsPublicFunction(method)) entries.Add(Line(method, all));
            }
            if (entries.Any(e => e is not null)) files[file] = entries;
        }
        return files;
    }

    private static bool IsPublicFunction(MethodDeclarationSyntax method) =>
        method.Modifiers.Any(SyntaxKind.PublicKeyword) && method.Modifiers.Any(SyntaxKind.StaticKeyword);

    private static string Line(MethodDeclarationSyntax method, MethodDeclarationSyntax[] all)
    {
        var declaration = Declaration(method);
        var summary = Summary(method, all);
        if (summary.Length == 0) return declaration;
        return (declaration.Length < CommentColumn ? declaration.PadRight(CommentColumn) : declaration + " ") + "// " + summary;
    }

    private static string Declaration(MethodDeclarationSyntax method)
    {
        var built = typeof(E3D).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .FirstOrDefault(m => m.Name == method.Identifier.Text
                && m.GetParameters().Select(p => p.Name).SequenceEqual(method.ParameterList.Parameters.Select(p => p.Identifier.Text)));
        var parameters = method.ParameterList.Parameters.Select((p, i) =>
        {
            var text = string.Join(" ", p.Modifiers.Select(m => m.Text).Append(p.Type!.ToString()).Append(p.Identifier.Text));
            if (p.Default is { Value: var value })
            {
                text += " = " + (value is LiteralExpressionSyntax or DefaultExpressionSyntax or PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax } || built is null
                    ? value.ToString()
                    : PublicSurface.Value(built.GetParameters()[i].RawDefaultValue, built.GetParameters()[i].ParameterType));
            }
            return text;
        });
        // A type a file names in full, as one beside a library's own of that name, is written as a
        // program importing System.Numerics names it.
        var declaration = $"{method.ReturnType} {method.Identifier.Text}{method.TypeParameterList}({string.Join(", ", parameters)});";
        return Spaced(declaration.Replace("System.Numerics.", "", StringComparison.Ordinal));
    }

    // The first sentence of the function's summary, or of the one its inheritdoc names, without its
    // full stop, since a line is a reminder and the guide is the explanation.
    private static string Summary(MethodDeclarationSyntax method, MethodDeclarationSyntax[] all)
    {
        var docs = method.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault();
        if (docs is null) return "";
        if (Element(docs, "summary") is { } summary)
        {
            var text = Spaced(Text(summary.Content)).Trim();
            var end = Sentence().Match(text);
            return (end.Success ? text[..end.Index] : text).TrimEnd('.');
        }

        var inherited = docs.Content.OfType<XmlEmptyElementSyntax>().FirstOrDefault(e => e.Name.ToString() == "inheritdoc");
        if (inherited?.Attributes.OfType<XmlCrefAttributeSyntax>().FirstOrDefault()?.Cref is not { } cref) return "";
        var name = Regex.Replace(cref.ToString(), @"\(.*$|\{[^}]*\}", "").Split('.')[^1];
        return all.Where(m => m != method && m.Identifier.Text == name).Select(m => Summary(m, all)).FirstOrDefault(s => s.Length > 0) ?? "";
    }

    private static XmlElementSyntax? Element(DocumentationCommentTriviaSyntax docs, string name) =>
        docs.Content.OfType<XmlElementSyntax>().FirstOrDefault(e => e.StartTag.Name.ToString() == name);

    private static string Text(SyntaxList<XmlNodeSyntax> nodes) => string.Concat(nodes.Select(node => node switch
    {
        XmlTextSyntax text => string.Concat(text.TextTokens.Select(t => t.ValueText)),
        XmlEmptyElementSyntax empty => Reference(empty),
        XmlElementSyntax element => Text(element.Content),
        _ => "",
    }));

    // What a see, paramref or langword element refers to, as its name, the parameters and type
    // arguments of a cref left out.
    private static string Reference(XmlEmptyElementSyntax element)
    {
        foreach (var attribute in element.Attributes)
        {
            switch (attribute)
            {
                case XmlCrefAttributeSyntax cref:
                    return Regex.Replace(cref.Cref.ToString(), @"\(.*$|\{[^}]*\}", "");
                case XmlNameAttributeSyntax name:
                    return name.Identifier.ToString();
                case XmlTextAttributeSyntax text when text.Name.ToString() == "langword":
                    return string.Concat(text.TextTokens.Select(t => t.ValueText));
            }
        }
        return "";
    }

    private static string Spaced(string text) => Regex.Replace(text, @"\s+", " ");

    [GeneratedRegex(@"^<!-- written by build/cheatsheet\.sh from (?<files>[\w.]+(?:, [\w.]+)*) -->$")]
    private static partial Regex Marker();

    // A full stop ending a sentence, after a word, a number, a bracket or a quote, before a space.
    [GeneratedRegex(@"(?<=[a-z0-9)\]`'""])\. ")]
    private static partial Regex Sentence();
}

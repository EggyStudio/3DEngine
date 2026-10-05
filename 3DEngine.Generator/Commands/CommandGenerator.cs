using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine;

/// <summary>
/// Roslyn incremental generator turning <c>[Command]</c> static methods into console commands
/// registered by a module initializer, so a command needs no registration code and nothing scans
/// for commands at runtime.
/// </summary>
/// <remarks>
/// Each command's runner reads its words with the parsers emitted beside it and answers a word that
/// does not parse with a sentence. A method the generator cannot call is reported on the method
/// (E3D100 to E3D103) and left out.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class CommandGenerator : IIncrementalGenerator
{
    private const string Category = "Commands";

    private static readonly DiagnosticDescriptor NotStatic = new(
        "E3D100", "A console command must be static", "'{0}' is marked [Command] but is not static",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NotReachable = new(
        "E3D101", "A console command must be public or internal",
        "'{0}' is marked [Command] but is not public or internal, so the generated registration cannot call it",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor WrongReturn = new(
        "E3D102", "A console command returns a string or nothing",
        "'{0}' is marked [Command] and must return string or void",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor WrongParameter = new(
        "E3D103", "A console command's parameter has a type the console cannot read",
        "'{0}' takes a {1}, and a console command's parameters are string, bool, int, long, float or double",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private const string Command = "Engine.CommandAttribute";

    /// <summary>Every attribute this generator reads, by full name.</summary>
    public static IReadOnlyList<string> Attributes { get; } = [Command];

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var commands = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                Command,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => Extract(ctx))
            .Where(static command => command is not null)
            .Collect();

        context.RegisterSourceOutput(commands, static (spc, found) =>
        {
            var models = new List<CommandModel>();
            foreach (var command in found)
            {
                if (command is null) continue;
                if (command.Diagnostic is { } diagnostic)
                {
                    spc.ReportDiagnostic(diagnostic);
                    continue;
                }
                models.Add(command);
            }

            if (models.Count > 0)
                spc.AddSource("ConsoleCommandRegistration.g.cs", Emit(models));
        });
    }

    // A parameter, and for one with a default the C# literal it takes when the words leave it off.
    private sealed record ParameterModel(string Name, string Kind, string? Default = null);

    private sealed record CommandModel(
        string Name, string Help, string Usage, string Call, ParameterModel[] Parameters,
        bool ReturnsText, bool TakesLine, Diagnostic? Diagnostic = null);

    private static CommandModel? Extract(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not IMethodSymbol method) return null;

        var attribute = context.Attributes.FirstOrDefault();
        var written = attribute?.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
        var help = attribute?.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value as string ?? "" : "";
        var name = string.IsNullOrWhiteSpace(written) ? method.Name.ToLowerInvariant() : written!.Trim();
        var location = method.Locations.FirstOrDefault() ?? Location.None;

        if (!method.IsStatic) return Refused(name, Diagnostic.Create(NotStatic, location, method.Name));
        if (method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal) ||
            method.ContainingType.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
            return Refused(name, Diagnostic.Create(NotReachable, location, method.Name));

        var returnsText = method.ReturnType.SpecialType == SpecialType.System_String;
        if (!returnsText && !method.ReturnsVoid)
            return Refused(name, Diagnostic.Create(WrongReturn, location, method.Name));

        var parameters = new List<ParameterModel>();
        foreach (var parameter in method.Parameters)
        {
            if (Reader(parameter.Type) is not { } reader)
                return Refused(name, Diagnostic.Create(WrongParameter, location, method.Name, parameter.Type.ToDisplayString()));
            parameters.Add(new ParameterModel(parameter.Name, reader,
                parameter.HasExplicitDefaultValue ? Literal(parameter.ExplicitDefaultValue) : null));
        }

        return new CommandModel(
            name, help,
            string.Join(" ", parameters.Select(p => p.Default is null ? "<" + p.Name + ">" : "[" + p.Name + "]")),
            method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + method.Name,
            parameters.ToArray(),
            returnsText,
            TakesLine: parameters.Count == 1 && parameters[0].Kind == "text");
    }

    // A default value as C# source, in the invariant culture, so a command reads the same everywhere.
    private static string Literal(object? value) => value switch
    {
        null => "default",
        string text => Quote(text),
        bool flag => flag ? "true" : "false",
        // NaN and the infinities have no literal, and printed as numbers are names nothing declares.
        float single when float.IsNaN(single) => "float.NaN",
        float single when float.IsInfinity(single) => single > 0 ? "float.PositiveInfinity" : "float.NegativeInfinity",
        double number when double.IsNaN(number) => "double.NaN",
        double number when double.IsInfinity(number) => number > 0 ? "double.PositiveInfinity" : "double.NegativeInfinity",
        float single => single.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "f",
        double number => number.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "d",
        long whole => whole.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!,
    };

    private static CommandModel Refused(string name, Diagnostic diagnostic) =>
        new(name, "", "", "", [], false, false, diagnostic);

    private static string? Reader(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_String => "text",
        SpecialType.System_Boolean => "flag",
        SpecialType.System_Int32 => "whole",
        SpecialType.System_Int64 => "long",
        SpecialType.System_Single => "single",
        SpecialType.System_Double => "number",
        _ => null,
    };

    private static string Emit(List<CommandModel> models)
    {
        var source = new StringBuilder(4096);
        source.Append("// <auto-generated />\n#nullable enable\n\nnamespace Engine.Generated;\n\n")
            .Append("internal static class ConsoleCommandRegistration\n{\n")
            .Append("    [global::System.Runtime.CompilerServices.ModuleInitializer]\n")
            .Append("    internal static void Initialize()\n    {\n");

        foreach (var model in models.OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            source.Append("        global::Engine.ConsoleCommands.Add(new global::Engine.ConsoleCommand(")
                .Append(Quote(model.Name)).Append(", ").Append(Quote(model.Help)).Append(", ").Append(Quote(model.Usage))
                .Append(", static words =>\n        {\n");
            EmitBody(source, model);
            source.Append("        })");

            if (model.Parameters.Length == 0)
            {
                source.Append(");\n\n");
                continue;
            }

            source.Append("\n        {\n            Parameters =\n            [\n");
            foreach (var parameter in model.Parameters)
                source.Append("                new global::Engine.CommandParameter(").Append(Quote(parameter.Name)).Append(", ")
                    .Append(Quote(parameter.Kind)).Append(", ").Append(model.TakesLine ? "true" : "false").Append("),\n");
            source.Append("            ],\n        });\n\n");
        }

        source.Append("    }\n");
        EmitReaders(source);
        source.Append("}\n");
        return source.ToString();
    }

    private static void EmitBody(StringBuilder source, CommandModel model)
    {
        var parameters = model.Parameters;
        if (model.TakesLine)
        {
            source.Append("            var line = string.Join(\" \", words);\n            ");
            if (model.ReturnsText) source.Append("return ");
            source.Append(model.Call).Append("(line);\n");
            if (!model.ReturnsText) source.Append("            return null;\n");
            return;
        }

        var required = parameters.Count(p => p.Default is null);
        if (required > 0)
            source.Append("            if (words.Length < ").Append(required).Append(") return \"needs ")
                .Append(required).Append(required == 1 ? " argument: " : " arguments: ")
                .Append(Escape(model.Usage)).Append("\";\n");

        // A parameter with a default takes it when the words stop before it.
        for (var i = 0; i < parameters.Length; i++)
        {
            var read = "!Read" + Title(parameters[i].Kind) + "(words[" + i + "], out var argument" + i + ")";
            if (parameters[i].Default is { } fallback)
                source.Append("            var argument").Append(i).Append(" = ").Append(fallback).Append(";\n")
                    .Append("            if (words.Length > ").Append(i).Append(" && !Read").Append(Title(parameters[i].Kind))
                    .Append("(words[").Append(i).Append("], out argument").Append(i).Append(")) return $\"not a ")
                    .Append(parameters[i].Kind).Append(": {words[").Append(i).Append("]}\";\n");
            else
                source.Append("            if (").Append(read).Append(") return $\"not a ").Append(parameters[i].Kind)
                    .Append(": {words[").Append(i).Append("]}\";\n");
        }

        source.Append("            ");
        if (model.ReturnsText) source.Append("return ");
        source.Append(model.Call).Append('(').Append(string.Join(", ", parameters.Select((_, i) => "argument" + i))).Append(");\n");
        if (!model.ReturnsText) source.Append("            return null;\n");
    }

    private static void EmitReaders(StringBuilder source) => source
        .Append("\n    private static bool ReadText(string word, out string value) { value = word; return true; }\n")
        .Append("\n    private static bool ReadFlag(string word, out bool value)\n    {\n")
        .Append("        switch (word.ToLowerInvariant())\n        {\n")
        .Append("            case \"1\" or \"on\" or \"true\" or \"yes\": value = true; return true;\n")
        .Append("            case \"0\" or \"off\" or \"false\" or \"no\": value = false; return true;\n")
        .Append("            default: value = false; return false;\n        }\n    }\n")
        .Append("\n    private static bool ReadWhole(string word, out int value) =>\n")
        .Append("        int.TryParse(word, global::System.Globalization.NumberStyles.Integer, global::System.Globalization.CultureInfo.InvariantCulture, out value);\n")
        .Append("\n    private static bool ReadLong(string word, out long value) =>\n")
        .Append("        long.TryParse(word, global::System.Globalization.NumberStyles.Integer, global::System.Globalization.CultureInfo.InvariantCulture, out value);\n")
        .Append("\n    private static bool ReadSingle(string word, out float value) =>\n")
        .Append("        float.TryParse(word, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out value);\n")
        .Append("\n    private static bool ReadNumber(string word, out double value) =>\n")
        .Append("        double.TryParse(word, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out value);\n");

    private static string Title(string reader) => char.ToUpperInvariant(reader[0]) + reader.Substring(1);

    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Quote(string text) => "\"" + Escape(text) + "\"";
}

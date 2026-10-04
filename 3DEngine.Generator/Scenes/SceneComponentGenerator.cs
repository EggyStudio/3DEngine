using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine;

/// <summary>
/// Roslyn incremental generator writing the JSON a scene file holds for each component type marked
/// <c>[SceneComponent]</c> and each <c>[Behavior]</c>, and registering it with
/// <c>SceneComponents</c> from a module initializer, so a scene is saved and loaded with no
/// reflection.
/// </summary>
/// <remarks>
/// Every public instance field that is not read-only is written by name when its type is one a
/// scene file holds: <c>bool</c>, the whole and real number types, <c>string</c>, an enum (by
/// name), <c>Vector2</c>, <c>Vector3</c>, <c>Vector4</c>, <c>Quaternion</c>, <c>Matrix4x4</c>,
/// <c>Color</c>, <c>Entity</c> (by scene id), <c>Handle&lt;T&gt;</c> (by asset path), a nullable
/// of one of those, or an array of one, as a mesh's positions are. Other fields are left out, so a
/// component holding a runtime handle is saved without it rather than refused. A component starts
/// from its static <c>Default</c> or <c>Identity</c> when it has one, so a field missing from an
/// older file keeps a sensible value.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class SceneComponentGenerator : IIncrementalGenerator
{
    private const string SceneComponent = "Engine.SceneComponentAttribute";
    private const string Behavior = "Engine.BehaviorAttribute";

    /// <summary>Every attribute this generator reads, by full name.</summary>
    public static IReadOnlyList<string> Attributes { get; } = [SceneComponent, Behavior];

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var marked = context.SyntaxProvider
            .ForAttributeWithMetadataName(SceneComponent,
                static (node, _) => node is StructDeclarationSyntax,
                static (ctx, _) => Model(ctx.TargetSymbol as INamedTypeSymbol))
            .Where(static m => m is not null)
            .Collect();

        var behaviors = context.SyntaxProvider
            .ForAttributeWithMetadataName(Behavior,
                static (node, _) => node is StructDeclarationSyntax,
                static (ctx, _) => Model(ctx.TargetSymbol as INamedTypeSymbol))
            .Where(static m => m is not null)
            .Collect();

        context.RegisterSourceOutput(marked.Combine(behaviors), static (spc, pair) =>
        {
            var models = pair.Left.Concat(pair.Right)
                .Where(m => m is not null)
                .GroupBy(m => m!.Type)
                .Select(g => g.First()!)
                .OrderBy(m => m.Type, StringComparer.Ordinal)
                .ToList();
            if (models.Count > 0)
                spc.AddSource("SceneComponentRegistration.g.cs", Emit(models));
        });
    }

    private sealed record FieldModel(string Name, string Write, string Read);

    private sealed record ComponentModel(string Type, string Name, string Start, FieldModel[] Fields);

    private static ComponentModel? Model(INamedTypeSymbol? type)
    {
        if (type is null || type.IsGenericType) return null;
        if (!Reachable(type)) return null;

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var start = StaticOfItself(type, "Default") ?? StaticOfItself(type, "Identity") ?? $"default({fqn})";

        var fields = new List<FieldModel>();
        foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsStatic || field.IsConst || field.IsReadOnly || field.DeclaredAccessibility != Accessibility.Public) continue;
            if (Field(field.Name, field.Type) is { } model) fields.Add(model);
        }

        return new ComponentModel(fqn, type.Name, start, fields.ToArray());
    }

    // Generated code lives in the type's assembly, so internal is enough, but a nested type must be
    // reachable through every type around it.
    private static bool Reachable(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.ContainingType)
            if (t.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)) return false;
        return true;
    }

    private static string? StaticOfItself(INamedTypeSymbol type, string name)
    {
        foreach (var member in type.GetMembers(name))
        {
            if (!member.IsStatic || member.DeclaredAccessibility != Accessibility.Public) continue;
            var memberType = member switch { IPropertySymbol p => p.Type, IFieldSymbol f => f.Type, _ => null };
            if (memberType is not null && SymbolEqualityComparer.Default.Equals(memberType, type))
                return $"{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{name}";
        }
        return null;
    }

    // How one field is written into the open object and read back from it, or null for a type a
    // scene file does not hold. The write reads v.Field, the read assigns value.Field from p.
    private static FieldModel? Field(string name, ITypeSymbol type)
    {
        var key = Quote(name);
        var value = $"v.{name}";

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            var inner = nullable.TypeArguments[0];
            if (Value(inner, key, $"{value}.Value", "p") is not { } innerParts) return null;
            return new FieldModel(name,
                $"if ({value}.HasValue) {{ {innerParts.Write} }} else w.WriteNull({key});",
                $"value.{name} = p.ValueKind == global::System.Text.Json.JsonValueKind.Null ? null : {innerParts.Read};");
        }

        // An array is a JSON array of its elements, each written as a field of its type is but with
        // no name, and read back into a new array of the length the file has.
        if (type is IArrayTypeSymbol { Rank: 1 } array)
        {
            if (Value(array.ElementType, null, "item", "q") is not { } itemParts) return null;
            var element = array.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new FieldModel(name,
                $"if ({value} is null) w.WriteNull({key}); else {{ w.WriteStartArray({key}); foreach (var item in {value}) {{ {itemParts.Write} }} w.WriteEndArray(); }}",
                $"if (p.ValueKind == global::System.Text.Json.JsonValueKind.Null) value.{name} = null!; else {{ var items = new {element}[p.GetArrayLength()]; var index = 0; foreach (var q in p.EnumerateArray()) items[index++] = {itemParts.Read}; value.{name} = items; }}");
        }

        return Value(type, key, value, "p") is { } parts
            ? new FieldModel(name, parts.Write, $"value.{name} = {parts.Read};")
            : null;
    }

    // How one value is written and read. With a key it is a named property of the open object, and
    // without one an element of the open array.
    private static (string Write, string Read)? Value(ITypeSymbol type, string? key, string value, string p)
    {
        string Json(string method, string argument) =>
            key is null ? $"w.{method}Value({argument});" : $"w.{method}({key}, {argument});";
        var named = key ?? "null";

        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean: return (Json("WriteBoolean", value), $"{p}.GetBoolean()");
            case SpecialType.System_Byte: return (Json("WriteNumber", value), $"{p}.GetByte()");
            case SpecialType.System_Int16: return (Json("WriteNumber", value), $"{p}.GetInt16()");
            case SpecialType.System_Int32: return (Json("WriteNumber", value), $"{p}.GetInt32()");
            case SpecialType.System_UInt32: return (Json("WriteNumber", value), $"{p}.GetUInt32()");
            case SpecialType.System_Int64: return (Json("WriteNumber", value), $"{p}.GetInt64()");
            case SpecialType.System_UInt64: return (Json("WriteNumber", value), $"{p}.GetUInt64()");
            case SpecialType.System_Single: return (Json("WriteNumber", value), $"{p}.GetSingle()");
            case SpecialType.System_Double: return (Json("WriteNumber", value), $"{p}.GetDouble()");
            case SpecialType.System_String: return (Json("WriteString", value), $"{p}.GetString()!");
        }

        var display = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (type.TypeKind == TypeKind.Enum)
            return (Json("WriteString", $"{value}.ToString()"), $"global::System.Enum.Parse<{display}>({p}.GetString()!)");

        switch (display)
        {
            case "global::System.Numerics.Vector2": return ($"global::Engine.SceneJson.Write(w, {named}, {value});", $"global::Engine.SceneJson.ReadVector2({p})");
            case "global::System.Numerics.Vector3": return ($"global::Engine.SceneJson.Write(w, {named}, {value});", $"global::Engine.SceneJson.ReadVector3({p})");
            case "global::System.Numerics.Vector4": return ($"global::Engine.SceneJson.Write(w, {named}, {value});", $"global::Engine.SceneJson.ReadVector4({p})");
            case "global::System.Numerics.Quaternion": return ($"global::Engine.SceneJson.Write(w, {named}, {value});", $"global::Engine.SceneJson.ReadQuaternion({p})");
            case "global::System.Numerics.Matrix4x4": return ($"global::Engine.SceneJson.Write(w, {named}, {value});", $"global::Engine.SceneJson.ReadMatrix4x4({p})");
            case "global::Engine.Color": return ($"global::Engine.SceneJson.Write(w, {named}, {value});", $"global::Engine.SceneJson.ReadColor({p})");
            case "global::Engine.Entity": return (Json("WriteString", $"ctx.IdOf({value})"), $"ctx.Resolve({p}.GetString())");
        }

        if (type is INamedTypeSymbol { IsGenericType: true } generic &&
            generic.OriginalDefinition.ToDisplayString() == "Engine.Handle<T>")
        {
            var asset = generic.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return (Json("WriteString", $"ctx.PathOf({value})"), $"ctx.Load<{asset}>({p}.GetString())");
        }

        return null;
    }

    private static string Emit(List<ComponentModel> models)
    {
        var source = new StringBuilder(8192);
        source.Append("// <auto-generated />\n#nullable enable\n\nnamespace Engine.Generated;\n\n")
            .Append("internal static class SceneComponentRegistration\n{\n")
            .Append("    [global::System.Runtime.CompilerServices.ModuleInitializer]\n")
            .Append("    internal static void Initialize()\n    {\n");

        foreach (var model in models)
        {
            source.Append("        global::Engine.SceneComponents.Add(new global::Engine.SceneCodec<").Append(model.Type).Append(">(")
                .Append(Quote(model.Name)).Append(",\n")
                .Append("            static (global::System.Text.Json.Utf8JsonWriter w, in ").Append(model.Type)
                .Append(" v, global::Engine.SceneWriteContext ctx) =>\n            {\n");
            foreach (var field in model.Fields)
                source.Append("                ").Append(field.Write).Append('\n');
            source.Append("            },\n")
                .Append("            static (e, ctx) =>\n            {\n")
                .Append("                var value = ").Append(model.Start).Append(";\n");
            foreach (var field in model.Fields)
                source.Append("                if (e.TryGetProperty(").Append(Quote(field.Name)).Append(", out var p_").Append(field.Name).Append(")) { var p = p_").Append(field.Name).Append("; ")
                    .Append(field.Read).Append(" }\n");
            source.Append("                return value;\n            }));\n");
        }

        source.Append("    }\n}\n");
        return source.ToString();
    }

    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

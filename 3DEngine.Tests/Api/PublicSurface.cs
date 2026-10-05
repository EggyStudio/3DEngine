using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Engine.Tests.Api;

/// <summary>
/// Writes every public type of an assembly and every member a game can reach on it, read from the
/// built assembly, as lines of text in a fixed order, so two builds' surfaces compare line by line.
/// </summary>
/// <remarks>
/// A type is a line of its own, with what it derives from, and its members follow it indented,
/// each with the modifiers, types, names and default values a caller depends on. Types are named
/// without their namespace inside a signature, since the header line names it once. Protected
/// members of a type that can be derived from are listed, as a game's subclass reaches them.
/// Nothing compiler-generated is listed, nor anything private or internal.
/// </remarks>
internal static class PublicSurface
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static string Write(Assembly assembly)
    {
        var text = new StringBuilder();
        foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            text.Append(Header(type)).Append('\n');
            foreach (var member in Members(type).Order(StringComparer.Ordinal)) text.Append("  ").Append(member).Append('\n');
        }
        return text.ToString();
    }

    private static string Header(Type type)
    {
        var name = FullName(type);
        if (type.IsEnum) return $"enum {name} : {Name(Enum.GetUnderlyingType(type))}";
        if (typeof(Delegate).IsAssignableFrom(type) && type != typeof(Delegate) && type.BaseType == typeof(MulticastDelegate))
        {
            var invoke = type.GetMethod("Invoke")!;
            return $"delegate {Returned(invoke)} {name}({Parameters(invoke.GetParameters())})";
        }

        string kind;
        if (type.IsInterface) kind = "interface";
        else if (type.IsValueType) kind = (type.IsDefined(typeof(IsReadOnlyAttribute)) ? "readonly " : "") + "struct";
        else if (type.IsAbstract && type.IsSealed) kind = "static class";
        else kind = (type.IsAbstract ? "abstract " : type.IsSealed ? "sealed " : "") + "class";

        var bases = new List<string>();
        if (type.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType)) bases.Add(Name(baseType));
        bases.AddRange(type.GetInterfaces().Select(i => Name(i)).Order(StringComparer.Ordinal));
        return $"{kind} {name}{(bases.Count > 0 ? " : " + string.Join(", ", bases) : "")}{Constraints(type.IsGenericTypeDefinition ? type.GetGenericArguments() : [])}";
    }

    private static IEnumerable<string> Members(Type type)
    {
        if (type.IsEnum)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                yield return $"{field.Name} = {Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture)}";
            yield break;
        }
        if (typeof(MulticastDelegate).IsAssignableFrom(type)) yield break;

        // Accessors are listed as their property or event.
        var accessors = new HashSet<MethodInfo>();
        foreach (var property in type.GetProperties(Declared))
        {
            var get = Visible(property.GetMethod) ? property.GetMethod : null;
            var set = Visible(property.SetMethod) ? property.SetMethod : null;
            if (property.GetMethod is { } g) accessors.Add(g);
            if (property.SetMethod is { } s) accessors.Add(s);
            if ((get ?? set) is not { } any || Generated(property.Name)) continue;
            var init = set is not null && set.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
            var parts = (get is not null ? Access(get, any) + "get; " : "") + (set is not null ? Access(set, any) + (init ? "init; " : "set; ") : "");
            var indexes = property.GetIndexParameters();
            var name = indexes.Length > 0 ? $"this[{Parameters(indexes)}]" : property.Name;
            yield return $"{Modifiers(any, type)}{Name(property.PropertyType, Nullability.Create(property))} {name} {{ {parts}}}";
        }
        foreach (var @event in type.GetEvents(Declared))
        {
            if (@event.AddMethod is { } add) accessors.Add(add);
            if (@event.RemoveMethod is { } remove) accessors.Add(remove);
            if (!Visible(@event.AddMethod)) continue;
            yield return $"{Modifiers(@event.AddMethod!, type)}event {Name(@event.EventHandlerType!, Nullability.Create(@event))} {@event.Name}";
        }
        foreach (var field in type.GetFields(Declared))
        {
            if (!(field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly) || Generated(field.Name)) continue;
            var access = field.IsPublic ? "" : "protected ";
            var fieldType = Name(field.FieldType, Nullability.Create(field));
            if (field.IsLiteral) yield return $"{access}const {fieldType} {field.Name} = {Value(field.GetRawConstantValue(), field.FieldType)}";
            else yield return $"{access}{(field.IsStatic ? "static " : "")}{(field.IsInitOnly ? "readonly " : "")}{fieldType} {field.Name}";
        }
        foreach (var constructor in type.GetConstructors(Declared))
        {
            if (!Visible(constructor) || constructor.IsStatic) continue;
            yield return $"{Access(constructor)}{Simple(type)}({Parameters(constructor.GetParameters())})";
        }
        foreach (var method in type.GetMethods(Declared))
        {
            if (!Visible(method) || accessors.Contains(method) || Generated(method.Name)) continue;
            var generic = method.IsGenericMethodDefinition ? "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + ">" : "";
            var constraints = method.IsGenericMethodDefinition ? Constraints(method.GetGenericArguments()) : "";
            yield return $"{Modifiers(method, type)}{Returned(method)} {method.Name}{generic}({Parameters(method.GetParameters(), Extension(method))}){constraints}";
        }
    }

    private static bool Visible(MethodBase? method) => method is not null && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);

    // Names the compiler makes up, such as a record's clone method, which nothing calls by name.
    private static bool Generated(string name) => name.Contains('<');

    private static bool Extension(MethodBase method) => method.IsDefined(typeof(ExtensionAttribute));

    private static string Access(MethodBase method, MethodBase? of = null) =>
        method.IsPublic || (of is not null && !of.IsPublic) ? "" : "protected ";

    private static string Modifiers(MethodBase method, Type owner)
    {
        var text = Access(method);
        if (owner.IsInterface) return text + (method.IsStatic ? "static " : "");
        if (method.IsStatic) return text + "static ";
        if (method.IsAbstract) return text + "abstract ";
        if (method is MethodInfo { IsVirtual: true } info && !info.IsFinal)
            return text + (info.GetBaseDefinition() == info ? "virtual " : "override ");
        if (method is MethodInfo { IsVirtual: true, IsFinal: true } sealedInfo && sealedInfo.GetBaseDefinition() != sealedInfo && !sealedInfo.DeclaringType!.IsSealed)
            return text + "sealed override ";
        return text;
    }

    private static string Returned(MethodInfo method)
    {
        var parameter = method.ReturnParameter;
        var prefix = parameter.ParameterType.IsByRef ? (method.ReturnParameter.IsDefined(typeof(IsReadOnlyAttribute)) ? "ref readonly " : "ref ") : "";
        return prefix + Name(parameter.ParameterType, Nullability.Create(parameter));
    }

    private static string Parameters(ParameterInfo[] parameters, bool extension = false) =>
        string.Join(", ", parameters.Select((p, i) =>
        {
            var type = p.ParameterType;
            var prefix = i == 0 && extension ? "this " : "";
            if (type.IsByRef) prefix += p.IsOut ? "out " : p.IsIn ? "in " : "ref ";
            if (p.IsDefined(typeof(ParamArrayAttribute))) prefix += "params ";
            var text = $"{prefix}{Name(type, Nullability.Create(p))} {p.Name}";
            if (p.HasDefaultValue) text += " = " + Value(p.RawDefaultValue, type);
            return text;
        }));

    private static string Value(object? value, Type type)
    {
        type = type.IsByRef ? type.GetElementType()! : type;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return value switch
        {
            null or DBNull or Missing => type.IsValueType && Nullable.GetUnderlyingType(type) is null ? "default" : "null",
            string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
            char c => $"'{c}'",
            bool b => b ? "true" : "false",
            _ when underlying.IsEnum => Enum.ToObject(underlying, value) is var e && Enum.IsDefined(underlying, e) ? $"{underlying.Name}.{e}" : $"({underlying.Name}){Convert.ToString(value, CultureInfo.InvariantCulture)}",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) + value switch { float => "f", double => "d", decimal => "m", long => "L", uint => "u", ulong => "UL", _ => "" },
            _ => value.ToString() ?? "",
        };
    }

    // A type's namespace and the types it is nested in, each with its own generic parameters.
    private static string FullName(Type type)
    {
        var outer = type.IsNested ? FullName(type.DeclaringType!) : type.Namespace;
        // A nested type's own parameters follow its declaring type's.
        var own = type.GetGenericArguments().Skip(type.DeclaringType?.GetGenericArguments().Length ?? 0).ToArray();
        var name = Simple(type) + (own.Length == 0 ? "" : "<" + string.Join(", ", own.Select(a => a.Name)) + ">");
        return string.IsNullOrEmpty(outer) ? name : outer + "." + name;
    }

    private static string Constraints(Type[] arguments)
    {
        var text = new StringBuilder();
        foreach (var argument in arguments.Where(a => a.IsGenericParameter))
        {
            var parts = new List<string>();
            var attributes = argument.GenericParameterAttributes;
            var unmanaged = argument.GetCustomAttributes().Any(a => a.GetType().Name == "IsUnmanagedAttribute");
            if (unmanaged) parts.Add("unmanaged");
            else if (attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint)) parts.Add("struct");
            else if (attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint)) parts.Add("class");
            parts.AddRange(argument.GetGenericParameterConstraints().Where(c => c != typeof(ValueType)).Select(c => Name(c)).Order(StringComparer.Ordinal));
            if (attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) && !attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint)) parts.Add("new()");
            if (parts.Count > 0) text.Append($" where {argument.Name} : {string.Join(", ", parts)}");
        }
        return text.ToString();
    }

    private static readonly Dictionary<Type, string> Keywords = new()
    {
        [typeof(void)] = "void", [typeof(bool)] = "bool", [typeof(byte)] = "byte", [typeof(sbyte)] = "sbyte",
        [typeof(short)] = "short", [typeof(ushort)] = "ushort", [typeof(int)] = "int", [typeof(uint)] = "uint",
        [typeof(long)] = "long", [typeof(ulong)] = "ulong", [typeof(float)] = "float", [typeof(double)] = "double",
        [typeof(decimal)] = "decimal", [typeof(char)] = "char", [typeof(string)] = "string", [typeof(object)] = "object",
        [typeof(nint)] = "nint", [typeof(nuint)] = "nuint",
    };

    // A type as a signature spells it, with the nullability the compiler recorded for a reference.
    private static string Name(Type type, NullabilityInfo? nullability = null)
    {
        if (type.IsByRef) return Name(type.GetElementType()!, nullability);
        if (type.IsPointer) return Name(type.GetElementType()!) + "*";
        if (type.IsArray) return Name(type.GetElementType()!, nullability?.ElementType) + "[" + new string(',', type.GetArrayRank() - 1) + "]" + Mark(type, nullability);
        if (Nullable.GetUnderlyingType(type) is { } inner) return Name(inner) + "?";
        if (type.IsGenericParameter) return type.Name + Mark(type, nullability);
        if (Keywords.TryGetValue(type, out var keyword)) return keyword + Mark(type, nullability);

        var arguments = type.GetGenericArguments();
        string name;
        if (type.IsGenericType && type.Namespace == "System" && type.Name.StartsWith("ValueTuple`", StringComparison.Ordinal))
            name = "(" + string.Join(", ", arguments.Select((a, i) => Name(a, Argument(nullability, i)))) + ")";
        else
        {
            var used = 0;
            name = Simple(type, arguments, nullability, ref used);
        }
        return name + Mark(type, nullability);
    }

    // A nested type with the types it is declared in, each with its own share of the generic arguments.
    private static string Simple(Type type, Type[] arguments, NullabilityInfo? nullability, ref int used)
    {
        var prefix = type.IsNested && !type.IsGenericParameter ? Simple(type.DeclaringType!, arguments, nullability, ref used) + "." : "";
        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick < 0) return prefix + name;
        var count = int.Parse(name[(tick + 1)..], CultureInfo.InvariantCulture);
        var start = used;
        used += count;
        var own = Enumerable.Range(start, count).Select(i => i < arguments.Length ? Name(arguments[i], Argument(nullability, i)) : "?");
        return prefix + name[..tick] + "<" + string.Join(", ", own) + ">";
    }

    private static string Simple(Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`');
        return tick < 0 ? name : name[..tick];
    }

    private static NullabilityInfo? Argument(NullabilityInfo? nullability, int index) =>
        nullability is not null && index < nullability.GenericTypeArguments.Length ? nullability.GenericTypeArguments[index] : null;

    private static string Mark(Type type, NullabilityInfo? nullability) =>
        !type.IsValueType && nullability?.ReadState == NullabilityState.Nullable ? "?" : "";

    private static class Nullability
    {
        private static readonly NullabilityInfoContext Context = new();

        public static NullabilityInfo? Create(ParameterInfo parameter) => Locked(() => Context.Create(parameter));
        public static NullabilityInfo? Create(PropertyInfo property) => Locked(() => Context.Create(property));
        public static NullabilityInfo? Create(FieldInfo field) => Locked(() => Context.Create(field));
        public static NullabilityInfo? Create(EventInfo @event) => Locked(() => Context.Create(@event));

        // The context caches what it has read and is not safe across threads, which tests run on.
        private static NullabilityInfo? Locked(Func<NullabilityInfo> create)
        {
            lock (Context) return create();
        }
    }
}

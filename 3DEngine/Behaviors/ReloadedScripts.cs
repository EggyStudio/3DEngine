using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// Moves what a script's last generation left in the world onto the types its new generation
/// declares, so a script edited while its game is played leaves the game where it was.
/// </summary>
/// <remarks>
/// <para>
/// A behavior script compiled again is a new assembly, so its <c>Ball</c> is a new type with the old
/// one's name. The entities carrying the old <c>Ball</c> would keep it, and the new generation's
/// systems, which query for the new one, would find none of them, so the game would stop where the
/// script changed, and the old generation would stay loaded for as long as its components did.
/// </para>
/// <para>
/// Each component and resource of a type the last generation declared is made again as the new
/// generation's type of the same name, field by field by name, so a field kept keeps its value,
/// one added starts as the new type's constructor leaves it, and one taken away is dropped. A field of
/// the engine's, the program's or .NET's types, an <see cref="Entity"/> among them, is carried as
/// it is, one of a type the script declares is made again the same way, an enum by the name of its
/// value, and an array or a list element by element. A component or resource whose type the new
/// generation no longer declares is removed, since nothing could read it and it would keep the last
/// generation loaded. A script's static fields start again, as its types do. A state machine is the
/// program's, declared on its own enum, which a reload leaves as it is.
/// </para>
/// </remarks>
internal static class ReloadedScripts
{
    // Why reading and writing a script's types by reflection is safe where a build is trimmed (N 2.5).
    private const string ScriptTypes = "Only the types of assemblies compiled from scripts while the program runs are read, which no "
        + "trimmer has seen or cut, and a native build, which cannot load one, never starts the compiler that calls this.";

    /// <summary>What a carry moved and what it dropped.</summary>
    /// <param name="Components">Components made again as the new generation's types.</param>
    /// <param name="Resources">Resources made again as the new generation's types.</param>
    /// <param name="Dropped">Components and resources of types the new generation no longer declares.</param>
    internal readonly record struct Carried(int Components, int Resources, int Dropped);

    /// <summary>
    /// Moves every component and resource of a type <paramref name="previous"/> declares onto the
    /// type of the same name in <paramref name="next"/>, or drops it where <paramref name="next"/>
    /// has none or is <c>null</c>, its scripts deleted. Run between frames, on the main thread.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = ScriptTypes)]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = ScriptTypes)]
    internal static Carried Carry(World world, Assembly previous, Assembly? next)
    {
        int components = 0, resources = 0, dropped = 0;
        var made = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);

        if (world.TryGetResource<EcsWorld>(out var ecs))
            foreach (var type in ecs.ComponentTypes.Where(t => t.Assembly == previous))
            {
                var counterpart = next?.GetType(type.FullName!);
                foreach (var entity in ecs.EntitiesOf(type))
                {
                    var old = ecs.GetBoxed(entity, type)!;
                    ecs.RemoveBoxed(entity, type);
                    if (counterpart is not null && Convert(old, counterpart, previous, made) is { } value && ecs.AddBoxed(entity, value)) components++;
                    else dropped++;
                }
                ecs.ForgetStore(type);
            }

        foreach (var type in world.ResourceTypes.Where(t => t.Assembly == previous))
        {
            var old = world.ResourceOf(type);
            world.RemoveResource(type);
            if (old is not null && next?.GetType(type.FullName!) is { } counterpart && Convert(old, counterpart, previous, made) is { } value)
            {
                world.InsertResourceBoxed(value);
                resources++;
            }
            else dropped++;
        }

        return new Carried(components, resources, dropped);
    }

    // The value as the target type has it, or null where it cannot be: a value of a type the last
    // generation declared made again, an array or a list element by element, anything else as it
    // is where the target holds it.
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = ScriptTypes)]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = ScriptTypes)]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = ScriptTypes)]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = ScriptTypes)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = ScriptTypes)]
    private static object? Convert(object? value, Type target, Assembly previous, Dictionary<object, object> made)
    {
        if (value is null) return null;
        var type = value.GetType();
        target = Nullable.GetUnderlyingType(target) ?? target;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
            && target.IsGenericType && target.GetGenericTypeDefinition() == typeof(List<>) && !target.IsInstanceOfType(value))
        {
            var list = (System.Collections.IList)Activator.CreateInstance(target)!;
            made[value] = list;
            var element = target.GetGenericArguments()[0];
            foreach (var item in (System.Collections.IList)value)
                if (Convert(item, element, previous, made) is { } carried) list.Add(carried);
            return list;
        }
        if (type.Assembly != previous && type is not { IsArray: true })
            return target.IsInstanceOfType(value) ? value : null;
        if (!type.IsValueType && made.TryGetValue(value, out var again)) return again;

        if (value is Array array)
        {
            if (!target.IsArray || array.Rank != 1) return null;
            var element = target.GetElementType()!;
            var copy = Array.CreateInstance(element, array.Length);
            made[value] = copy;
            for (int i = 0; i < array.Length; i++)
                if (Convert(array.GetValue(i), element, previous, made) is { } item) copy.SetValue(item, i);
            return copy;
        }

        if (type.FullName != target.FullName) return null;
        if (type.IsEnum)
            return target.IsEnum && Enum.GetName(type, value) is { } name && Enum.TryParse(target, name, out var parsed)
                ? parsed
                : null;

        // Through its constructor where it has one, so a field it starts that the last generation
        // lacked starts as it does.
        var result = (target.IsValueType || target.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) is not null
            ? Activator.CreateInstance(target, nonPublic: true)
            : null) ?? RuntimeHelpers.GetUninitializedObject(target);
        if (!type.IsValueType) made[value] = result;

        var fields = FieldsOf(type).ToDictionary(f => (f.DeclaringType!.FullName, f.Name));
        foreach (var field in FieldsOf(target))
        {
            if (!fields.TryGetValue((field.DeclaringType!.FullName, field.Name), out var source)) continue;
            // A value that cannot be carried leaves the field as the new type's constructor did,
            // so a list it starts with is not taken away.
            var old = source.GetValue(value);
            if (old is null)
            {
                if (!field.FieldType.IsValueType || Nullable.GetUnderlyingType(field.FieldType) is not null) field.SetValue(result, null);
            }
            else if (Convert(old, field.FieldType, previous, made) is { } carried)
                field.SetValue(result, carried);
        }
        return result;
    }

    // Every instance field of a type and the types it derives from, private ones among them.
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = ScriptTypes)]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = ScriptTypes)]
    private static IEnumerable<FieldInfo> FieldsOf(Type type)
    {
        for (var t = type; t is not null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
            foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                yield return field;
    }
}

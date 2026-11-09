using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>The component types a scene file can hold, registered by the generated code of each assembly.</summary>
/// <remarks>
/// A component is written under its type's name, and under its full name when another registered
/// type has the same name, so neither is read back as the other whichever registered first. A file
/// written under a type's former name (<see cref="FormerNameAttribute"/>) is read as that type,
/// where no registered type has the name now.
/// </remarks>
public static class SceneComponents
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, ISceneCodec> ByFullName = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<ISceneCodec>> ByName = new(StringComparer.Ordinal);

    // The codecs by each of their former names, full and alone.
    private static readonly Dictionary<string, List<ISceneCodec>> ByFormerName = new(StringComparer.Ordinal);

    /// <summary>Registers a component type's codec. Registering the same type again replaces it.</summary>
    public static void Add(ISceneCodec codec)
    {
        lock (Gate)
        {
            var fullName = FullName(codec);
            if (ByFullName.Remove(fullName, out var replaced))
            {
                ByName[replaced.Name].Remove(replaced);
                foreach (var former in FormerKeys(replaced)) ByFormerName[former].Remove(replaced);
            }
            ByFullName[fullName] = codec;
            if (!ByName.TryGetValue(codec.Name, out var named)) ByName[codec.Name] = named = [];
            named.Add(codec);
            foreach (var former in FormerKeys(codec))
            {
                if (!ByFormerName.TryGetValue(former, out var formerly)) ByFormerName[former] = formerly = [];
                formerly.Add(codec);
            }
        }
    }

    /// <summary>Every registered codec.</summary>
    public static IReadOnlyList<ISceneCodec> All
    {
        get { lock (Gate) return ByFullName.Values.ToArray(); }
    }

    /// <summary>
    /// The codec written under <paramref name="key"/>, which is a type's full name, or a name that
    /// one of the engine's own types has or that no other registered type shares, or else a former
    /// name, full or alone, that one registered type had. Null for none, or for a name more than one
    /// of a program's types has.
    /// </summary>
    public static ISceneCodec? Find(string key)
    {
        lock (Gate)
        {
            if (ByFullName.TryGetValue(key, out var exact)) return exact;
            if (ByName.TryGetValue(key, out var named) && named.Count > 0) return EngineOwner(named) ?? (named.Count == 1 ? named[0] : null);
            return ByFormerName.TryGetValue(key, out var formerly) && formerly.Count == 1 ? formerly[0] : null;
        }
    }

    // The keys a codec is found by from its former names, each full name and the name alone after
    // its namespace, as a file wrote a component under its name alone unless two shared it.
    private static IEnumerable<string> FormerKeys(ISceneCodec codec) =>
        codec.FormerNames.SelectMany(full => new[] { full, full[(full.LastIndexOfAny(['.', '+']) + 1)..] }).Distinct();

    /// <summary>
    /// The key a codec's components are written under. That is its name, unless another type shares
    /// the name and the name is not this codec's as one of the engine's own types, then its full name.
    /// </summary>
    /// <remarks>
    /// An engine type keeps its name whatever a program registers, so a level saved with the
    /// engine's <c>Light</c> under <c>Light</c> still loads its lights once the game adds a
    /// <c>Light</c> of its own, which is written by its full name instead.
    /// </remarks>
    public static string KeyOf(ISceneCodec codec)
    {
        lock (Gate)
        {
            if (!ByName.TryGetValue(codec.Name, out var named) || named.Count == 1) return codec.Name;
            return ReferenceEquals(EngineOwner(named), codec) ? codec.Name : FullName(codec);
        }
    }

    // The one engine type among the types with a name, or null when there is none or more than one.
    private static ISceneCodec? EngineOwner(List<ISceneCodec> named)
    {
        ISceneCodec? owner = null;
        foreach (var codec in named)
        {
            if (codec.Type.Assembly != typeof(SceneComponents).Assembly) continue;
            if (owner is not null) return null;
            owner = codec;
        }
        return owner;
    }

    private static string FullName(ISceneCodec codec) => codec.Type.FullName ?? codec.Name;
}

using System.Numerics;

namespace Engine;

/// <summary>The four <c>float4</c> values a custom shader reads with <c>param(0)</c> to <c>param(3)</c>.</summary>
public readonly record struct ShaderParams(Vector4 P0, Vector4 P1, Vector4 P2, Vector4 P3)
{
    /// <summary>The value in <paramref name="slot"/>.</summary>
    public Vector4 this[int slot] => slot switch { 0 => P0, 1 => P1, 2 => P2, 3 => P3, _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slots are 0 to 3.") };

    /// <summary>A copy with <paramref name="slot"/> set to <paramref name="value"/>.</summary>
    public ShaderParams With(int slot, Vector4 value) => slot switch
    {
        0 => this with { P0 = value },
        1 => this with { P1 = value },
        2 => this with { P2 = value },
        3 => this with { P3 = value },
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slots are 0 to 3."),
    };
}

/// <summary>
/// The custom shaders the flat API has loaded, by id: their compiled stages, and the ids that have
/// been unloaded, until <see cref="ImmediateRenderer"/> retires their pipelines.
/// </summary>
/// <remarks>Id 0 is never given out, so a default shader handle draws with the engine's own stages.</remarks>
public sealed class ShaderStore
{
    private readonly object _gate = new();
    private readonly Dictionary<int, ShaderProgram> _live = [];
    private readonly List<int> _removals = [];
    private int _next = 1;

    /// <summary>Stores a compiled program and returns its id.</summary>
    /// <exception cref="ArgumentException">The program has no fragment stage.</exception>
    public int Add(ShaderProgram program)
    {
        if (!program.Stages.ContainsKey(ShaderStage.Fragment))
            throw new ArgumentException($"'{program.Name}' has no fragment stage, which a shader for the immediate pass needs.", nameof(program));
        lock (_gate)
        {
            var id = _next++;
            _live[id] = program;
            return id;
        }
    }

    /// <summary>The program with id <paramref name="id"/>, or <c>null</c>.</summary>
    public ShaderProgram? Get(int id)
    {
        lock (_gate) return _live.GetValueOrDefault(id);
    }

    /// <summary>Unloads a shader.</summary>
    public bool Remove(int id)
    {
        lock (_gate)
        {
            if (!_live.Remove(id)) return false;
            _removals.Add(id);
            return true;
        }
    }

    /// <summary>Hands the unloaded ids to the renderer.</summary>
    internal int[] TakeRemovals()
    {
        lock (_gate)
        {
            var taken = _removals.ToArray();
            _removals.Clear();
            return taken;
        }
    }
}

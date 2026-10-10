namespace Engine.Game;

/// <summary>Makes the columns of a world from its seed.</summary>
/// <remarks>
/// <see cref="Generate"/> is called on worker threads, several at once, so a generator holds
/// nothing it changes after it is made, and a column depends on its seed and place alone.
/// </remarks>
public interface IWorldGenerator
{
    /// <summary>The name <c>voxel.world</c> takes for it.</summary>
    string Name { get; }

    int Seed { get; }

    /// <summary>Makes a column, its blocks and its <see cref="Paint"/>ed biomes and tints.</summary>
    ChunkColumn Generate(int columnX, int columnZ);

    /// <summary>
    /// Writes a column's biome and tints at each of its places, which depend on the seed and the
    /// place alone, so a column read back from a save is painted again rather than saved with them.
    /// </summary>
    void Paint(ChunkColumn column);
}

/// <summary>The kinds of world, by the names a save and <c>voxel.world</c> give them.</summary>
public static class Generators
{
    public static readonly string[] Kinds = ["overworld", "flat"];

    /// <summary>The generator of a kind of world from a seed, or null for a kind not known.</summary>
    public static IWorldGenerator? Create(string kind, int seed) => kind.ToLowerInvariant() switch
    {
        "overworld" => new Overworld(seed),
        "flat" => new Superflat(seed),
        _ => null,
    };
}

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

    ChunkColumn Generate(int columnX, int columnZ);
}

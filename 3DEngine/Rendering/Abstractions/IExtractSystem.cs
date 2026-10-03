namespace Engine;

/// <summary>
/// Extracts data from the game world into the render world.
/// Runs before <c>BeginFrame</c> (no GPU frame context available yet).
/// </summary>
/// <seealso cref="Renderer"/>
/// <seealso cref="RenderWorld"/>
public interface IExtractSystem
{
    /// <summary>Copies relevant game-world data into the render world for GPU processing.</summary>
    /// <param name="world">The game world to read from.</param>
    /// <param name="renderWorld">The render world to write extracted data into.</param>
    void Run(World world, RenderWorld renderWorld);
}

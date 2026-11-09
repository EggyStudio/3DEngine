namespace Engine;

/// <summary>
/// Whether the world's renderer draws, which the renderer inserts under this name as well as its
/// own, so the run mode and the command line read it without naming the renderer.
/// </summary>
internal interface IRendererStatus
{
    /// <summary>Whether the renderer's device is up and draws.</summary>
    bool Draws { get; }
}

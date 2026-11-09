namespace Engine;

/// <summary>
/// Whether the renderer draws, inserted beside it under <see cref="IRendererStatus"/> rather than
/// the renderer inserted twice, since the world disposes each resource it holds once a key, and the
/// renderer disposed twice crashed the process.
/// </summary>
internal sealed class RendererStatus(Renderer renderer) : IRendererStatus
{
    public bool Draws => renderer.Context.IsInitialized;
}

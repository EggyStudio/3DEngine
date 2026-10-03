namespace Engine;

/// <summary>
/// Prepares GPU resources (buffers, descriptor sets, textures) from extracted render data.
/// Runs after <c>BeginFrame</c> (fence wait guarantees the in-flight slot is idle)
/// and before graph node execution.
/// </summary>
/// <seealso cref="Renderer"/>
/// <seealso cref="IExtractSystem"/>
public interface IPrepareSystem
{
    /// <summary>Uploads or updates GPU resources using data from the render world.</summary>
    /// <param name="renderWorld">The render world containing extracted data.</param>
    /// <param name="renderContext">The render context providing GPU device and command buffer access.</param>
    void Run(RenderWorld renderWorld, RenderContext renderContext);
}

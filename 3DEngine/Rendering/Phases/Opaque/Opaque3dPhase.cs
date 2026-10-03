namespace Engine;

/// <summary>Typed wrapper for the opaque 3D render phase, stored as a resource in <see cref="RenderWorld"/>.</summary>
/// <seealso cref="OpaquePhaseItem"/>
public sealed class Opaque3dPhase
{
    /// <summary>The underlying render phase.</summary>
    public RenderPhase<OpaquePhaseItem> Phase { get; } = new();
}

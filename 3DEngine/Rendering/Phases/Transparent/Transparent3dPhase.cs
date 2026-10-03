namespace Engine;

/// <summary>Typed wrapper for the transparent 3D render phase, stored as a resource in <see cref="RenderWorld"/>.</summary>
/// <seealso cref="TransparentPhaseItem"/>
public sealed class Transparent3dPhase
{
    /// <summary>The underlying render phase.</summary>
    public RenderPhase<TransparentPhaseItem> Phase { get; } = new();
}

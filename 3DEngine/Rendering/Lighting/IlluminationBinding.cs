namespace Engine;

/// <summary>
/// The probes the model pass reads the window's indirect light from this frame, over the field they
/// were traced through, and the screen's probes, which it reads first where one stands near, with
/// what a glossy surface traces its reflection through, the window's depth, its scene the frame
/// before where one was kept, its meshes for the device's ray tracing where it traces rays, and how
/// many glow lights the frame's lights hold, without which the model pass draws with its build that
/// leaves them out.
/// </summary>
internal sealed record IlluminationBinding(GpuIllumination Probes, GpuSceneField Field, GpuScreenProbes? Screen,
    WindowDepth? Depth = null, GpuReflectionHistory? History = null, GpuRayScene? Rays = null, int GlowLights = 0);

/// <summary>
/// What the model pass reads each render target's indirect light from this frame, by the target's
/// texture id, the world's probes with the target's own screen probes, stood on its own depth,
/// where it draws meshes through a camera of its own.
/// </summary>
internal sealed class TargetIllumination
{
    /// <summary>Each target's binding, set as the target's probes are traced ahead of its pass.</summary>
    public Dictionary<int, IlluminationBinding> ByTarget { get; } = [];
}

using System.Numerics;

namespace Engine;

/// <summary>
/// What the frame's passes ask of global illumination while it runs, set in the render world by its
/// renderer and found there by this type, so the HDR frame, the render targets and the lighting
/// buffer reach it without its renderer, and none where it does not run.
/// </summary>
internal interface IIlluminationPasses
{
    /// <summary>Whether the screen's probes stand for the glow lights alone this frame, as at <see cref="GlobalIllumination.Low"/> while some glow light is lit.</summary>
    bool GlowOnly(RenderWorld renderWorld);

    /// <summary>The window's camera the frame <see cref="KeepFrame"/> last kept the scene of, or null where none is kept.</summary>
    Matrix4x4? HistoryViewProjection { get; }

    /// <summary>
    /// The window's view of that frame, its eye and turn with it, which a reflection reads the kept
    /// scene through against this frame's origin (<see cref="WindowView.UnjitteredAgainst"/>).
    /// </summary>
    WindowView? HistoryView { get; }

    /// <summary>Whether the window's meshes are built for the device's ray tracing this frame.</summary>
    bool TracesRays { get; }

    /// <summary>Keeps the window's scene the HDR frame drew this frame for the reflections of the frame after.</summary>
    void KeepFrame(RenderContext renderContext, RenderWorld renderWorld, IImageView scene, Extent2D extent);

    /// <summary>Traces and holds the screen's probes of render target <paramref name="id"/> ahead of its pass.</summary>
    void DrawTarget(RenderContext renderContext, RenderWorld renderWorld, int id, Extent2D size, Matrix4x4 camera);
}

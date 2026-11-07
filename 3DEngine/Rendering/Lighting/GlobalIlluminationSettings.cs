using System.Numerics;

namespace Engine;

/// <summary>How much global illumination the program set, a world resource the renderer reads each frame.</summary>
internal sealed class GlobalIlluminationSettings
{
    /// <summary>The quality, <see cref="GlobalIllumination.Off"/> for none.</summary>
    public GlobalIllumination Quality { get; set; }

    /// <summary>Whether High leaves the device's ray tracing alone, as <c>gi.rays off</c> sets it, so what it costs can be measured.</summary>
    public bool RaysOff { get; set; }

    /// <summary>
    /// Whether High traces the world's probes' rays through the device's ray tracing in place of the
    /// scene's distance field, crossing what lets light through as the field does, as
    /// <c>gi.toggle meshes on</c> sets it.
    /// </summary>
    /// <remarks>
    /// Off until it reads every view at least as near a path-traced reference as the field: taken,
    /// Sponza's door at 8:00 read half its light where the field reads 0.98 of it, the bounce rooms'
    /// window's room 0.250 of its light off where 0.228, and the floor under a canopy of leaf blocks
    /// two and a half units down 0.135 of the sky's light the canopy holds back where the reference
    /// takes 0.17 and the field 0.155.
    /// </remarks>
    public bool MeshesTraced { get; set; }

    /// <summary>Whether the screen's probes take nothing of the frame before's, as <c>gi.toggle history off</c> sets it.</summary>
    public bool HistoryOff { get; set; }

    /// <summary>Whether each screen probe keeps its own light, blended with none of its neighbors', as <c>gi.toggle filter off</c> sets it.</summary>
    public bool FilterOff { get; set; }

    /// <summary>
    /// Whether the screen's probes are left out whole at any quality, for the window and the render
    /// targets, neither made nor traced, every surface reading the cascades' light alone and each
    /// pixel marching to the glow lights itself, as <c>gi.toggle screen off</c> and the global
    /// illumination window's debug toggle set it, so what they cost and give can be read.
    /// </summary>
    /// <remarks>
    /// A debugging switch alone, which no program's call reaches. A program leaves the screen's
    /// probes out by <see cref="GlobalIllumination.Low"/> (<see cref="GlobalIlluminationRenderer.ScreenProbesAt"/>).
    /// Each pixel's march costs the model pass in step with the glow lights in view, 13.3 ms in a
    /// voxel game's room of forty glowstones at 1280 by 720 at High, where 2.2 with the probes read.
    /// </remarks>
    public bool ScreenOff { get; set; }

    /// <summary>The window's screen probe whose rays are written for <c>gi.screenprobe</c> to read, by its index, or -1 for none.</summary>
    public int LoggedScreenProbe { get; set; } = -1;

    /// <summary>
    /// Whether the logged screen probe (<see cref="LoggedScreenProbe"/>) writes a value that is not a
    /// number as its light, which a test feeds the probes' history and the window's blend with.
    /// </summary>
    public bool LoggedScreenProbeNotANumber { get; set; }

    /// <summary>Whether each cascade keeps its own rays' light, taking nothing from the cascade above, as <c>gi.toggle merge off</c> sets it.</summary>
    public bool MergeOff { get; set; }

    /// <summary>Whether the surfaces the rays meet take none of the indirect light that reached them the frame before, so light makes one bounce, as <c>gi.toggle again off</c> sets it.</summary>
    public bool AgainOff { get; set; }

    /// <summary>What the indirect light reaching the surfaces the rays meet is taken times, 1 unless <see cref="E3D.SetBounceGain"/> set it.</summary>
    public float BounceGain { get; set; } = 1;

    /// <summary>
    /// Whether the world's probes take the whole of the indirect light of the frame before however
    /// their own light falls, so the indirect light from a light that went out fades over
    /// the frames each bounce takes, as <c>gi.toggle follow off</c> sets it.
    /// </summary>
    public bool FollowOff { get; set; }

    /// <summary>
    /// Whether the small emissive surfaces global illumination carries as lights give none, their
    /// light lost to the rays all the same, as <c>gi.toggle glow off</c> sets it, so what the glow
    /// lights give and cost can be read (<see cref="GlowLights"/>).
    /// </summary>
    public bool GlowOff { get; set; }

    /// <summary>
    /// Whether the window draws every surface white, so the frame shows the light that reaches each
    /// surface alone and not its color, as <c>gi.toggle albedo off</c> sets it, which a reading of how
    /// the light changes as the camera moves takes apart from the textures the camera moves over.
    /// </summary>
    public bool AlbedoOff { get; set; }

    /// <summary>
    /// Whether the window leaves out the surfaces' reflection of the indirect light, the probes'
    /// along the mirror way, the environment's, a reflection probe's, the ambient lights' and what
    /// the trace through the depth meets, as <c>gi.toggle specular off</c> sets it, so a reading
    /// takes the diffuse light apart from it against a reference traced without the first face's
    /// specular.
    /// </summary>
    public bool SpecularOff { get; set; }

    /// <summary>The one cascade whose rays' light alone reaches the pixels, or -1 for every cascade, as <c>gi.toggle cascade</c> sets it.</summary>
    public int Alone { get; set; } = -1;

    /// <summary>
    /// Whether the rays of the world's probes and the screen's stop at a surface that lets light
    /// through as at a solid one, the merge of the cascades still seeing past it, as
    /// <c>gi.toggle through off</c> sets it, so what crossing a canopy's leaves costs can be read.
    /// </summary>
    public bool ThroughOff { get; set; }

    /// <summary>What <see cref="BounceViewRenderer"/> draws over the window, as <c>gi.show</c> sets it.</summary>
    public BounceView Shown { get; set; }

    /// <summary>The cascade a view of one shows.</summary>
    public int ShownCascade { get; set; }

    /// <summary>
    /// A reference's linear light, red, green and blue a pixel with rows from the top, its width and
    /// height, and how many times one has been given, which the difference view is drawn against.
    /// </summary>
    public (float[] Light, int Width, int Height, int Version)? Reference { get; set; }
}

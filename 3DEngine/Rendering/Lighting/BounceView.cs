namespace Engine;

/// <summary>What <c>BounceViewRenderer</c> draws of global illumination, as <c>gi.show</c> names it.</summary>
internal enum BounceView
{
    /// <summary>Nothing, the window as it is.</summary>
    None,

    /// <summary>The screen's probes as tiles over the picture, each a dot of the light it holds.</summary>
    Tiles,

    /// <summary>The light each screen probe's rays brought, a block a probe.</summary>
    Light,

    /// <summary>Each screen probe's light blended with its neighbors' and the frame before's, as the model pass reads it.</summary>
    Filtered,

    /// <summary>How much of each screen probe's light the frames before gave, red for none to green for the most.</summary>
    History,

    /// <summary>The light a cascade's rays brought, each layer of its probes a square of their octahedrons.</summary>
    Rays,

    /// <summary>A cascade's light merged with the cascades above, laid out as <see cref="Rays"/> is.</summary>
    Merged,

    /// <summary>A cascade's probes as small cubes in the scene, each face the light the probe gathers from that side.</summary>
    Probes,

    /// <summary>The window's light against a reference, red where the frame is brighter and blue where it is darker.</summary>
    Difference,
}

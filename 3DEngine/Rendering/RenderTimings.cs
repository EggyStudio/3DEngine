namespace Engine;

/// <summary>
/// How long the last frame's rendering took, by phase and by graph node on the CPU, and by graph
/// node on the GPU as of the last frame whose timestamps came back, which is a few frames before.
/// </summary>
/// <remarks>
/// <see cref="BeginFrameMs"/> holds the wait for the frame in flight that last used the same
/// slot, so a frame the GPU cannot keep up with shows there.
/// </remarks>
public sealed class RenderTimings
{
    /// <summary>Copying the world into the render world.</summary>
    public double ExtractMs { get; internal set; }

    /// <summary>Waiting for the frame slot's fence and acquiring the image to draw into.</summary>
    public double BeginFrameMs { get; internal set; }

    /// <summary>The prepare systems, which upload what changed.</summary>
    public double PrepareMs { get; internal set; }

    /// <summary>Recording the graph's nodes.</summary>
    public double GraphMs { get; internal set; }

    /// <summary>Submitting the frame and presenting it.</summary>
    public double EndFrameMs { get; internal set; }

    /// <summary>Each prepare system, by its type's name, in the order they run.</summary>
    public IReadOnlyList<(string System, double Milliseconds)> PrepareCpu { get; internal set; } = [];

    /// <summary>Each graph node's recording, in graph order.</summary>
    public IReadOnlyList<(string Node, double Milliseconds)> NodeCpu { get; internal set; } = [];

    /// <summary>Each graph node's time on the GPU, empty on a device without timestamps.</summary>
    public IReadOnlyList<(string Node, double Milliseconds)> NodeGpu { get; internal set; } = [];
}

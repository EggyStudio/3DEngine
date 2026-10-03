namespace Engine;

/// <summary>Diagnostic snapshot summarizing adapter, surface, and frame metrics.</summary>
/// <seealso cref="Renderer"/>
public sealed class RendererDiagnostics
{
    /// <summary>Information about the current graphics adapter (GPU).</summary>
    public GraphicsAdapterInfo AdapterInfo { get; private set; } = GraphicsAdapterInfo.Unknown;

    /// <summary>Current surface extent (width × height) in pixels.</summary>
    public Extent2D SurfaceExtent { get; private set; }

    /// <summary>Surface revision counter (bumped on resize).</summary>
    public ulong SurfaceRevision { get; private set; }

    /// <summary>Total number of frames rendered since initialization.</summary>
    public ulong FramesRendered { get; private set; }

    /// <summary>Initializes diagnostics with the specified adapter info.</summary>
    /// <param name="adapter">The graphics adapter information.</param>
    internal void Initialize(GraphicsAdapterInfo adapter)
    {
        AdapterInfo = adapter;
    }

    /// <summary>Records metrics for the current frame.</summary>
    /// <param name="adapter">Current adapter info.</param>
    /// <param name="extent">Current surface extent.</param>
    /// <param name="surfaceRevision">Current surface revision.</param>
    internal void RecordFrame(GraphicsAdapterInfo adapter, Extent2D extent, ulong surfaceRevision)
    {
        AdapterInfo = adapter;
        SurfaceExtent = extent;
        SurfaceRevision = surfaceRevision;
        FramesRendered++;
    }
}

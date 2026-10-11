namespace Engine;

/// <summary>
/// The window's screen probes: the light arriving at each probe's surface and the surface's normal
/// and distance from the eye, an image of each, a texel a probe, and the buffer of the view they
/// were placed through.
/// </summary>
internal sealed class GpuScreenProbes : IDisposable
{
    private readonly Action _dispose;

    internal GpuScreenProbes(int across, int down, int tile, IImage irradiance, IImageView irradianceView, IImage geometry, IImageView geometryView,
        ISampler sampler, IBuffer view, Action dispose, IImage blended, IImageView blendedView, IImage history, IImageView historyView,
        IImage lastGeometry, IImageView lastGeometryView, IBuffer glowSeen, ScreenGlow glow)
    {
        GlowSeen = glowSeen;
        Glow = glow;
        History = history;
        HistoryView = historyView;
        LastGeometry = lastGeometry;
        LastGeometryView = lastGeometryView;
        Blended = blended;
        BlendedView = blendedView;
        Across = across;
        Down = down;
        Tile = tile;
        Irradiance = irradiance;
        IrradianceView = irradianceView;
        Geometry = geometry;
        GeometryView = geometryView;
        Sampler = sampler;
        View = view;
        _dispose = dispose;
    }

    /// <summary>The probes across the window.</summary>
    public int Across { get; }

    /// <summary>The probes down the window.</summary>
    public int Down { get; }

    /// <summary>The pixels along each side of the tile a probe stands in.</summary>
    public int Tile { get; }

    internal IImage Irradiance { get; }

    /// <summary>The light arriving at each probe's surface, a texel a probe, its alpha 1 where the probe holds it.</summary>
    public IImageView IrradianceView { get; }

    internal IImage Geometry { get; }

    /// <summary>Each probe's surface's normal, and in w its distance from the eye, or -1 for a probe on no surface.</summary>
    public IImageView GeometryView { get; }

    internal IImage Blended { get; }

    /// <summary>The light arriving at each probe's surface blended with its neighbors' on like surfaces, which the model pass reads, its alpha one more than the share of it the frame before's light gave.</summary>
    public IImageView BlendedView { get; }

    internal IImage History { get; }

    /// <summary>The blended light of the frame before, copied from <see cref="BlendedView"/>, which the next frame's blend takes in.</summary>
    public IImageView HistoryView { get; }

    internal IImage LastGeometry { get; }

    /// <summary>The probes' surfaces of the frame before, copied from <see cref="GeometryView"/>, which tell where its light may be taken.</summary>
    public IImageView LastGeometryView { get; }

    /// <summary>
    /// How much of each glow light each probe's surface sees past what the field holds between, a
    /// byte a light in the places the bounce's lights are written in, sixteen words of them, and
    /// three of the light of those too faint to be marched to by their faces, which
    /// <c>gi_screen_glow.slang</c> takes each point's from the probes around it.
    /// </summary>
    public IBuffer GlowSeen { get; }

    /// <summary>The glow lights' light on the view's surfaces, a texel of the view's depth each, and what it is blended from.</summary>
    public ScreenGlow Glow { get; }

    /// <summary>A sampler that reads a texel as it is.</summary>
    public ISampler Sampler { get; }

    /// <summary>The view the probes were placed through, as <c>gi_screen.slang</c>'s <c>ScreenView</c>.</summary>
    public IBuffer View { get; }

    /// <summary>
    /// The bytes of <see cref="View"/>: this frame's camera and its inverse, four rows of the probes'
    /// layout, the frame before's camera and eye, how far from their tiles' middles the probes march
    /// to the glow lights from, and the glow lights' count and the places kept.
    /// </summary>
    public const int ViewBytes = 3 * 64 + 7 * 16;

    /// <summary>The bytes of <see cref="GlowSeen"/> each probe holds, twenty words (GlowSeenWords in glow.slang).</summary>
    public const int GlowSeenBytes = 20 * 4;

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

/// <summary>
/// The glow lights' light on a view's surfaces as <c>gi_screen_glow.slang</c> leaves it, a texel of
/// the view's depth each, half the view's size: the share each point takes of the near lights'
/// rough light, the far lights' light, how far the frames' light has strayed from the blend and how
/// many frames it holds, this frame's, which the model pass reads with each texel's normal, and the
/// frame before's, copied after each frame.
/// </summary>
internal sealed record ScreenGlow(IImage Light, IImageView LightView, IImage LastLight, IImageView LastLightView,
    IImage State, IImageView StateView, IImage LastState, IImageView LastStateView, IImage Normal, IImageView NormalView,
    IImage Far, IImageView FarView, IImage LastFar, IImageView LastFarView)
{
    /// <summary>The images, in the order the views are made of them.</summary>
    public IImage[] Images => [Light, LastLight, State, LastState, Normal, Far, LastFar];

    /// <summary>The views, in the order of <see cref="Images"/>.</summary>
    public IImageView[] Views => [LightView, LastLightView, StateView, LastStateView, NormalView, FarView, LastFarView];
}

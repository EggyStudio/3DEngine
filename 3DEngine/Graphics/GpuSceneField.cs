using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// The scene's distance field on the GPU, two images of half floats, each its cascades one after
/// another along z, the field the still meshes make, which a build writes, and the field the
/// passes read, that one with the shapes of the meshes that moved stamped into it, the color and
/// the emission of each cell's nearest still surface in two images more, and a uniform
/// buffer saying where each cascade lies, which a frame writes before any pass reads it.
/// </summary>
/// <remarks>
/// The field the passes read waits to be sampled between the frames' work on it, and goes to the
/// general layout while a build or a stamp writes it. The still field stays in the general layout,
/// read and written only by the field's own work. A build fills a buffer of a word a cell of one
/// cascade with the least of the codes its triangles give, then turns them into distances.
/// </remarks>
internal sealed class GpuSceneField : IDisposable
{
    private readonly Action _dispose;

    internal GpuSceneField(int cascades, int resolution, IImageView view, ISampler sampler, IBuffer info, IBuffer cells,
        VkImage still, VkImageView stillView, VkImage field, VkImageView fieldView, Action dispose,
        IBuffer albedo, IBuffer glow, VkImage albedoImage, IImageView albedoView, VkImage glowImage, IImageView glowView, IBuffer behind,
        VkImage stillPaint, VkImageView stillPaintView, VkImage facesImage, IImageView facesView, VkImage stillFaces, VkImageView stillFacesView)
    {
        FacesImage = facesImage;
        FacesView = facesView;
        StillFaces = stillFaces;
        StillFacesView = stillFacesView;
        Behind = behind;
        StillPaint = stillPaint;
        StillPaintView = stillPaintView;
        Albedo = albedo;
        Glow = glow;
        AlbedoImage = albedoImage;
        AlbedoView = albedoView;
        GlowImage = glowImage;
        GlowView = glowView;
        Cascades = cascades;
        Resolution = resolution;
        View = view;
        Sampler = sampler;
        Info = info;
        Cells = cells;
        Still = still;
        StillView = stillView;
        Field = field;
        FieldView = fieldView;
        _dispose = dispose;
    }

    /// <summary>How many cascades the images hold.</summary>
    public int Cascades { get; }

    /// <summary>The cells along each side of a cascade.</summary>
    public int Resolution { get; }

    /// <summary>The field the passes read, every cascade, as a 3D texture.</summary>
    public IImageView View { get; }

    /// <summary>A sampler that blends between the eight cells around a point, clamped at the edges.</summary>
    public ISampler Sampler { get; }

    /// <summary>Where each cascade lies and the eye they lie around, as <c>scenefield.slang</c>'s <c>SceneFieldInfo</c>.</summary>
    public IBuffer Info { get; }

    /// <summary>The bytes of <see cref="Info"/>.</summary>
    public const int InfoBytes = 16 + MaxCascades * 16 + 16;

    /// <summary>The most cascades the info carries, eight counted and the far one.</summary>
    public const int MaxCascades = 9;

    /// <summary>
    /// The bytes of GPU memory a field of <paramref name="cascades"/> cascades of
    /// <paramref name="resolution"/> cells a side takes, its images' 22 bytes a cell, the still and
    /// the read distances in two each, the still and the read colors in four each, the emission in
    /// eight and the faces in one each, and the four buffers a build splats a cascade into, 20 a cell
    /// of one cascade.
    /// </summary>
    public static long MemoryBytes(int cascades, int resolution) =>
        (long)resolution * resolution * resolution * (22L * Math.Max(1, cascades) + 20);

    /// <summary>
    /// The color of each cell's nearest still surface, linear, its alpha 1 where a surface painted
    /// the cell and 0 where none did, laid out as <see cref="View"/> is.
    /// </summary>
    public IImageView AlbedoView { get; }

    /// <summary>The emission of each cell's nearest still surface, linear, laid out as <see cref="View"/> is.</summary>
    public IImageView GlowView { get; }

    /// <summary>
    /// The way each cell's nearest surface's face looks, in eight bits, as <c>scenefield.slang</c>'s
    /// <c>faceWay</c> reads them, laid out as <see cref="View"/> is, which a ray's hit takes its color
    /// from cells by.
    /// </summary>
    public IImageView FacesView { get; }

    /// <summary>The bytes the faces of the field take, the passes' and the still meshes', a byte a cell each.</summary>
    public long FacesBytes => 2L * Resolution * Resolution * Resolution * Cascades;

    internal VkImage FacesImage { get; }

    // The faces the still meshes leave, which a stamp starts each cell of its bricks from, as it
    // does the still meshes' colors.
    internal VkImage StillFaces { get; }
    internal VkImageView StillFacesView { get; }

    internal IBuffer Albedo { get; }
    internal IBuffer Glow { get; }
    internal VkImage AlbedoImage { get; }
    internal VkImage GlowImage { get; }

    internal IBuffer Cells { get; }

    // The nearest face each cell lies straight behind, in 1024ths of a cell, which tells a wall
    // thinner than a cell, a cell lying in front of one of its faces and close behind the other.
    internal IBuffer Behind { get; }

    // What the emitters thinner than a cell lend the cells they lie near, six words a cell, made
    // the first time a build has such an emitter, so a scene with none holds none of its 6 MB.
    internal IBuffer? Emitted { get; set; }

    // Each cascade's words as its last build left them, cascade after cascade, made with the first
    // build, which a mesh painted again reads to find the cells it is nearest.
    internal IBuffer? KeptCells { get; set; }
    internal VkImage Still { get; }
    internal VkImageView StillView { get; }

    // The colors the still meshes paint, which a stamp starts each cell of its bricks from, so a
    // cell a moving mesh's box painted takes the still meshes' color again once the box has left.
    internal VkImage StillPaint { get; }
    internal VkImageView StillPaintView { get; }
    internal VkImage Field { get; }
    internal VkImageView FieldView { get; }

    // The word of a second double-sided face a cell lies as near as its nearest, of another way, as
    // where two walls cross, which the splat's painting pass keeps and the resolve turns into the
    // cell's second plane, the passes' own image beside the field, read by the probes' rays alone.
    internal IBuffer Second { get; init; } = null!;
    internal VkImage SecondImage { get; init; }

    /// <summary>The second plane a cell keeps where two double-sided faces lie as near it, every cascade, as a 3D texture.</summary>
    public IImageView SecondView { get; init; } = null!;

    /// <inheritdoc />
    public void Dispose()
    {
        _dispose();
        Emitted?.Dispose();
        KeptCells?.Dispose();
    }
}

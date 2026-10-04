namespace Engine;

/// <summary>
/// The GPU side of every texture in the <see cref="TextureStore"/>: an image, a view, a sampler and
/// a descriptor set per id, kept up to date by <see cref="GpuTexturesPrepare"/> before the graph
/// runs, and read by every pass that samples the flat API's textures.
/// </summary>
/// <remarks>
/// A texture that is unloaded or replaced may still be read by a frame the GPU has not finished,
/// so its objects are kept for <see cref="RetireFrames"/> frames before they are destroyed. A
/// replaced texture gets new objects rather than having its image written over for the same
/// reason. Id 0 is a white pixel, which untextured draws sample.
/// </remarks>
public sealed class GpuTextures : IDisposable
{
    /// <summary>
    /// Frames a retired object is kept for: one more than the device's frames in flight, so a frame
    /// that was recording when the object was retired has also finished.
    /// </summary>
    public const int RetireFrames = 4;

    // A render target's image and views belong to its RenderTarget, so Image is null for one, and
    // for a target's depth, whose view the target owns too. SrgbView is the same pixels decoded
    // from sRGB when sampled, which the model pass reads a base color through, since it lights in
    // linear space.
    private sealed record Entry(IImage? Image, IImageView View, IImageView SrgbView, ISampler Sampler, IDescriptorSet Set, RenderTarget? Target = null,
        int DepthOf = 0)
    {
        // The first level alone, which a compute shader writes a mipmapped image through, made
        // when one first does. A copy made for a new sampler shares it, since the image is the same.
        public IImageView? FirstLevel { get; set; }

        public IDisposable[] Owned => Target is not null ? [Set, Sampler, Target]
            : DepthOf != 0 ? [Set, Sampler]
            : FirstLevel is not null ? [Set, Sampler, FirstLevel, SrgbView, View, Image!]
            : [Set, Sampler, SrgbView, View, Image!];
    }

    private readonly Dictionary<int, Entry> _entries = [];
    private readonly List<(long Frame, IDisposable[] Objects)> _retired = [];
    private long _frame;

    /// <summary>How many textures have GPU objects, the white one included.</summary>
    public int Count => _entries.Count;

    /// <summary>The descriptor set for texture <paramref name="id"/>, or the white one when it is not loaded.</summary>
    /// <remarks>A texture unloaded after its draw was recorded draws white rather than failing.</remarks>
    public IDescriptorSet SetFor(IGraphicsDevice gfx, int id) =>
        id != 0 && _entries.TryGetValue(id, out var entry) ? entry.Set : White(gfx).Set;

    /// <summary>
    /// The view and sampler of texture <paramref name="id"/>, or the white one's when it is not
    /// loaded, for a descriptor set of a pass's own. With <paramref name="srgb"/> the view decodes
    /// the texture's color from sRGB to linear as it is sampled.
    /// </summary>
    public (IImageView View, ISampler Sampler) ViewFor(IGraphicsDevice gfx, int id, bool srgb = false)
    {
        var entry = id != 0 && _entries.TryGetValue(id, out var found) ? found : White(gfx);
        return (srgb ? entry.SrgbView : entry.View, entry.Sampler);
    }

    /// <summary>
    /// The image of texture <paramref name="id"/> and its view as stored, for a compute shader to
    /// write, a render target's color among them, or null for one not on the GPU yet, or a render
    /// target on a device that cannot store to the window's format.
    /// </summary>
    /// <remarks>
    /// A mipmapped texture is written through a view of its first level, which the dispatch then
    /// makes the other levels from.
    /// </remarks>
    internal (IImage Image, IImageView View)? StorageFor(GraphicsDevice device, int id)
    {
        if (id == 0 || !_entries.TryGetValue(id, out var entry)) return null;
        var image = entry.Image ?? entry.Target?.ColorView.Image;
        if (image is null || !image.Description.Usage.HasFlag(ImageUsage.Storage)) return null;
        if (image.Description.MipLevels <= 1) return (image, entry.View);
        return (image, entry.FirstLevel ??= device.CreateFirstLevelView(image));
    }

    /// <summary>The render target of texture <paramref name="id"/>, or <c>null</c> when it is not one.</summary>
    public RenderTarget? TargetFor(int id) => _entries.TryGetValue(id, out var entry) ? entry.Target : null;

    /// <summary>Applies the store's queued uploads and removals, and destroys what has been retired long enough.</summary>
    public void Update(IGraphicsDevice gfx, TextureStore? store)
    {
        _frame++;
        if (store is not null)
        {
            var (uploads, removals) = store.Take();

            foreach (var id in removals)
            {
                if (!_entries.Remove(id, out var gone)) continue;
                Retire(gone.Owned);
                // A target's depth goes with it, since the view it samples does.
                if (gone.Target is not null)
                    foreach (var (depthId, depth) in _entries.Where(e => e.Value.DepthOf == id).ToList())
                    {
                        _entries.Remove(depthId);
                        Retire(depth.Owned);
                    }
            }

            foreach (var upload in uploads)
            {
                _entries.TryGetValue(upload.Id, out var existing);

                if (upload.Target)
                {
                    if (gfx is not GraphicsDevice device) continue;
                    var target = device.CreateRenderTarget((uint)upload.Width, (uint)upload.Height);
                    var targetSampler = CreateSampler(gfx, upload.Filter, upload.Wrap);
                    _entries[upload.Id] = new Entry(null, target.ColorView, target.SrgbColorView, targetSampler, CreateSet(gfx, target.ColorView, targetSampler), target);
                    device.Name(target.ColorView.Image, $"Render texture {upload.Id}");
                    if (existing is not null) Retire(existing.Owned);
                    continue;
                }

                if (upload.DepthOf != 0)
                {
                    if (!_entries.TryGetValue(upload.DepthOf, out var owner) || owner.Target is not { } depthTarget) continue;
                    var depthSampler = CreateSampler(gfx, upload.Filter, upload.Wrap);
                    _entries[upload.Id] = new Entry(null, depthTarget.DepthView, depthTarget.DepthView, depthSampler,
                        CreateSet(gfx, depthTarget.DepthView, depthSampler), DepthOf: upload.DepthOf);
                    if (existing is not null) Retire(existing.Owned);
                    continue;
                }

                if (upload.Rgba is null && upload.Mipmaps && existing is { Image: { } old } && old.Description.MipLevels <= 1 && gfx is GraphicsDevice mipDevice)
                {
                    // Mip levels asked for after the pixels went up, so a new image takes them
                    // from the old one on the GPU.
                    var image = gfx.CreateImage(MipmappedDesc(old.Description.Extent.Width, old.Description.Extent.Height));
                    mipDevice.CopyWithMipmaps(old, image);
                    var view = gfx.CreateImageView(image);
                    var sampler = CreateSampler(gfx, upload.Filter, upload.Wrap);
                    _entries[upload.Id] = new Entry(image, view, gfx.CreateImageView(image, ImageFormat.R8G8B8A8_Srgb), sampler, CreateSet(gfx, view, sampler));
                    Retire(existing.Owned);
                    continue;
                }

                if (upload.Rgba is null)
                {
                    // Only the filter changed, so the image stays and the sampler and set are new.
                    if (existing is null) continue;
                    var sampler = CreateSampler(gfx, upload.Filter, upload.Wrap);
                    _entries[upload.Id] = existing with { Sampler = sampler, Set = CreateSet(gfx, existing.View, sampler) };
                    Retire(existing.Set, existing.Sampler);
                    continue;
                }

                _entries[upload.Id] = Create(gfx, upload.Rgba, upload.Width, upload.Height, upload.Filter, upload.Mipmaps, upload.Wrap);
                (gfx as GraphicsDevice)?.Name(_entries[upload.Id].Image!, $"Texture {upload.Id}");
                if (existing is not null)
                    Retire(existing.Owned);
            }
        }

        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < RetireFrames) continue;
            foreach (var o in _retired[i].Objects) o.Dispose();
            _retired.RemoveAt(i);
        }
    }

    private Entry White(IGraphicsDevice gfx)
    {
        if (_entries.TryGetValue(0, out var white)) return white;
        return _entries[0] = Create(gfx, [255, 255, 255, 255], 1, 1, TextureFilter.Point);
    }

    private static Entry Create(IGraphicsDevice gfx, byte[] rgba, int width, int height, TextureFilter filter, bool mipmaps = false,
        TextureWrap wrap = TextureWrap.Repeat)
    {
        // A copy source too, so mip levels can be made from it later.
        var image = gfx.CreateImage(mipmaps
            ? MipmappedDesc((uint)width, (uint)height)
            : new ImageDesc(new Extent2D((uint)width, (uint)height), ImageFormat.R8G8B8A8_UNorm, Usage));
        gfx.UploadTexture2D(image, rgba, (uint)width, (uint)height, 4);
        var view = gfx.CreateImageView(image);
        var sampler = CreateSampler(gfx, filter, wrap);
        return new Entry(image, view, gfx.CreateImageView(image, ImageFormat.R8G8B8A8_Srgb), sampler, CreateSet(gfx, view, sampler));
    }

    private static ImageDesc MipmappedDesc(uint width, uint height) => new(
        new Extent2D(width, height),
        ImageFormat.R8G8B8A8_UNorm,
        Usage,
        ImageDesc.FullMipChain(width, height));

    // A texture is sampled, filled from the CPU, copied into its mip levels, and written by a
    // compute shader the program gives it to.
    private const ImageUsage Usage = ImageUsage.Sampled | ImageUsage.TransferDst | ImageUsage.TransferSrc | ImageUsage.Storage;

    private static ISampler CreateSampler(IGraphicsDevice gfx, TextureFilter filter, TextureWrap wrap) => gfx.CreateSampler(SamplerFor(filter, wrap));

    /// <summary>The sampler a texture filtered by <paramref name="filter"/> and wrapped by <paramref name="wrap"/> is read through.</summary>
    internal static SamplerDesc SamplerFor(TextureFilter filter, TextureWrap wrap = TextureWrap.Repeat)
    {
        var address = wrap switch
        {
            TextureWrap.Clamp => SamplerAddressMode.ClampToEdge,
            TextureWrap.MirrorRepeat => SamplerAddressMode.MirrorRepeat,
            _ => SamplerAddressMode.Repeat,
        };
        var f = filter == TextureFilter.Point ? SamplerFilter.Nearest : SamplerFilter.Linear;
        var anisotropy = filter switch
        {
            TextureFilter.Anisotropic4x => 4f,
            TextureFilter.Anisotropic8x => 8f,
            TextureFilter.Anisotropic16x => 16f,
            _ => 1f,
        };
        return new SamplerDesc(f, f, address, address, address, anisotropy);
    }

    private static IDescriptorSet CreateSet(IGraphicsDevice gfx, IImageView view, ISampler sampler)
    {
        var set = gfx.CreateDescriptorSet();
        gfx.UpdateDescriptorSet(set, uniformBinding: null, new CombinedImageSamplerBinding(view, sampler, 1));
        return set;
    }

    private void Retire(params IDisposable[] objects) => _retired.Add((_frame, objects));

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, objects) in _retired)
            foreach (var o in objects) o.Dispose();
        _retired.Clear();
        foreach (var e in _entries.Values)
            foreach (var o in e.Owned) o.Dispose();
        _entries.Clear();
    }
}

/// <summary>Prepare system that brings <see cref="GpuTextures"/> up to date and hands it to the render world.</summary>
public sealed class GpuTexturesPrepare : IPrepareSystem, IDisposable
{
    private readonly GpuTextures _textures = new();

    /// <inheritdoc />
    public void Run(RenderWorld renderWorld, RenderContext renderContext)
    {
        _textures.Update(renderContext.Device, renderWorld.TryGet<TextureStore>());
        renderWorld.Set(_textures);
    }

    /// <inheritdoc />
    public void Dispose() => _textures.Dispose();
}

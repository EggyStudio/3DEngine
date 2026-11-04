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
internal sealed class GpuTextures : IDisposable
{
    /// <summary>
    /// Frames a retired object is kept for, one more than the device's frames in flight, so a frame
    /// that was recording when the object was retired has also finished.
    /// </summary>
    public const int RetireFrames = 4;

    // A render target's image and views belong to its RenderTarget, so Image is null for one, and
    // for a target's depth and its colors past the first, whose views the target owns too, the
    // target being Of. SrgbView is the same pixels decoded
    // from sRGB when sampled, which the model pass reads a base color through, since it lights in
    // linear space.
    private sealed record Entry(IImage? Image, IImageView View, IImageView SrgbView, ISampler Sampler, IDescriptorSet Set, RenderTarget? Target = null,
        int Of = 0, bool Cube = false)
    {
        // The first level alone, which a compute shader writes a mipmapped image through, made
        // when one first does. A copy made for a new sampler shares it, since the image is the same.
        public IImageView? FirstLevel { get; set; }

        // The image a compute shader writes in place of a render target's that the device cannot
        // store to, copied into it after each dispatch, made when one first does. A copy made for
        // a new sampler shares it, as it shares the first level.
        public (IImage Image, IImageView View)? StandIn { get; set; }

        private IDisposable[] StandInParts => StandIn is { } standIn ? [standIn.View, standIn.Image] : [];

        public IDisposable[] Owned => Target is not null ? [Set, Sampler, Target, .. StandInParts]
            : Of != 0 ? [Set, Sampler, .. StandInParts]
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
    /// <remarks>
    /// With <paramref name="cube"/> it is for a <c>SamplerCube</c>, which a texture that is not a
    /// cube, or none, reads as black, as an unset cube reads in OpenGL. Without it a cube reads as
    /// the white texture, since a 2D sampler cannot read a cube.
    /// </remarks>
    public (IImageView View, ISampler Sampler) ViewFor(IGraphicsDevice gfx, int id, bool srgb = false, bool cube = false)
    {
        if (cube)
        {
            var face = id != 0 && _entries.TryGetValue(id, out var cubeEntry) && cubeEntry.Cube ? cubeEntry : BlackCube(gfx);
            return (srgb ? face.SrgbView : face.View, face.Sampler);
        }
        var entry = id != 0 && _entries.TryGetValue(id, out var found) && !found.Cube ? found : White(gfx);
        return (srgb ? entry.SrgbView : entry.View, entry.Sampler);
    }

    /// <summary>
    /// The image of texture <paramref name="id"/> and its view as stored, for a compute shader to
    /// write, any color of a render target among them, with the image a stand-in is copied into
    /// after the shader runs, or null for one not on the GPU yet.
    /// </summary>
    /// <remarks>
    /// A mipmapped texture is written through a view of its first level, which the dispatch then
    /// makes the other levels from. A render target's color the device cannot store to is written
    /// through a stand-in of a format it does store (<see cref="GraphicsDevice.CreateStandIn"/>),
    /// which the dispatch fills from the target first and copies into it after.
    /// </remarks>
    internal (IImage Image, IImageView View, IImage? Into)? StorageFor(GraphicsDevice device, int id)
    {
        if (id == 0 || !_entries.TryGetValue(id, out var entry) || entry.Cube) return null;
        var image = entry.Image ?? entry.Target?.ColorView.Image
            ?? (entry.Of != 0 && entry.View.Image.Description.Format != ImageFormat.D32_Float ? entry.View.Image : null);
        if (image is null) return null;
        if (image.Description.Usage.HasFlag(ImageUsage.Storage))
            return image.Description.MipLevels <= 1 ? (image, entry.View, null) : (image, entry.FirstLevel ??= device.CreateFirstLevelView(image), null);
        if (entry.Target is null && entry.Of == 0) return null;
        var (standIn, standInView) = entry.StandIn ??= device.CreateStandIn(image);
        return (standIn, standInView, image);
    }

    /// <summary>The image of texture <paramref name="id"/>, a render target's colors among them and not its depth, or null for one not on the GPU yet.</summary>
    internal IImage? ImageFor(int id) =>
        id != 0 && _entries.TryGetValue(id, out var entry) && !entry.Cube
            ? entry.Image ?? entry.Target?.ColorView.Image ?? (entry.Of != 0 && entry.View.Image.Description.Format != ImageFormat.D32_Float ? entry.View.Image : null)
            : null;

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
                // A target's depth and colors go with it, since the views they sample do.
                if (gone.Target is not null)
                    foreach (var (ownedId, owned) in _entries.Where(e => e.Value.Of == id).ToList())
                    {
                        _entries.Remove(ownedId);
                        Retire(owned.Owned);
                    }
            }

            foreach (var upload in uploads)
            {
                _entries.TryGetValue(upload.Id, out var existing);

                if (upload.Target)
                {
                    if (gfx is not GraphicsDevice device) continue;
                    var target = upload.Formats is { } formats
                        ? device.CreateRenderTarget((uint)upload.Width, (uint)upload.Height, formats, multisampled: upload.Multisampled)
                        : device.CreateRenderTarget((uint)upload.Width, (uint)upload.Height, ImageFormat.Undefined, multisampled: upload.Multisampled);
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
                    _entries[upload.Id] = new Entry(null, depthTarget.DepthView!, depthTarget.DepthView!, depthSampler,
                        CreateSet(gfx, depthTarget.DepthView!, depthSampler), Of: upload.DepthOf);
                    if (existing is not null) Retire(existing.Owned);
                    continue;
                }

                if (upload.ColorOf != 0)
                {
                    if (!_entries.TryGetValue(upload.ColorOf, out var owner) || owner.Target is not { } colorTarget
                        || upload.ColorIndex < 1 || upload.ColorIndex > colorTarget.MoreColorViews.Count) continue;
                    var view = colorTarget.MoreColorViews[upload.ColorIndex - 1];
                    var colorSampler = CreateSampler(gfx, upload.Filter, upload.Wrap);
                    _entries[upload.Id] = new Entry(null, view, view, colorSampler, CreateSet(gfx, view, colorSampler), Of: upload.ColorOf);
                    if (existing is not null) Retire(existing.Owned);
                    continue;
                }

                if (upload.Blocks is { } blocks)
                {
                    if (Compressed(gfx, blocks, upload, store) is not { } made) continue;
                    _entries[upload.Id] = made;
                    if (existing is not null) Retire(existing.Owned);
                    continue;
                }

                if (upload.Cube && upload.Rgba is { } faces)
                {
                    if (gfx is not GraphicsDevice cubeDevice) continue;
                    var (cubeImage, cubeView, cubeSrgb) = cubeDevice.CreateTextureCube((uint)upload.Width, faces);
                    var cubeSampler = CreateSampler(gfx, upload.Filter, upload.Wrap);
                    // The immediate pass's set holds the white texture, since a 2D sampler cannot read a cube.
                    _entries[upload.Id] = new Entry(cubeImage, cubeView, cubeSrgb, cubeSampler, CreateSet(gfx, White(gfx).View, cubeSampler), Cube: true);
                    cubeDevice.Name(cubeImage, $"Cube texture {upload.Id}");
                    if (existing is not null) Retire(existing.Owned);
                    continue;
                }

                if (upload.Offset is { } at && upload.Rgba is { } part)
                {
                    // A rectangle of a texture on the GPU, written into its image in place.
                    if (existing is { Image: { } held } && gfx is GraphicsDevice regionDevice)
                        regionDevice.UploadTextureRegion(held, part, at.X, at.Y, (uint)upload.Width, (uint)upload.Height);
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
                    _entries[upload.Id] = existing with { Sampler = sampler, Set = CreateSet(gfx, existing.Cube ? White(gfx).View : existing.View, sampler) };
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

    // A texture of blocks made on the GPU with every level its file holds, viewed decoded from sRGB
    // as well where its format has such a view, and a level of it unpacked into pixels and read
    // back for the store, which takes its average color and whether it is translucent from them. A
    // device that samples no such blocks leaves the texture white, said once.
    private Entry? Compressed(IGraphicsDevice gfx, TextureAsset blocks, TextureStore.Upload upload, TextureStore store)
    {
        if (gfx is not GraphicsDevice device || !device.CanSampleBlockCompressed)
        {
            if (gfx is GraphicsDevice && !_warnedBlocks)
                Logger.Warn("The GPU samples no BC1, BC5 or BC7 blocks, so the textures of them are drawn white.");
            _warnedBlocks = true;
            return null;
        }
        var (format, decoded) = blocks.Format switch
        {
            TextureFormat.Bc1 => (ImageFormat.Bc1Alpha_UNorm, ImageFormat.Bc1Alpha_Srgb),
            TextureFormat.Bc5 => (ImageFormat.Bc5_UNorm, ImageFormat.Bc5_UNorm),
            _ => (ImageFormat.Bc7_UNorm, ImageFormat.Bc7_Srgb),
        };
        var image = device.CreateCompressedTexture(format, (uint)blocks.Width, (uint)blocks.Height, Ktx2.Levels(blocks));
        device.Name(image, $"Texture {upload.Id}");
        var view = gfx.CreateImageView(image);
        var sampler = CreateSampler(gfx, upload.Filter, upload.Wrap);
        // BC7's alpha may be anywhere from clear to solid, so its first level is read whole, where
        // BC1's is one or the other and BC5 has none, so their average is read from a level of 64
        // texels a side or fewer, a few thousand bytes where the first of 1,024 is 4 MB.
        var level = blocks.Format == TextureFormat.Bc7 ? 0
            : Math.Clamp(System.Numerics.BitOperations.Log2((uint)Math.Max(blocks.Width, blocks.Height)) - 6, 0, blocks.MipCount - 1);
        if (device.DecodedPixels(image, (uint)level) is { } pixels) store.Decoded(upload.Id, pixels, Math.Max(1, blocks.Width >> level));
        return new Entry(image, view, gfx.CreateImageView(image, decoded), sampler, CreateSet(gfx, view, sampler));
    }

    private bool _warnedBlocks;
    private static readonly ILogger Logger = Log.Category("Engine.Textures");

    private Entry White(IGraphicsDevice gfx)
    {
        if (_entries.TryGetValue(0, out var white)) return white;
        return _entries[0] = Create(gfx, [255, 255, 255, 255], 1, 1, TextureFilter.Point);
    }

    // A cube of one black texel a face, which a SamplerCube with no cube set reads.
    private Entry? _blackCube;

    private Entry BlackCube(IGraphicsDevice gfx)
    {
        if (_blackCube is not null) return _blackCube;
        if (gfx is not GraphicsDevice device) return White(gfx);
        byte[] faces = [.. Enumerable.Repeat<byte[]>([0, 0, 0, 255], 6).SelectMany(texel => texel)];
        var (image, view, srgb) = device.CreateTextureCube(1, faces);
        var sampler = CreateSampler(gfx, TextureFilter.Point, TextureWrap.Clamp);
        return _blackCube = new Entry(image, view, srgb, sampler, CreateSet(gfx, White(gfx).View, sampler), Cube: true);
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
            TextureWrap.MirrorClamp => SamplerAddressMode.MirrorClampToEdge,
            _ => SamplerAddressMode.Repeat,
        };
        var f = filter == TextureFilter.Point ? SamplerFilter.Nearest : SamplerFilter.Linear;
        // Point and bilinear read the nearest mip level, and the rest blend two, as raylib's do.
        var mip = filter is TextureFilter.Point or TextureFilter.Bilinear ? SamplerFilter.Nearest : SamplerFilter.Linear;
        // Every filter that blends takes 16 samples along a slant, or the device's most.
        return new SamplerDesc(f, f, address, address, address, filter == TextureFilter.Point ? 1f : 16f, mip);
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
        foreach (var o in _blackCube?.Owned ?? []) o.Dispose();
        _blackCube = null;
    }
}

/// <summary>Prepare system that brings <see cref="GpuTextures"/> up to date and hands it to the render world.</summary>
internal sealed class GpuTexturesPrepare : IPrepareSystem, IDisposable
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

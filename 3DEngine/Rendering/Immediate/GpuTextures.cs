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

    private sealed record Entry(IImage Image, IImageView View, ISampler Sampler, IDescriptorSet Set);

    private readonly Dictionary<int, Entry> _entries = [];
    private readonly List<(long Frame, IDisposable[] Objects)> _retired = [];
    private long _frame;

    /// <summary>How many textures have GPU objects, the white one included.</summary>
    public int Count => _entries.Count;

    /// <summary>The descriptor set for texture <paramref name="id"/>, or the white one when it is not loaded.</summary>
    /// <remarks>A texture unloaded after its draw was recorded draws white rather than failing.</remarks>
    public IDescriptorSet SetFor(IGraphicsDevice gfx, int id) =>
        id != 0 && _entries.TryGetValue(id, out var entry) ? entry.Set : White(gfx).Set;

    /// <summary>Applies the store's queued uploads and removals, and destroys what has been retired long enough.</summary>
    public void Update(IGraphicsDevice gfx, TextureStore? store)
    {
        _frame++;
        if (store is not null)
        {
            var (uploads, removals) = store.Take();

            foreach (var id in removals)
                if (_entries.Remove(id, out var gone))
                    Retire(gone.Set, gone.Sampler, gone.View, gone.Image);

            foreach (var upload in uploads)
            {
                _entries.TryGetValue(upload.Id, out var existing);

                if (upload.Rgba is null)
                {
                    // Only the filter changed, so the image stays and the sampler and set are new.
                    if (existing is null) continue;
                    var sampler = CreateSampler(gfx, upload.Filter);
                    _entries[upload.Id] = existing with { Sampler = sampler, Set = CreateSet(gfx, existing.View, sampler) };
                    Retire(existing.Set, existing.Sampler);
                    continue;
                }

                _entries[upload.Id] = Create(gfx, upload.Rgba, upload.Width, upload.Height, upload.Filter);
                if (existing is not null)
                    Retire(existing.Set, existing.Sampler, existing.View, existing.Image);
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

    private static Entry Create(IGraphicsDevice gfx, byte[] rgba, int width, int height, TextureFilter filter)
    {
        var image = gfx.CreateImage(new ImageDesc(
            new Extent2D((uint)width, (uint)height),
            ImageFormat.R8G8B8A8_UNorm,
            ImageUsage.Sampled | ImageUsage.TransferDst));
        gfx.UploadTexture2D(image, rgba, (uint)width, (uint)height, 4);
        var view = gfx.CreateImageView(image);
        var sampler = CreateSampler(gfx, filter);
        return new Entry(image, view, sampler, CreateSet(gfx, view, sampler));
    }

    private static ISampler CreateSampler(IGraphicsDevice gfx, TextureFilter filter)
    {
        var f = filter == TextureFilter.Point ? SamplerFilter.Nearest : SamplerFilter.Linear;
        return gfx.CreateSampler(new SamplerDesc(f, f,
            SamplerAddressMode.Repeat, SamplerAddressMode.Repeat, SamplerAddressMode.Repeat));
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
        {
            e.Set.Dispose();
            e.Sampler.Dispose();
            e.View.Dispose();
            e.Image.Dispose();
        }
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

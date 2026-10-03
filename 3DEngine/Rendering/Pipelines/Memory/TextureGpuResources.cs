using System.Collections.Concurrent;

namespace Engine;

/// <summary>
/// GPU resource registry mapping <see cref="Texture"/> <see cref="AssetId"/>s to
/// uploaded GPU images, image views, and samplers. Cached across frames; entries are
/// rebuilt on demand by <see cref="TexturePrepare"/> when a new handle appears, and
/// evicted when an <see cref="AssetEvent{T}.Modified"/> arrives via
/// <see cref="TextureInvalidations"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifetime:</b> uploads are reference-counted by <see cref="AssetId"/>: the registry
/// owns the GPU resources and disposes them on <see cref="Invalidate"/> /
/// <see cref="Dispose"/>. Disposal happens lazily on the next prepare pass after a frame
/// boundary - the GPU may still be sampling the old texture this frame, so the prior
/// view/sampler/image are deferred onto a per-frame retire list. The retire list is
/// drained on the next <see cref="DrainRetired"/> call (invoked by
/// <see cref="TexturePrepare"/>).
/// </para>
/// <para>
/// <b>Thread-safety:</b> all entry points are render-thread only. The backing
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> is concurrent only to harden against
/// future parallel prepare systems; today there is a single render thread.
/// </para>
/// </remarks>
/// <seealso cref="TexturePrepare"/>
/// <seealso cref="TextureInvalidations"/>
/// <seealso cref="MeshGpuRegistry"/>
public sealed class TextureGpuRegistry : IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.Renderer.Texture");

    /// <summary>A live GPU upload owned by the registry.</summary>
    /// <param name="Image">Backing image storage.</param>
    /// <param name="View">Sampleable view over <paramref name="Image"/>.</param>
    /// <param name="Sampler">Sampler bound alongside the view.</param>
    /// <param name="Width">Width of the uploaded mip-0 in pixels.</param>
    /// <param name="Height">Height of the uploaded mip-0 in pixels.</param>
    public readonly record struct Entry(IImage Image, IImageView View, ISampler Sampler, int Width, int Height);

    private readonly ConcurrentDictionary<AssetId, Entry> _entries = new();
    private readonly List<Entry> _retired = new();

    /// <summary>Number of live GPU uploads.</summary>
    public int Count => _entries.Count;

    /// <summary>Tries to retrieve an existing GPU upload for the given asset id.</summary>
    /// <param name="id">The texture asset id.</param>
    /// <param name="entry">When returning <c>true</c>, the GPU upload; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if a cached upload exists; otherwise <c>false</c>.</returns>
    public bool TryGet(AssetId id, out Entry entry) => _entries.TryGetValue(id, out entry);

    /// <summary>
    /// Gets the existing GPU upload for <paramref name="id"/>, or creates one by
    /// uploading <paramref name="texture"/>'s pixel bytes through
    /// <paramref name="device"/>. Returns <c>null</c> if the device cannot upload
    /// (e.g. test-only stub backend) or if the texture format is unsupported.
    /// </summary>
    /// <param name="id">The texture asset id (lookup key).</param>
    /// <param name="texture">CPU-side texture asset to upload on a cache miss.</param>
    /// <param name="device">Graphics device used for upload (must be a concrete
    ///   <see cref="GraphicsDevice"/> for the upload path to run).</param>
    /// <returns>The cached or freshly uploaded entry, or <c>null</c> on failure.</returns>
    public Entry? GetOrCreate(AssetId id, Texture texture, IGraphicsDevice device)
    {
        if (_entries.TryGetValue(id, out var existing)) return existing;

        if (device is not GraphicsDevice gd)
            return null; // headless / test backend - skip upload silently.

        if (!TryMapFormat(texture, out var imageFormat, out var bytesPerPixel))
        {
            Logger.Debug($"TextureGpuRegistry: skipping upload for asset {id} - unsupported format {texture.Format}.");
            return null;
        }

        var imageDesc = new ImageDesc(
            new Extent2D((uint)texture.Width, (uint)texture.Height),
            imageFormat,
            ImageUsage.Sampled | ImageUsage.TransferDst);

        IImage image;
        try { image = gd.CreateImage(imageDesc); }
        catch (Exception ex)
        {
            Logger.Warn($"TextureGpuRegistry: CreateImage failed for asset {id} ({texture.Width}x{texture.Height}, {texture.Format}): {ex.Message}");
            return null;
        }

        try
        {
            // Upload mip 0 only; the asset's MipCount > 1 case is left for the
            // future BC*/KTX2 path that needs explicit per-level CopyBufferToImage.
            int level0Bytes = texture.Width * texture.Height * bytesPerPixel;
            var pixels = texture.Pixels.AsSpan(0, Math.Min(level0Bytes, texture.Pixels.Length));
            gd.UploadTexture2D(image, pixels, (uint)texture.Width, (uint)texture.Height, bytesPerPixel);
        }
        catch (Exception ex)
        {
            Logger.Warn($"TextureGpuRegistry: UploadTexture2D failed for asset {id}: {ex.Message}");
            image.Dispose();
            return null;
        }

        var view = gd.CreateImageView(image);
        var sampler = gd.CreateSampler(new SamplerDesc(
            SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.Repeat, SamplerAddressMode.Repeat, SamplerAddressMode.Repeat));

        var entry = new Entry(image, view, sampler, texture.Width, texture.Height);
        _entries[id] = entry;
        Logger.Debug($"TextureGpuRegistry: uploaded asset {id} ({texture.Width}x{texture.Height}, {texture.Format}, source='{texture.SourcePath}').");
        return entry;
    }

    /// <summary>
    /// Evicts the GPU upload for the given asset id, deferring resource disposal until
    /// the next <see cref="DrainRetired"/> pass so the in-flight frame can finish using it.
    /// </summary>
    /// <param name="id">The asset id to evict.</param>
    /// <returns><c>true</c> if an entry was found and queued for retirement; otherwise <c>false</c>.</returns>
    public bool Invalidate(AssetId id)
    {
        if (!_entries.TryRemove(id, out var entry)) return false;
        _retired.Add(entry);
        Logger.Debug($"TextureGpuRegistry: invalidated asset {id} ({entry.Width}x{entry.Height}); deferred dispose.");
        return true;
    }

    /// <summary>
    /// Disposes all entries previously queued by <see cref="Invalidate"/>. Call once per
    /// frame from a render-thread system (typically <see cref="TexturePrepare"/>) after
    /// a fence guarantees the previous frame's commands are retired.
    /// </summary>
    public void DrainRetired()
    {
        if (_retired.Count == 0) return;
        for (int i = 0; i < _retired.Count; i++) DisposeEntry(_retired[i]);
        _retired.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var kv in _entries) DisposeEntry(kv.Value);
        _entries.Clear();
        for (int i = 0; i < _retired.Count; i++) DisposeEntry(_retired[i]);
        _retired.Clear();
    }

    private static void DisposeEntry(in Entry e)
    {
        try { e.Sampler?.Dispose(); } catch { /* best-effort */ }
        try { e.View?.Dispose(); }    catch { /* best-effort */ }
        try { e.Image?.Dispose(); }   catch { /* best-effort */ }
    }

    private static bool TryMapFormat(Texture texture, out ImageFormat format, out int bytesPerPixel)
    {
        // The renderer currently exposes only R8G8B8A8_UNorm and the depth/swapchain
        // formats; all other texture formats fall through until the GPU image-format
        // table grows. sRGB-vs-linear interpretation is tracked by texture.ColorSpace
        // but the underlying VkImage uses the same UNorm storage; future work should
        // emit the matching *_SRGB ImageFormat so sampling is hardware-linearised.
        switch (texture.Format)
        {
            case TextureFormat.Rgba8:
                format = ImageFormat.R8G8B8A8_UNorm;
                bytesPerPixel = 4;
                return true;
            default:
                format = ImageFormat.Undefined;
                bytesPerPixel = 0;
                return false;
        }
    }
}


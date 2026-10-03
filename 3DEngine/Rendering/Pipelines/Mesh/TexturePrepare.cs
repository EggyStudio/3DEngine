namespace Engine;

/// <summary>
/// Prepare system that ensures GPU image, view and sampler exist for every
/// <see cref="Handle{T}"/> referenced by a <see cref="RenderMeshInstance"/>.
/// Uses <see cref="TextureGpuRegistry"/> to cache uploads across frames keyed by
/// the texture asset id, and consumes <see cref="TextureInvalidations"/> emitted by
/// <see cref="MeshMaterialExtract"/> to evict entries whose CPU bytes were hot-reloaded.
/// </summary>
/// <remarks>
/// <para>
/// <b>Source of truth:</b> the main-world <see cref="Assets{T}"/> store is forwarded
/// onto the <see cref="RenderWorld"/> by <see cref="MeshMaterialExtract"/>; this
/// system reads it to obtain the CPU-side <see cref="Texture"/> for each handle and
/// passes the bytes to the registry. If neither the asset store nor a concrete
/// <see cref="GraphicsDevice"/> is available (e.g. headless tests), the system is a
/// no-op.
/// </para>
/// <para>
/// <b>Hot-reload:</b> <see cref="TextureInvalidations"/> is drained first, so any
/// asset that was modified this frame is freed before its replacement is re-uploaded
/// in the same pass.
/// </para>
/// </remarks>
/// <seealso cref="TextureGpuRegistry"/>
/// <seealso cref="MeshMaterialExtract"/>
/// <seealso cref="MeshPrepare"/>
public sealed class TexturePrepare : IPrepareSystem
{
    /// <inheritdoc />
    public void Run(RenderWorld renderWorld, RenderContext renderContext)
    {
        var ecs = renderWorld.Entities;
        if (ecs.Count<RenderMeshInstance>() == 0) return;

        var registry = renderWorld.TryGet<TextureGpuRegistry>();
        if (registry is null)
        {
            registry = new TextureGpuRegistry();
            renderWorld.Set(registry);
        }

        // 1) Process invalidations first so re-uploaded handles get fresh GPU storage.
        var invalidations = renderWorld.TryGet<TextureInvalidations>();
        if (invalidations is { Ids.Count: > 0 })
        {
            for (int i = 0; i < invalidations.Ids.Count; i++)
                registry.Invalidate(invalidations.Ids[i]);
            invalidations.Ids.Clear();
        }
        registry.DrainRetired();

        // 2) Without a Texture asset store there's nothing to upload. The renderer can
        //    still draw flat-shaded meshes via the existing albedo push constant.
        var assets = renderWorld.TryGet<Assets<Texture>>();
        if (assets is null) return;

        var device = renderContext.Device;
        foreach (var (_, mesh) in ecs.Query<RenderMeshInstance>())
        {
            EnsureUploaded(registry, assets, device, mesh.BaseColorTexture);
            EnsureUploaded(registry, assets, device, mesh.MetallicRoughnessTexture);
            EnsureUploaded(registry, assets, device, mesh.NormalTexture);
            EnsureUploaded(registry, assets, device, mesh.EmissiveTexture);
            EnsureUploaded(registry, assets, device, mesh.OcclusionTexture);
        }
    }

    private static void EnsureUploaded(
        TextureGpuRegistry registry,
        Assets<Texture> assets,
        IGraphicsDevice device,
        Handle<Texture> handle)
    {
        if (!handle.IsValid) return;
        if (registry.TryGet(handle.Id, out _)) return;
        if (!assets.TryGet(handle, out var tex)) return; // still streaming in
        registry.GetOrCreate(handle.Id, tex, device);
    }
}


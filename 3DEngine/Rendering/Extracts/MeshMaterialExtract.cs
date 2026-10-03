using System.Numerics;

namespace Engine;

/// <summary>
/// Extracts entities with <see cref="Mesh"/> + <see cref="Material"/> components into
/// render entities with <see cref="RenderMeshInstance"/> components in the render world's
/// <see cref="EcsWorld"/>.
/// </summary>
/// <remarks>
/// <c>extract_meshes</c> system that spawns render entities with
/// <c>RenderMeshInstance</c> + <c>MeshTransforms</c> components.
/// </remarks>
/// <seealso cref="RenderMeshInstance"/>
/// <seealso cref="QueueMeshPhaseItems"/>
public sealed class MeshMaterialExtract : IExtractSystem
{
    /// <inheritdoc />
    public void Run(World world, RenderWorld renderWorld)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs)) return;

        // Make the main-world Texture asset store and (if available) the live AssetServer
        // visible to render-thread prepare systems so they can resolve Handle<Texture>
        // -> CPU bytes -> GPU upload. We forward the references each frame; ownership
        // stays with the main world.
        if (world.TryGetResource<Assets<Texture>>(out var texAssets))
            renderWorld.Set(texAssets);

        // Forward Modified texture events so the render-thread TextureGpuRegistry can
        // invalidate cached GPU uploads in response to AssetServer hot-reloads.
        var texEvents = world.ReadAssetEvents<Texture>();
        if (texEvents.Count > 0)
        {
            var pending = renderWorld.TryGet<TextureInvalidations>() ?? new TextureInvalidations();
            for (int i = 0; i < texEvents.Count; i++)
            {
                var e = texEvents[i];
                if (e.Kind == AssetEventKind.Modified || e.Kind == AssetEventKind.Removed)
                    pending.Ids.Add(e.Id);
            }
            renderWorld.Set(pending);
        }

        foreach (var (entity, mesh) in ecs.Query<Mesh>())
        {
            if (!ecs.TryGet(entity, out Material mat)) continue;
            if (mesh.Positions is null || mesh.Positions.Length == 0) continue;

            // Identity transform if the entity has no Transform component.
            Transform t = default;
            ecs.TryGet(entity, out t);

            var model = Matrix4x4.CreateScale(t.Scale)
                      * Matrix4x4.CreateFromQuaternion(t.Rotation)
                      * Matrix4x4.CreateTranslation(t.Position);

            int renderEntity = renderWorld.Spawn();
            renderWorld.Entities.Add(renderEntity, new RenderMeshInstance
            {
                MainEntityId = entity,
                ModelMatrix = model,
                Albedo = mat.Albedo,
                MeshData = mesh,
                VertexCount = mesh.Positions.Length,
                BaseColorTexture         = mat.BaseColorTexture,
                MetallicRoughnessTexture = mat.MetallicRoughnessTexture,
                NormalTexture            = mat.NormalTexture,
                EmissiveTexture          = mat.EmissiveTexture,
                OcclusionTexture         = mat.OcclusionTexture,
                Material                 = mat.Handle,
            });
        }
    }
}

/// <summary>
/// Render-thread bucket of <see cref="AssetId"/>s whose backing <see cref="Texture"/>
/// was modified or removed since the previous frame. Consumed by <see cref="TexturePrepare"/>
/// to invalidate cached GPU uploads. Cleared each frame after consumption.
/// </summary>
/// <seealso cref="MeshMaterialExtract"/>
/// <seealso cref="TextureGpuRegistry"/>
public sealed class TextureInvalidations
{
    /// <summary>Asset IDs scheduled for cache eviction this frame.</summary>
    public List<AssetId> Ids { get; } = new();
}


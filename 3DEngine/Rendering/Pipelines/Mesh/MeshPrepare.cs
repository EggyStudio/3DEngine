namespace Engine;

/// <summary>
/// Prepare system that ensures GPU vertex buffers exist for all render entities
/// with <see cref="RenderMeshInstance"/> components.
/// Uses <see cref="MeshGpuRegistry"/> to cache buffers across frames, keyed by the
/// source game-world entity ID.
/// </summary>
/// <remarks>
/// <c>prepare_meshes</c> system querying <c>RenderMeshInstance</c> entities.
/// </remarks>
/// <seealso cref="RenderMeshInstance"/>
/// <seealso cref="MeshGpuRegistry"/>
/// <seealso cref="MainPassNode"/>
public sealed class MeshPrepare : IPrepareSystem
{
    /// <inheritdoc />
    public void Run(RenderWorld renderWorld, RenderContext renderContext)
    {
        var ecs = renderWorld.Entities;
        if (ecs.Count<RenderMeshInstance>() == 0)
            return;

        var registry = renderWorld.TryGet<MeshGpuRegistry>();
        if (registry is null)
        {
            registry = new MeshGpuRegistry();
            renderWorld.Set(registry);
        }

        var gfx = renderContext.Device;

        foreach (var (entity, mesh) in ecs.Query<RenderMeshInstance>())
        {
            // GetOrCreate checks if a buffer already exists.
            // creates and uploads only if missing.
            registry.GetOrCreate(mesh.MainEntityId, mesh.MeshData, gfx);
        }
    }
}

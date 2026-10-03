namespace Engine;

/// <summary>
/// Queue system that queries render entities with <see cref="RenderMeshInstance"/> components
/// and populates <see cref="Opaque3dPhase"/> and <see cref="Transparent3dPhase"/> with sorted
/// phase items and draw functions.
/// Runs as a prepare system after <see cref="MeshPrepare"/> (vertex buffers are already uploaded).
/// </summary>
/// <remarks>
/// <c>queue_mesh3d_manual</c> system querying <c>RenderMeshInstance</c>
/// entities and populating typed <c>RenderPhase&lt;T&gt;</c> collections.
/// </remarks>
/// <seealso cref="RenderMeshInstance"/>
/// <seealso cref="Opaque3dPhase"/>
/// <seealso cref="OpaquePhaseItem"/>
/// <seealso cref="DrawMeshOpaque"/>
public sealed class QueueMeshPhaseItems : IPrepareSystem
{
    /// <inheritdoc />
    public void Run(RenderWorld renderWorld, RenderContext renderContext)
    {
        var ecs = renderWorld.Entities;
        if (ecs.Count<RenderMeshInstance>() == 0) return;

        var opaquePhase = renderWorld.TryGet<Opaque3dPhase>();
        if (opaquePhase is null)
        {
            opaquePhase = new Opaque3dPhase();
            renderWorld.Set(opaquePhase);
        }

        var transparentPhase = renderWorld.TryGet<Transparent3dPhase>();
        if (transparentPhase is null)
        {
            transparentPhase = new Transparent3dPhase();
            renderWorld.Set(transparentPhase);
        }

        opaquePhase.Phase.Clear();
        transparentPhase.Phase.Clear();

        foreach (var (renderEntity, mesh) in ecs.Query<RenderMeshInstance>())
        {
            bool isTransparent = mesh.Albedo.W < 1.0f;
            int materialId = mesh.Material.Id;

            if (isTransparent)
            {
                transparentPhase.Phase.Add(new TransparentPhaseItem
                {
                    EntityId = mesh.MainEntityId,
                    SortKey = mesh.VertexCount, // TODO: replace with camera-space Z for proper depth sort.
                    BatchKey = materialId,
                    ModelMatrix = mesh.ModelMatrix,
                    Albedo = mesh.Albedo,
                    VertexCount = mesh.VertexCount,
                    MaterialId = materialId,
                    DrawFunction = null // assigned by MainPassNode
                });
            }
            else
            {
                opaquePhase.Phase.Add(new OpaquePhaseItem
                {
                    EntityId = mesh.MainEntityId,
                    SortKey = mesh.VertexCount, // TODO: replace with camera-space Z for proper depth sort.
                    BatchKey = materialId,
                    ModelMatrix = mesh.ModelMatrix,
                    Albedo = mesh.Albedo,
                    VertexCount = mesh.VertexCount,
                    MaterialId = materialId,
                    DrawFunction = null // assigned by MainPassNode
                });
            }
        }

        // Opaque front-to-back (early-Z), transparent back-to-front (correct blending).
        opaquePhase.Phase.Sort(descending: false);
        transparentPhase.Phase.Sort(descending: true);
    }
}

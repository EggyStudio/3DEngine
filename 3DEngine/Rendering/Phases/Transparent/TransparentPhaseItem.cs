using System.Numerics;

namespace Engine;

/// <summary>
/// Phase item for transparent 3D geometry. Sorted back-to-front by <see cref="SortKey"/>
/// within each <see cref="BatchKey"/> group to ensure correct alpha compositing.
/// </summary>
/// <seealso cref="Transparent3dPhase"/>
public struct TransparentPhaseItem : IPhaseItem
{
    /// <summary>ECS entity ID for GPU resource lookup in <see cref="MeshGpuRegistry"/>.</summary>
    public int EntityId;

    /// <inheritdoc />
    public int SortKey { get; set; }

    /// <inheritdoc />
    public int BatchKey { get; set; }

    /// <summary>Object-to-world model matrix.</summary>
    public Matrix4x4 ModelMatrix;

    /// <summary>Material albedo color (RGBA).</summary>
    public Vector4 Albedo;

    /// <summary>Number of vertices to draw.</summary>
    public int VertexCount;

    /// <summary>
    /// Authoring-side <see cref="MaterialHandle"/> id (<c>0</c> when the entity has
    /// no library-registered material). See <see cref="OpaquePhaseItem.MaterialId"/>
    /// for the cache-and-batch contract; transparent items still sort back-to-front
    /// by <see cref="SortKey"/> first, batching by material is secondary.
    /// </summary>
    public int MaterialId;

    /// <summary>The draw function that knows how to render this item.</summary>
    public IDrawFunction<TransparentPhaseItem>? DrawFunction;
}

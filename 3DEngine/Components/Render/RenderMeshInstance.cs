using System.Numerics;

namespace Engine;

/// <summary>
/// Per-entity render component attached to render entities during the extract phase.
/// Combines the model matrix, material albedo, mesh data, and source entity reference
/// needed by the prepare and queue phases.
/// </summary>
/// <seealso cref="MeshMaterialExtract"/>
/// <seealso cref="MeshPrepare"/>
/// <seealso cref="QueueMeshPhaseItems"/>
public struct RenderMeshInstance
{
    /// <summary>The source game-world entity ID (for <see cref="MeshGpuRegistry"/> vertex buffer lookup).</summary>
    public int MainEntityId;

    /// <summary>Object-to-world model matrix.</summary>
    public Matrix4x4 ModelMatrix;

    /// <summary>Material albedo color (RGBA).</summary>
    public Vector4 Albedo;

    /// <summary>Raw mesh data for GPU upload (vertex positions).</summary>
    public Mesh MeshData;

    /// <summary>Number of vertices (cached for draw calls).</summary>
    public int VertexCount;

    /// <summary>sRGB base-color texture handle; <see cref="Handle{T}.Invalid"/> when unbound.</summary>
    public Handle<Texture> BaseColorTexture;

    /// <summary>Linear metallic-roughness texture handle.</summary>
    public Handle<Texture> MetallicRoughnessTexture;

    /// <summary>Linear tangent-space normal-map handle.</summary>
    public Handle<Texture> NormalTexture;

    /// <summary>sRGB emissive texture handle.</summary>
    public Handle<Texture> EmissiveTexture;

    /// <summary>Linear ambient-occlusion texture handle.</summary>
    public Handle<Texture> OcclusionTexture;

    /// <summary>
    /// Authoring-side <see cref="MaterialHandle"/> copied verbatim from the source
    /// <see cref="Material.Handle"/>. The render world keys per-material Vulkan
    /// pipelines, descriptor sets and uniform buffers by this handle. <see cref="MaterialHandle.IsValid"/> is
    /// <c>false</c> for entities authored without a <see cref="MaterialLibrary"/>;
    /// the renderer falls back to the shared static-white pipeline in that case.
    /// </summary>
    public MaterialHandle Material;
}

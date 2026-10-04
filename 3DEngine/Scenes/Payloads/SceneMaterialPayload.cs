using System.Numerics;

namespace Engine;

/// <summary>
/// Backend-agnostic material payload modeled on the <c>UsdPreviewSurface</c> shader.
/// Produced by readers, as <see cref="AssimpModelReader"/>'s material pass, and translated to
/// renderer-side <see cref="Material"/> components by <c>SceneSpawnSystem</c>.
/// </summary>
/// <remarks>
/// <para>
/// Field names mirror the <c>UsdPreviewSurface</c> input names (<c>diffuseColor</c>,
/// <c>metallic</c>, <c>roughness</c>, <c>normal</c>, <c>occlusion</c>, <c>emissiveColor</c>,
/// <c>opacity</c>, <c>opacityThreshold</c>) and the glTF 2.0 PBR convention for
/// metallic-roughness packing (G = roughness, B = metallic in a single texture).
/// </para>
/// <para>
/// <b>De-duplication:</b> <see cref="SourcePath"/> is the prim path of the source material
/// in the stage; the spawn system uses it as a stable cache key so multiple meshes / subsets
/// that bind the same material share a single ECS material entry.
/// </para>
/// <para>
/// <b>Texture loading is out of scope</b> for this payload: <see cref="SceneTextureRef"/>
/// only carries the resolved asset path and the sampling parameters. A separate texture
/// loader / asset pipeline turns those into GPU resources.
/// </para>
/// </remarks>
/// <seealso cref="SceneMeshPayload"/>
/// <seealso cref="SceneTextureRef"/>
/// <seealso cref="SceneAlphaMode"/>
public sealed class SceneMaterialPayload
{
    /// <summary>Display name (typically the source material prim's leaf name).</summary>
    public string Name { get; init; } = "Material";

    /// <summary>
    /// Source prim path of the material in the stage (e.g. <c>"/teapot/Looks/teapotMat"</c>).
    /// Used as the cache key for <see cref="SceneMeshSubset.MaterialPath"/> resolution at
    /// spawn time, and to round-trip back to the writer.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Linear-RGBA base color factor multiplied with <see cref="BaseColorTexture"/> if present.
    /// Default is opaque white. Equivalent to <c>UsdPreviewSurface.diffuseColor</c> +
    /// <c>opacity</c> packed into one vector.
    /// </summary>
    public Vector4 BaseColorFactor { get; init; } = Vector4.One;

    /// <summary>RGBA texture for base color; <c>null</c> when not authored.</summary>
    public SceneTextureRef? BaseColorTexture { get; init; }

    /// <summary>Scalar metallic factor multiplied into the B channel of <see cref="MetallicRoughnessTexture"/>.</summary>
    public float MetallicFactor { get; init; }

    /// <summary>Scalar roughness factor multiplied into the G channel of <see cref="MetallicRoughnessTexture"/>.</summary>
    public float RoughnessFactor { get; init; } = 1f;

    /// <summary>
    /// Combined metallic-roughness texture using the glTF packing convention
    /// (G = roughness, B = metallic). Readers map the separate <c>UsdPreviewSurface</c>
    /// <c>metallic</c> / <c>roughness</c> inputs into this single reference when both
    /// resolve to the same texture file; otherwise <c>null</c> and the factors are used.
    /// </summary>
    public SceneTextureRef? MetallicRoughnessTexture { get; init; }

    /// <summary>Tangent-space normal map; <c>null</c> when not authored.</summary>
    public SceneTextureRef? NormalTexture { get; init; }

    /// <summary>Scalar multiplier applied to the sampled normal vectors (default 1.0).</summary>
    public float NormalScale { get; init; } = 1f;

    /// <summary>Linear emissive color factor (multiplied with <see cref="EmissiveTexture"/> if present).</summary>
    public Vector3 EmissiveFactor { get; init; } = Vector3.Zero;

    /// <summary>Emissive texture; <c>null</c> when not authored.</summary>
    public SceneTextureRef? EmissiveTexture { get; init; }

    /// <summary>Ambient occlusion texture (R channel); <c>null</c> when not authored.</summary>
    public SceneTextureRef? OcclusionTexture { get; init; }

    /// <summary>Strength of the occlusion contribution (default 1.0).</summary>
    public float OcclusionStrength { get; init; } = 1f;

    /// <summary>How the alpha channel of <see cref="BaseColorFactor"/> / <see cref="BaseColorTexture"/> is interpreted.</summary>
    public SceneAlphaMode AlphaMode { get; init; } = SceneAlphaMode.Opaque;

    /// <summary>Alpha threshold used when <see cref="AlphaMode"/> is <see cref="SceneAlphaMode.Mask"/>. Ignored otherwise.</summary>
    public float AlphaCutoff { get; init; } = 0.5f;

    /// <summary>
    /// When <c>true</c>, the surface is rendered from both sides (no back-face culling).
    /// Mirrors <c>UsdGeomGprim.doubleSided</c>.
    /// </summary>
    public bool DoubleSided { get; init; }

}

/// <summary>Reference to a texture file that backs a <see cref="SceneMaterialPayload"/> input.</summary>
/// <param name="AssetPath">
/// Resolved asset path of the texture file, relative to the model file. It may be a
/// virtual <c>__embedded__/</c> path to a texture inside the model file, which the texture
/// loader reads from memory.
/// </param>
/// <param name="UvSet">
/// UV channel index used to sample this texture (0 = <see cref="SceneMeshPayload.Uv0"/>,
/// 1 = <see cref="SceneMeshPayload.Uv1"/>). Resolved from the
/// <c>UsdPrimvarReader_float2.varname</c> upstream of the <c>UsdUVTexture</c> node.
/// </param>
/// <param name="WrapS">Texture-coordinate wrap mode along the S axis.</param>
/// <param name="WrapT">Texture-coordinate wrap mode along the T axis.</param>
public sealed record SceneTextureRef(
    string AssetPath,
    int UvSet = 0,
    SceneWrapMode WrapS = SceneWrapMode.Repeat,
    SceneWrapMode WrapT = SceneWrapMode.Repeat);

/// <summary>How a material's alpha channel is interpreted at render time.</summary>
public enum SceneAlphaMode
{
    /// <summary>Alpha is ignored; the surface is fully opaque.</summary>
    Opaque,

    /// <summary>
    /// A cutout, which discards a fragment whose sampled alpha is below
    /// <see cref="SceneMaterialPayload.AlphaCutoff"/>.
    /// </summary>
    Mask,

    /// <summary>Alpha-blended translucency. Requires depth-sorted rendering.</summary>
    Blend,
}

/// <summary>Texture-coordinate wrap mode (matches <c>UsdUVTexture</c> + glTF semantics).</summary>
public enum SceneWrapMode
{
    /// <summary>Coordinates outside [0,1] wrap around (default for <c>UsdUVTexture</c>).</summary>
    Repeat,

    /// <summary>Coordinates outside [0,1] mirror back into the range.</summary>
    Mirror,

    /// <summary>Coordinates outside [0,1] clamp to the texture edge.</summary>
    Clamp,

    /// <summary>Coordinates outside [0,1] sample a constant border color (typically transparent black).</summary>
    Black,
}
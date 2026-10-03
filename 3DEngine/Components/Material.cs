using System.Numerics;

namespace Engine;

/// <summary>
/// Runtime material component. Carries the PBR factors and (optional) texture handles
/// translated from a <see cref="SceneMaterialPayload"/> by <see cref="SceneSpawner"/>,
/// or set explicitly by gameplay code.
/// </summary>
/// <remarks>
/// <para>
/// <b>Texture handles:</b> the five PBR map slots
/// (<see cref="BaseColorTexture"/>, <see cref="MetallicRoughnessTexture"/>,
/// <see cref="NormalTexture"/>, <see cref="EmissiveTexture"/>,
/// <see cref="OcclusionTexture"/>) are <see cref="Handle{T}"/>s that may be
/// <see cref="Handle{T}.Invalid"/> when no texture is bound. Use
/// <c>handle.IsValid</c> to test before sampling. The renderer is responsible for
/// uploading the underlying <see cref="Texture"/> assets and binding them; the
/// component itself is GPU-agnostic.
/// </para>
/// <para>
/// <b>Color-space convention</b> (matches glTF / USD): <see cref="BaseColorTexture"/>
/// and <see cref="EmissiveTexture"/> store sRGB-encoded values; the other three are
/// linear. <see cref="SceneSpawner"/> requests the right encoding from the asset server
/// at load time via the <c>"srgb"</c> / <c>"linear"</c> sub-asset label honoured by
/// <see cref="TextureAssetLoader"/>.
/// </para>
/// </remarks>
/// <seealso cref="Mesh"/>
/// <seealso cref="Transform"/>
/// <seealso cref="SceneMaterialPayload"/>
/// <seealso cref="Texture"/>
public struct Material
{
    /// <summary>
    /// Base albedo / base-color factor (linear RGBA, 0..1). Multiplied with
    /// <see cref="BaseColorTexture"/>'s sample when one is bound.
    /// </summary>
    public Vector4 Albedo;

    /// <summary>Metallic factor [0..1]. Multiplied with <see cref="MetallicRoughnessTexture"/>'s blue channel.</summary>
    public float MetallicFactor;

    /// <summary>Roughness factor [0..1]. Multiplied with <see cref="MetallicRoughnessTexture"/>'s green channel.</summary>
    public float RoughnessFactor;

    /// <summary>Linear emissive RGB. Added to the lit color; multiplied with <see cref="EmissiveTexture"/>'s sample.</summary>
    public Vector3 EmissiveFactor;

    /// <summary>Normal-map scale (glTF <c>normalTexture.scale</c>).</summary>
    public float NormalScale;

    /// <summary>Occlusion-map strength (glTF <c>occlusionTexture.strength</c>).</summary>
    public float OcclusionStrength;

    /// <summary>sRGB base-color texture; <see cref="Handle{T}.Invalid"/> when unbound.</summary>
    public Handle<Texture> BaseColorTexture;

    /// <summary>Linear metallic-roughness texture (glTF packing: B = metallic, G = roughness).</summary>
    public Handle<Texture> MetallicRoughnessTexture;

    /// <summary>Linear tangent-space normal map.</summary>
    public Handle<Texture> NormalTexture;

    /// <summary>sRGB emissive texture.</summary>
    public Handle<Texture> EmissiveTexture;

    /// <summary>Linear ambient-occlusion texture (single channel sampled from R).</summary>
    public Handle<Texture> OcclusionTexture;

    /// <summary>
    /// Stable handle to the authoring <see cref="MaterialDescription"/> in the world's
    /// <see cref="MaterialLibrary"/>. Populated by <see cref="SceneSpawner"/> (or by
    /// gameplay code that authors materials through <see cref="MaterialLibrary.Create"/>).
    /// The renderer keys per-material GPU resources (pipelines, descriptor sets, uniform
    /// buffers) by this handle. <see cref="MaterialHandle.IsValid"/>
    /// returns <c>false</c> when the entity carries only inline factors and should
    /// render with the shared fallback pipeline.
    /// </summary>
    public MaterialHandle Handle;

    /// <summary>
    /// Creates a material with only the albedo factor set. PBR factors default to
    /// metal=0 / rough=1 / emissive=0; texture slots are <see cref="Handle{T}.Invalid"/>.
    /// Convenience for callers that only need a flat-shaded color.
    /// </summary>
    /// <param name="albedo">RGBA albedo (0..1).</param>
    public Material(Vector4 albedo)
    {
        Albedo = albedo;
        MetallicFactor = 0f;
        RoughnessFactor = 1f;
        EmissiveFactor = Vector3.Zero;
        NormalScale = 1f;
        OcclusionStrength = 1f;
        BaseColorTexture = default;
        MetallicRoughnessTexture = default;
        NormalTexture = default;
        EmissiveTexture = default;
        OcclusionTexture = default;
        Handle = default;
    }
}

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
[SceneComponent]
public struct Material : IEquatable<Material>
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
    /// How <see cref="Albedo"/>'s and the base color texture's alpha are meant, as glTF says:
    /// blended where below one and drawn after the opaque meshes from far to near, cut out below
    /// <see cref="AlphaCutoff"/>, or ignored. The constructors set blend, so a color with alpha
    /// is see-through, and a material made with <c>new()</c> or <c>default</c> is opaque, glTF's own default.
    /// </summary>
    public MaterialAlphaMode AlphaMode;

    /// <summary>The alpha below which <see cref="MaterialAlphaMode.Mask"/> cuts the surface out.</summary>
    public float AlphaCutoff;

    /// <summary>Whether both sides of each face are drawn, as the constructors set, or the back faces left out.</summary>
    public bool DoubleSided;

    /// <summary>
    /// White and opaque. A default <see cref="Material"/> has an albedo of zero, which is
    /// transparent black and draws nothing.
    /// </summary>
    public static Material Default => new(Vector4.One);

    /// <summary>A material of a color as raylib's colors and image pixels are, sRGB-encoded, decoded into the linear <see cref="Albedo"/>.</summary>
    public Material(Color color) : this(new Vector4(Decode(color.R), Decode(color.G), Decode(color.B), color.A / 255f)) { }

    private static float Decode(byte value)
    {
        var c = value / 255f;
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

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
        AlphaMode = MaterialAlphaMode.Blend;
        AlphaCutoff = 0.5f;
        DoubleSided = true;
    }

    /// <summary>Whether every field is the same, compared field by field rather than through reflection.</summary>
    /// <remarks>
    /// The renderer keeps each mesh entity's draw while its material compares equal, so a material
    /// replaced by <c>Add</c>, which marks no change, still reaches the screen.
    /// </remarks>
    public readonly bool Equals(Material other) =>
        Albedo == other.Albedo && MetallicFactor == other.MetallicFactor && RoughnessFactor == other.RoughnessFactor
        && EmissiveFactor == other.EmissiveFactor && NormalScale == other.NormalScale && OcclusionStrength == other.OcclusionStrength
        && BaseColorTexture.Equals(other.BaseColorTexture) && MetallicRoughnessTexture.Equals(other.MetallicRoughnessTexture)
        && NormalTexture.Equals(other.NormalTexture) && EmissiveTexture.Equals(other.EmissiveTexture)
        && OcclusionTexture.Equals(other.OcclusionTexture) && Handle.Equals(other.Handle)
        && AlphaMode == other.AlphaMode && AlphaCutoff == other.AlphaCutoff && DoubleSided == other.DoubleSided;

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Material other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(Albedo, BaseColorTexture, Handle, AlphaMode);

    /// <summary>Whether two materials are the same in every field.</summary>
    public static bool operator ==(Material left, Material right) => left.Equals(right);

    /// <summary>Whether two materials differ in a field.</summary>
    public static bool operator !=(Material left, Material right) => !left.Equals(right);
}

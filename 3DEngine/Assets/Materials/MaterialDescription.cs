using System.Numerics;

namespace Engine;

/// <summary>
/// Backend-agnostic, mutable PBR material description owned by a <see cref="MaterialLibrary"/>.
/// Mirrors the glTF 2.0 metallic-roughness model and <c>UsdPreviewSurface</c> input names so
/// importers (USD, MaterialX, glTF) round-trip lossless and the renderer has a single shape
/// to consume.
/// </summary>
/// <remarks>
/// <para>
/// This is the <b>user-facing</b> material type. Backends like <c>Engine.Materials.X</c>
/// (MaterialX) translate their own document/graph representation into this structure and
/// hand it to <see cref="MaterialLibrary.Create(MaterialDescription)"/>. End-user code
/// authors materials directly via the same API:
/// </para>
/// <example>
/// <code>
/// var lib = ctx.World.Resource&lt;MaterialLibrary&gt;();
/// var gold = lib.Create(new MaterialDescription
/// {
///     Name = "Gold",
///     BaseColorFactor = new Vector4(1.0f, 0.86f, 0.57f, 1f),
///     MetallicFactor = 1f,
///     RoughnessFactor = 0.2f,
/// });
/// </code>
/// </example>
/// </remarks>
/// <seealso cref="MaterialHandle"/>
/// <seealso cref="MaterialLibrary"/>
public sealed class MaterialDescription
{
    /// <summary>Display name (typically the source material's leaf name).</summary>
    public string Name { get; set; } = "Material";

    /// <summary>
    /// Stable identifier for de-duplication and re-binding across reloads (e.g. a USD prim
    /// path or MaterialX <c>surfacematerial</c> name). <c>null</c> for procedurally
    /// authored materials.
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>Linear-RGBA base-color factor multiplied with <see cref="BaseColorTexture"/> if present.</summary>
    public Vector4 BaseColorFactor { get; set; } = Vector4.One;

    /// <summary>RGBA texture for base color; <c>null</c> when not authored.</summary>
    public MaterialTextureRef? BaseColorTexture { get; set; }

    /// <summary>Scalar metallic factor multiplied into the B channel of <see cref="MetallicRoughnessTexture"/>.</summary>
    public float MetallicFactor { get; set; }

    /// <summary>Scalar roughness factor multiplied into the G channel of <see cref="MetallicRoughnessTexture"/>.</summary>
    public float RoughnessFactor { get; set; } = 1f;

    /// <summary>
    /// Combined metallic-roughness texture using the glTF packing convention
    /// (G = roughness, B = metallic). <c>null</c> when not authored.
    /// </summary>
    public MaterialTextureRef? MetallicRoughnessTexture { get; set; }

    /// <summary>Tangent-space normal map; <c>null</c> when not authored.</summary>
    public MaterialTextureRef? NormalTexture { get; set; }

    /// <summary>Scalar multiplier applied to the sampled normal vectors (default 1.0).</summary>
    public float NormalScale { get; set; } = 1f;

    /// <summary>Linear emissive color factor (multiplied with <see cref="EmissiveTexture"/> if present).</summary>
    public Vector3 EmissiveFactor { get; set; } = Vector3.Zero;

    /// <summary>Emissive texture; <c>null</c> when not authored.</summary>
    public MaterialTextureRef? EmissiveTexture { get; set; }

    /// <summary>Ambient-occlusion texture (R channel); <c>null</c> when not authored.</summary>
    public MaterialTextureRef? OcclusionTexture { get; set; }

    /// <summary>Strength of the occlusion contribution (default 1.0).</summary>
    public float OcclusionStrength { get; set; } = 1f;

    /// <summary>How the alpha channel of <see cref="BaseColorFactor"/> / <see cref="BaseColorTexture"/> is interpreted.</summary>
    public MaterialAlphaMode AlphaMode { get; set; } = MaterialAlphaMode.Opaque;

    /// <summary>Alpha threshold used when <see cref="AlphaMode"/> is <see cref="MaterialAlphaMode.Mask"/>. Ignored otherwise.</summary>
    public float AlphaCutoff { get; set; } = 0.5f;

    /// <summary>When <c>true</c>, the surface is rendered from both sides (no back-face culling).</summary>
    public bool DoubleSided { get; set; }

    /// <summary>Returns a shallow clone of this description (texture refs are records and so safely shared).</summary>
    public MaterialDescription Clone() => new()
    {
        Name = Name,
        SourcePath = SourcePath,
        BaseColorFactor = BaseColorFactor,
        BaseColorTexture = BaseColorTexture,
        MetallicFactor = MetallicFactor,
        RoughnessFactor = RoughnessFactor,
        MetallicRoughnessTexture = MetallicRoughnessTexture,
        NormalTexture = NormalTexture,
        NormalScale = NormalScale,
        EmissiveFactor = EmissiveFactor,
        EmissiveTexture = EmissiveTexture,
        OcclusionTexture = OcclusionTexture,
        OcclusionStrength = OcclusionStrength,
        AlphaMode = AlphaMode,
        AlphaCutoff = AlphaCutoff,
        DoubleSided = DoubleSided,
    };
}
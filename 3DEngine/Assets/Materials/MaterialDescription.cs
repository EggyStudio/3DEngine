using System.Numerics;

namespace Engine;

/// <summary>
/// Backend-agnostic, mutable PBR material description owned by a <see cref="MaterialLibrary"/>.
/// It follows glTF 2.0's metallic-roughness model, with <c>UsdPreviewSurface</c>'s input names,
/// so an imported material keeps what its file says and the renderer reads one shape.
/// </summary>
/// <remarks>
/// <para>
/// This is the <b>user-facing</b> material type. An importer translates its file's materials
/// into it and hands them to <see cref="MaterialLibrary.Create(MaterialDescription)"/>, and a
/// program makes materials through the same call:
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
internal sealed class MaterialDescription
{
    /// <summary>Display name (typically the source material's leaf name).</summary>
    public string Name { get; set; } = "Material";

    /// <summary>
    /// A stable name, as a model file's material path, by which a material is found again
    /// across reloads and made once. <c>null</c> for a material made in code.
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
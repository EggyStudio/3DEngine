using System.Numerics;
using System.Runtime.InteropServices;
using StbImageSharp;

namespace Engine;

/// <summary>
/// What a mesh is drawn with, which is a color multiplied with a texture when it has one, how
/// metallic and rough it is, a normal map, and a shader of the program's own.
/// </summary>
/// <remarks>
/// The surface follows glTF's metallic-roughness model, which every format Assimp reads is mapped
/// onto. It shows under light entities. A world with none draws the color and the texture unlit,
/// as raylib does, with the light the material gives off. The color and the texture are sRGB, as
/// raylib's colors are, and the model pass decodes them to light them in linear space. The maps
/// are linear.
/// </remarks>
public record struct ModelMaterial(Color Color, Texture2D Texture = default)
{
    /// <summary>How metallic the surface is, from 0 (plastic, stone, wood) to 1 (bare metal), multiplied by the blue of <see cref="MetallicRoughnessMap"/>.</summary>
    public float Metallic { get; set; } = 0;

    /// <summary>How rough the surface is, from 0 (a mirror) to 1 (chalk), multiplied by the green of <see cref="MetallicRoughnessMap"/>.</summary>
    public float Roughness { get; set; } = 0.5f;

    /// <summary>A tangent-space normal map, with up toward the top of the image, as glTF has it. A default texture means none.</summary>
    public Texture2D NormalMap { get; set; }

    /// <summary>How strongly <see cref="NormalMap"/> bends the surface, 1 as authored.</summary>
    public float NormalScale { get; set; } = 1;

    /// <summary>A map with roughness in green and metallic in blue, as glTF packs them. A default texture means none.</summary>
    public Texture2D MetallicRoughnessMap { get; set; }

    /// <summary>
    /// How the color's and the texture's alpha are meant. Blend, the default, lets what is behind
    /// show through where alpha is below one, drawn after the opaque meshes. Mask cuts out what
    /// is below <see cref="AlphaCutoff"/> and keeps the rest solid. Opaque ignores alpha, as a
    /// glTF material that says so is drawn.
    /// </summary>
    public MaterialAlphaMode AlphaMode { get; set; } = MaterialAlphaMode.Blend;

    /// <summary>The alpha below which <see cref="MaterialAlphaMode.Mask"/> cuts the surface out.</summary>
    public float AlphaCutoff { get; set; } = 0.5f;

    /// <summary>
    /// Whether both sides of each face are drawn. Only a face's front is unless this is set, the
    /// side its corners go round counterclockwise, as raylib draws a face, so a maze's roof hides
    /// nothing from above. A leaf or a flag seen from behind sets it, and a glTF file says for each
    /// of its materials.
    /// </summary>
    public bool DoubleSided { get; set; }

    /// <summary>Whether the surface casts shadows, true unless set, false for a glow, a light's bulb or an effect that would darken what is under it.</summary>
    public bool CastsShadows { get; set; } = true;

    /// <summary>The color of the light the surface gives off whatever lights it, black for none.</summary>
    public Color Emissive { get; set; } = Color.Black;

    /// <summary>How bright <see cref="Emissive"/> is, 1 for the color as it is and more for a light that blooms past white.</summary>
    public float EmissiveIntensity { get; set; } = 1;

    /// <summary>An sRGB map multiplying <see cref="Emissive"/>, as a screen's picture or a sign's letters. A default texture means none.</summary>
    public Texture2D EmissiveMap { get; set; }

    /// <summary>A map whose red darkens the light from all around in the surface's creases, as glTF has it. A default texture means none.</summary>
    public Texture2D OcclusionMap { get; set; }

    /// <summary>How strongly <see cref="OcclusionMap"/> darkens, from 0 to 1.</summary>
    public float OcclusionStrength { get; set; } = 1;

    /// <summary>
    /// How far, in world units, light that enters the surface travels under it before it leaves,
    /// as it does in skin, wax, marble or a leaf, 0 for none, which is the default.
    /// </summary>
    /// <remarks>
    /// Light scattered under the surface softens the line between lit and shadowed and takes the
    /// colors that travel farthest past it, as red does in skin. It is worked out over the
    /// window's frame for an opaque or masked surface drawn with the model pass's own shader, so a
    /// render texture and a reflection probe's faces draw the surface without it.
    /// </remarks>
    public float SubsurfaceRadius { get; set; }

    /// <summary>
    /// How far each of red, green and blue travels under the surface, each a share of
    /// <see cref="SubsurfaceRadius"/> from 0 to 255 of it, white for all alike, as (255, 90, 60)
    /// for skin, whose red goes farthest.
    /// </summary>
    public Color SubsurfaceColor { get; set; } = Color.White;

    /// <summary>
    /// A shader that draws the mesh in place of the model pass's own, which imports
    /// <c>modelpass</c> and has its uniforms set by name. A default shader uses the model pass's.
    /// </summary>
    /// <remarks>As raylib's <c>material.shader</c>: <c>model.Materials[0].Shader = LoadShader("toon.slang");</c>.</remarks>
    public Shader Shader { get; set; }
}

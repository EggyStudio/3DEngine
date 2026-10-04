using System.Numerics;

namespace Engine;

/// <summary>One mesh drawn this frame.</summary>
/// <param name="Mesh">The <see cref="MeshStore"/> id.</param>
/// <param name="World">Model to world space.</param>
/// <param name="ViewProjection">World to clip space, the camera the draw was recorded through.</param>
/// <param name="Color">Multiplied with the texture and the shading.</param>
/// <param name="Texture">The <see cref="TextureStore"/> id, or 0 for none.</param>
/// <param name="Target">The render target the mesh draws into, or 0 for the window.</param>
/// <param name="Shader">The <see cref="ShaderStore"/> id of the material's own shader, or 0 for the model pass's.</param>
/// <param name="Uniforms">That shader's uniform values as they were when the draw was recorded, laid out as it declares them.</param>
/// <param name="Metallic">How metallic the surface is, from 0 to 1.</param>
/// <param name="Roughness">How rough the surface is, from 0 (a mirror) to 1.</param>
/// <param name="NormalMap">The <see cref="TextureStore"/> id of a tangent-space normal map, or 0 for none.</param>
/// <param name="NormalScale">How strongly the normal map bends the surface.</param>
/// <param name="MetallicRoughnessMap">The <see cref="TextureStore"/> id of a map with roughness in green and metallic in blue, or 0 for none.</param>
/// <param name="Emission">The light the surface gives off, linear, which may pass 1.</param>
/// <param name="EmissiveMap">The <see cref="TextureStore"/> id of an sRGB map multiplying <paramref name="Emission"/>, or 0 for none.</param>
/// <param name="OcclusionMap">The <see cref="TextureStore"/> id of a map whose red darkens the light from all around, or 0 for none.</param>
/// <param name="OcclusionStrength">How strongly the occlusion map darkens, from 0 to 1.</param>
/// <param name="AlphaMode">How alpha is meant, blended where below one, cut out below <paramref name="AlphaCutoff"/>, or ignored.</param>
/// <param name="AlphaCutoff">The alpha below which a masked surface is cut out.</param>
/// <param name="TextureTranslucent">Whether the base color texture has alpha between clear and solid somewhere.</param>
public readonly record struct ModelDraw(int Mesh, Matrix4x4 World, Matrix4x4 ViewProjection, Color Color, int Texture, int Target = 0,
    int Shader = 0, byte[]? Uniforms = null, float Metallic = 0, float Roughness = 0.5f, int NormalMap = 0, float NormalScale = 1,
    int MetallicRoughnessMap = 0, Vector3 Emission = default, int EmissiveMap = 0, int OcclusionMap = 0, float OcclusionStrength = 1,
    MaterialAlphaMode AlphaMode = MaterialAlphaMode.Blend, float AlphaCutoff = 0.5f, bool TextureTranslucent = false)
{
    /// <summary>
    /// Whether what is behind shows through, so the draw comes after the opaque ones, in order:
    /// a blended material whose color or base color texture has alpha below one.
    /// </summary>
    public bool IsTranslucent => AlphaMode == MaterialAlphaMode.Blend && (Color.A < 255 || TextureTranslucent);
}

/// <summary>
/// The meshes recorded for the current frame by <c>DrawModel</c> and <c>DrawMesh</c>, drawn by
/// <see cref="ModelNode"/> after the ECS meshes and before the immediate shapes, and cleared at
/// <see cref="Stage.First"/>.
/// </summary>
public sealed class ModelDrawList
{
    private readonly object _gate = new();
    private readonly List<ModelDraw> _draws = [];

    /// <summary>The meshes recorded this frame, in recording order.</summary>
    public IReadOnlyList<ModelDraw> Draws => _draws;

    /// <summary>Records a mesh.</summary>
    public void Add(in ModelDraw draw)
    {
        lock (_gate) _draws.Add(draw);
    }

    /// <summary>Records several meshes under one lock, as a system recording thousands does.</summary>
    public void AddRange(ReadOnlySpan<ModelDraw> draws)
    {
        lock (_gate) _draws.AddRange(draws);
    }

    /// <summary>Forgets every recorded mesh.</summary>
    public void Clear()
    {
        lock (_gate) _draws.Clear();
    }
}

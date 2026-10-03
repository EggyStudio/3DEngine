using System.Numerics;

namespace Engine;

/// <summary>One mesh drawn this frame.</summary>
/// <param name="Mesh">The <see cref="MeshStore"/> id.</param>
/// <param name="World">Model to world space.</param>
/// <param name="ViewProjection">World to clip space, the camera the draw was recorded through.</param>
/// <param name="Color">Multiplied with the texture and the shading.</param>
/// <param name="Texture">The <see cref="TextureStore"/> id, or 0 for none.</param>
/// <param name="Target">The render target the mesh draws into, or 0 for the window.</param>
public readonly record struct ModelDraw(int Mesh, Matrix4x4 World, Matrix4x4 ViewProjection, Color Color, int Texture, int Target = 0);

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

    /// <summary>Forgets every recorded mesh.</summary>
    public void Clear()
    {
        lock (_gate) _draws.Clear();
    }
}

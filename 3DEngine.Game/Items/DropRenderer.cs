using System.Numerics;
using System.Runtime.InteropServices;
using static Engine.E3D;

namespace Engine.Game;

/// <summary>Draws the stacks lying in the world, each item one small mesh drawn instanced, turning slowly and bobbing.</summary>
/// <remarks>
/// A block's stack is a cube a quarter of a block a side in its top's, sides' and bottom's colors,
/// and an item of its own a flat square of its color. They cast no shadow, which keeps them out of
/// the scene's distance field as well, where each, moving every frame, would be stamped as a box.
/// </remarks>
public sealed class DropRenderer : IDisposable
{
    private const float Distance = 48;

    private readonly ModelMaterial _material = new(Color.White) { AlphaMode = MaterialAlphaMode.Opaque, Roughness = 0.8f, CastsShadows = false };
    private readonly ModelMesh?[] _meshes = new ModelMesh?[Items.All.Count];
    private readonly List<Matrix4x4>[] _draws = [.. Items.All.Select(_ => new List<Matrix4x4>())];

    /// <summary>Instanced draws the last <see cref="Draw"/> made.</summary>
    public int Draws { get; private set; }

    /// <summary>Draws the stacks within reach of the eye at a time in seconds, which turns them. Called inside <c>BeginMode3D</c>.</summary>
    public void Draw(IReadOnlyList<ItemDrop> drops, Vector3 eye, double time)
    {
        foreach (var list in _draws) list.Clear();
        foreach (var drop in drops)
        {
            if (Vector3.DistanceSquared(drop.Body.Position, eye) > Distance * Distance) continue;
            // Each stack turns and bobs out of step with the others, by its own age.
            var spin = (float)time * 1.2f + drop.Delay * 7 + drop.Body.Position.X * 3;
            var bob = 0.06f * (1 + MathF.Sin((float)time * 2.5f + drop.Body.Position.Z * 5));
            _draws[drop.Item.Id].Add(Matrix4x4.CreateRotationY(spin) * Matrix4x4.CreateTranslation(drop.Body.Position + new Vector3(0, 0.125f + bob, 0)));
        }
        var draws = 0;
        for (int i = 0; i < _draws.Length; i++)
        {
            if (_draws[i].Count == 0) continue;
            var mesh = _meshes[i] ??= Build(Items.All[i]);
            DrawMeshInstanced(mesh, _material, CollectionsMarshal.AsSpan(_draws[i]));
            draws++;
        }
        Draws = draws;
    }

    // A cube for a block in its faces' colors, and a flat square for an item of its own.
    private static ModelMesh Build(Item item)
    {
        var mesh = new BoxMesh();
        if (item.Block is { } id && Blocks.Get(id) is { Shape: null } block)
        {
            Color top = Surfaces.All[block.Top].Plain, side = Surfaces.All[block.Side].Plain, bottom = Surfaces.All[block.Bottom].Plain;
            mesh.Add(new Vector3(-0.125f), new Vector3(0.125f), [side, side, top, bottom, side, side]);
        }
        else
        {
            var half = item.Block is null ? new Vector3(0.15f, 0.15f, 0.03f) : new Vector3(0.1f, 0.15f, 0.1f);
            mesh.Add(-half, half, item.Color);
        }
        return mesh.Upload();
    }

    public void Dispose()
    {
        foreach (var mesh in _meshes)
            if (mesh is { } m) UnloadMesh(m);
    }
}

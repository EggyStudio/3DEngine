using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Game;

/// <summary>
/// A layer of blocky clouds drifting east over the world, as Minecraft's are: cells of 12 by 12
/// blocks, 4 high, at 120 blocks up, each a box whose faces against a cloud beside it are left out.
/// </summary>
/// <remarks>
/// <para>
/// Which cells hold a cloud follows a slow noise of the world's seed, so the same sky passes over
/// the same world. The cells around the eye are one mesh in cloud space, made again only when the
/// eye moves a cell's width against the clouds, and the drift within a cell is the mesh's
/// transform, so the clouds move each frame without a new mesh.
/// </para>
/// <para>
/// They are see-through and cast no shadow, which keeps them out of the scene's distance field, so
/// they neither shade the sun's light nor stand in the light that bounces, as Minecraft's cast no
/// shadow. Their faces are shaded by the way they face, the top brightest, and lit by the model
/// pass, so they darken at night, and they fade into the sky at twice the render distance through a
/// copy of the terrain shader of their own.
/// </para>
/// </remarks>
public sealed class Clouds : IDisposable
{
    public const int Height = 120;
    private const int Cell = 12;
    private const int Thick = 4;
    // Blocks a second the clouds drift east, about Minecraft's.
    private const float Speed = 0.6f;

    private Noise _noise = new(11);
    private readonly Shader _shader;
    private readonly int _fogColor, _fogStart, _fogEnd;
    private readonly ModelMaterial _material;
    private ModelMesh _mesh;
    private (int X, int Z, int Reach) _built = (int.MinValue, int.MinValue, 0);
    private double _drift;

    public Clouds()
    {
        _shader = LoadShader("resources/shaders/terrain.slang");
        (_fogColor, _fogStart, _fogEnd) = (GetShaderLocation(_shader, "fogColor"), GetShaderLocation(_shader, "fogStart"), GetShaderLocation(_shader, "fogEnd"));
        _material = new ModelMaterial(new Color(255, 255, 255, 254)) { AlphaMode = MaterialAlphaMode.Blend, Roughness = 1, CastsShadows = false, Shader = _shader };
    }

    /// <summary>Takes the clouds of a world's seed, as a world opens.</summary>
    public void Follow(int seed)
    {
        _noise = new Noise(seed + 11);
        _built = (int.MinValue, int.MinValue, 0);
    }

    /// <summary>Whether the clouds are drawn.</summary>
    public bool Shown { get; set; } = true;

    /// <summary>Faces in the mesh drawn, for the settings window.</summary>
    public int Faces { get; private set; }

    /// <summary>Whether a cell of cloud space holds a cloud.</summary>
    public bool IsCloud(int x, int z) => _noise.Fractal(x / 7f, z / 7f, 2) > 0.12f;

    public void Update(float seconds) => _drift += seconds * Speed;

    /// <summary>Draws the clouds within <paramref name="renderDistance"/> columns' reach of the eye, twice the land's, fading into <paramref name="sky"/>.</summary>
    public void Draw(Vector3 eye, int renderDistance, Color sky)
    {
        if (!Shown) return;
        var reach = renderDistance * Section.Size * 2;
        var drift = (float)(_drift % (Cell * 4096.0));
        // The eye's cell in cloud space, which the clouds have drifted east of.
        int cx = (int)MathF.Floor((eye.X - drift) / Cell), cz = (int)MathF.Floor(eye.Z / Cell);
        if ((cx, cz, reach) != _built) Build(cx, cz, reach);
        if (!_mesh.IsValid) return;

        SetShaderValue(_shader, _fogColor, new Vector4(sky.R, sky.G, sky.B, 255) / 255);
        SetShaderValue(_shader, _fogStart, reach * 0.6f);
        SetShaderValue(_shader, _fogEnd, reach);
        DrawMesh(_mesh, _material, Matrix4x4.CreateTranslation(cx * Cell + drift, Height, cz * Cell));
    }

    // The clouds of the cells within reach of a cell, as boxes in cloud space about that cell's
    // corner, a face left out where a cloud stands beside it.
    private void Build(int cx, int cz, int reach)
    {
        _built = (cx, cz, reach);
        if (_mesh.IsValid) UnloadMesh(_mesh);
        _mesh = default;

        var cells = reach / Cell + 1;
        var vertices = new List<ModelVertex>();
        var colors = new List<Color>();
        var indices = new List<uint>();
        for (int dz = -cells; dz <= cells; dz++)
            for (int dx = -cells; dx <= cells; dx++)
            {
                if (dx * dx + dz * dz > cells * cells || !IsCloud(cx + dx, cz + dz)) continue;
                var corner = new Vector3(dx * Cell, 0, dz * Cell);
                // The top and the bottom always show, and a side only where no cloud stands beside it.
                Face(corner, Vector3.UnitY, new Color(255, 255, 255, 204));
                Face(corner, -Vector3.UnitY, new Color(196, 198, 208, 204));
                if (!IsCloud(cx + dx + 1, cz + dz)) Face(corner, Vector3.UnitX, new Color(226, 228, 234, 204));
                if (!IsCloud(cx + dx - 1, cz + dz)) Face(corner, -Vector3.UnitX, new Color(226, 228, 234, 204));
                if (!IsCloud(cx + dx, cz + dz + 1)) Face(corner, Vector3.UnitZ, new Color(214, 216, 224, 204));
                if (!IsCloud(cx + dx, cz + dz - 1)) Face(corner, -Vector3.UnitZ, new Color(214, 216, 224, 204));
            }
        Faces = indices.Count / 6;
        if (indices.Count == 0) return;
        // The store keeps the arrays it is given until the frame uploads them, so they are the mesh's own.
        _mesh = UploadMesh([.. vertices], [.. indices], [.. colors], null);

        void Face(Vector3 corner, Vector3 normal, Color color)
        {
            var size = new Vector3(Cell, Thick, Cell);
            // Two axes across the face, ordered so its corners run counterclockwise seen from outside.
            var (u, v) = normal.X != 0 ? (Vector3.UnitY, Vector3.UnitZ) : normal.Y != 0 ? (Vector3.UnitZ, Vector3.UnitX) : (Vector3.UnitX, Vector3.UnitY);
            if (normal.X + normal.Y + normal.Z < 0) (u, v) = (v, u);
            var origin = corner + Vector3.Max(normal, Vector3.Zero) * size;
            var first = (uint)vertices.Count;
            foreach (var at in (ReadOnlySpan<Vector3>)[Vector3.Zero, u, u + v, v])
            {
                vertices.Add(new ModelVertex(origin + at * size, normal, new Vector2(at.X + at.Z, at.Y + at.Z)));
                colors.Add(color);
            }
            indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
        }
    }

    public void Dispose()
    {
        if (_mesh.IsValid) UnloadMesh(_mesh);
        UnloadShader(_shader);
    }
}

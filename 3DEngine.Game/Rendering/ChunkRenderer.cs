using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using static Engine.Engine3D;

namespace Engine.Game;

/// <summary>
/// Keeps a mesh for each section, meshes sections again as the world changes, and draws those
/// within the render distance with the blocks that give off light.
/// </summary>
/// <remarks>
/// <para>
/// A section is one mesh in one white material, each block's color in its vertices, which the
/// scene's distance field reads as the model pass does, so the light that bounces takes each
/// block's color from the mesh it shares with its neighbors. A section meshed again with the same
/// faces keeps its mesh and takes only new colors, which keep its vertex array, the field knowing a
/// mesh by that, so a change of light leaves the section settled in the field.
/// </para>
/// <para>
/// A material's light is one for its whole draw, so a block that gives off light is a cube drawn
/// on its own, copies of one mesh in one instanced draw for each kind. Sections are drawn by
/// distance alone and not culled to the view, since a mesh left out of a frame leaves the field
/// and the light it gave or blocked goes with it until it settles again. Every section within the
/// render distance is drawn each frame, through the field's gather and each pass's culling, so the
/// render distance sets much of the frame's cost, which <see cref="Draws"/> counts.
/// </para>
/// </remarks>
public sealed class ChunkRenderer : IDisposable
{
    private sealed class SectionMeshes(Matrix4x4 transform)
    {
        public readonly Matrix4x4 Transform = transform;
        public ModelMesh Mesh;
        public ModelVertex[] Vertices = [];
        public uint[] Indices = [];
        public Color[] Colors = [];
        public readonly List<(BlockId Block, Matrix4x4 At)> Emitters = [];
    }

    // Every section's faces are drawn with this, their colors in their vertices.
    private static readonly ModelMaterial Terrain = new(Color.White) { AlphaMode = MaterialAlphaMode.Opaque, Roughness = 0.9f };

    private readonly Dictionary<SectionKey, SectionMeshes> _sections = [];
    private readonly SectionMesher _mesher = new();
    private readonly ModelMesh _cube = GenMeshCube(1, 1, 1);
    private readonly List<Matrix4x4>[] _emitterDraws = [.. Blocks.All.Select(_ => new List<Matrix4x4>())];
    private readonly List<SectionKey> _queue = [];
    private ModelMaterial[] _lamps = [];
    private float _glowScale = 1;

    public ChunkRenderer() => BuildLamps();

    /// <summary>What the light of every emissive surface is multiplied by.</summary>
    public float GlowScale
    {
        get => _glowScale;
        set
        {
            if (value == _glowScale) return;
            _glowScale = value;
            BuildLamps();
        }
    }

    /// <summary>Draw calls the last <see cref="Draw"/> made, each instanced draw of lamps counted once.</summary>
    public int Draws { get; private set; }

    public int Triangles { get; private set; }

    public int Lamps { get; private set; }

    /// <summary>Sections holding faces or lamps.</summary>
    public int Sections => _sections.Count;

    /// <summary>Whether faces are darkened by light levels, which takes effect as sections are shaded again.</summary>
    public bool LightLevels
    {
        get => _mesher.LightLevels;
        set => _mesher.LightLevels = value;
    }

    /// <summary>Whether corners are darkened where blocks meet, which takes effect as sections are shaded again.</summary>
    public bool CornerShade
    {
        get => _mesher.CornerShade;
        set => _mesher.CornerShade = value;
    }

    private void BuildLamps() => _lamps = [.. Surfaces.All.Select(s => s.Material(_glowScale))];

    /// <summary>
    /// Meshes every section an edit changed, then those whose shading changed and those that loaded,
    /// nearest the eye first, until <paramref name="budgetMs"/> of the frame is spent.
    /// </summary>
    public void Update(VoxelWorld world, Vector3 eye, double budgetMs)
    {
        foreach (var key in world.Edited)
            if (!Remesh(world, key)) world.Loaded.Add(key);
        world.Reshaded.ExceptWith(world.Edited);
        world.Edited.Clear();

        if (world.Loaded.Count == 0 && world.Reshaded.Count == 0) return;
        int ex = (int)MathF.Floor(eye.X) >> Section.Shift, ey = (int)MathF.Floor(eye.Y) >> Section.Shift, ez = (int)MathF.Floor(eye.Z) >> Section.Shift;
        _queue.Clear();
        _queue.AddRange(world.Reshaded);
        _queue.AddRange(world.Loaded);
        _queue.Sort((a, b) => Distance(a).CompareTo(Distance(b)));
        int Distance(SectionKey k) => (k.X - ex) * (k.X - ex) + (k.Y - ey) * (k.Y - ey) + (k.Z - ez) * (k.Z - ez);

        var watch = Stopwatch.StartNew();
        foreach (var key in _queue)
        {
            if (watch.Elapsed.TotalMilliseconds > budgetMs) break;
            // A section whose neighbor column has not arrived is dropped rather than kept, since the
            // neighbor's arrival marks it again.
            if (!world.Reshaded.Remove(key) & !world.Loaded.Remove(key)) continue;
            Remesh(world, key);
        }
    }

    private bool Remesh(VoxelWorld world, SectionKey key)
    {
        if (!world.HasColumn(key.X, key.Z))
        {
            Forget(key);
            return true;
        }
        if (!_mesher.Mesh(world, key)) return false;

        if (_mesher.Vertices.Count == 0 && _mesher.Emitters.Count == 0)
        {
            Forget(key);
            return true;
        }
        if (!_sections.TryGetValue(key, out var entry))
            _sections[key] = entry = new SectionMeshes(Matrix4x4.CreateTranslation(key.Origin));

        var vertices = CollectionsMarshal.AsSpan(_mesher.Vertices);
        var indices = CollectionsMarshal.AsSpan(_mesher.Indices);
        var colors = CollectionsMarshal.AsSpan(_mesher.Colors);
        if (vertices.SequenceEqual(entry.Vertices) && indices.SequenceEqual(entry.Indices))
        {
            if (!colors.SequenceEqual(entry.Colors))
            {
                UpdateMeshBuffer<Color>(entry.Mesh, 3, colors, 0);
                entry.Colors = colors.ToArray();
            }
        }
        else
        {
            if (entry.Mesh.IsValid) UnloadMesh(entry.Mesh);
            // The store keeps the arrays it is given until the frame uploads them, so each mesh has
            // arrays of its own that nothing changes afterward.
            (entry.Vertices, entry.Indices, entry.Colors) = (vertices.ToArray(), indices.ToArray(), colors.ToArray());
            entry.Mesh = entry.Vertices.Length > 0 ? UploadMesh(entry.Vertices, entry.Indices, entry.Colors, null) : default;
        }

        entry.Emitters.Clear();
        foreach (var (index, block) in _mesher.Emitters)
        {
            var local = new Vector3(index & Section.Mask, index >> 8, (index >> 4) & Section.Mask);
            entry.Emitters.Add((block, Matrix4x4.CreateTranslation(key.Origin + local + new Vector3(0.5f))));
        }
        return true;
    }

    private void Forget(SectionKey key)
    {
        if (_sections.Remove(key, out var entry) && entry.Mesh.IsValid) UnloadMesh(entry.Mesh);
    }

    /// <summary>Lets go of the meshes of a column's sections, as it unloads.</summary>
    public void ForgetColumn(int x, int z)
    {
        for (int y = 0; y < ChunkColumn.SectionCount; y++) Forget(new SectionKey(x, y, z));
    }

    /// <summary>Lets go of every section's meshes, as a new world begins.</summary>
    public void Clear()
    {
        foreach (var key in _sections.Keys.ToList()) Forget(key);
    }

    /// <summary>Draws the sections whose column lies within <paramref name="renderDistance"/> columns of the eye's, and the lamps in them.</summary>
    public void Draw(Vector3 eye, int renderDistance)
    {
        int cx = (int)MathF.Floor(eye.X) >> Section.Shift, cz = (int)MathF.Floor(eye.Z) >> Section.Shift;
        var reach = renderDistance * renderDistance + renderDistance;
        int draws = 0, triangles = 0, lamps = 0;
        foreach (var list in _emitterDraws) list.Clear();

        foreach (var (key, entry) in _sections)
        {
            int dx = key.X - cx, dz = key.Z - cz;
            if (dx * dx + dz * dz > reach) continue;
            if (entry.Mesh.IsValid)
            {
                DrawMesh(entry.Mesh, Terrain, entry.Transform);
                draws++;
                triangles += entry.Mesh.TriangleCount;
            }
            foreach (var (block, at) in entry.Emitters) _emitterDraws[(int)block].Add(at);
        }

        for (int block = 0; block < _emitterDraws.Length; block++)
        {
            var list = _emitterDraws[block];
            if (list.Count == 0) continue;
            DrawMeshInstanced(_cube, _lamps[Blocks.All[block].Top], CollectionsMarshal.AsSpan(list));
            draws++;
            triangles += _cube.TriangleCount * list.Count;
            lamps += list.Count;
        }
        (Draws, Triangles, Lamps) = (draws, triangles, lamps);
    }

    public void Dispose()
    {
        Clear();
        UnloadMesh(_cube);
    }
}

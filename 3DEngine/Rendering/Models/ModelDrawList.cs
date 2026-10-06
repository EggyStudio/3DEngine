using System.Numerics;
using System.Runtime.InteropServices;

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
/// <param name="DoubleSided">Whether both sides of each face are drawn, or the back faces left out.</param>
/// <param name="ShaderTextures">The textures the draw's shader samples, by their index in the program's textures, then the storage buffers it reads, by their index in its buffers, or null for none.</param>
/// <param name="CastsShadow">Whether the mesh is drawn into the shadow map, which a sky around the camera is not.</param>
/// <param name="CullFront">Whether the front faces are left out in place of the back ones, where any are, as rlgl's rlSetCullFace sets.</param>
/// <param name="Points">Whether the triangles are drawn as a point at each corner, as rlgl's point mode draws them.</param>
/// <param name="ColorBlend">Whether the draw is blended with what is behind it by its alpha, which rlgl's rlDisableColorBlend turns off, so it is written as it is.</param>
/// <param name="DepthWrite">Whether the draw writes its depth, which rlgl's rlDisableDepthMask turns off, so what is drawn after it shows over it, as a sky drawn around the camera does.</param>
internal readonly record struct ModelDraw(int Mesh, Matrix4x4 World, Matrix4x4 ViewProjection, Color Color, int Texture, int Target = 0,
    int Shader = 0, byte[]? Uniforms = null, float Metallic = 0, float Roughness = 0.5f, int NormalMap = 0, float NormalScale = 1,
    int MetallicRoughnessMap = 0, Vector3 Emission = default, int EmissiveMap = 0, int OcclusionMap = 0, float OcclusionStrength = 1,
    MaterialAlphaMode AlphaMode = MaterialAlphaMode.Blend, float AlphaCutoff = 0.5f, bool TextureTranslucent = false,
    bool DoubleSided = true, int[]? ShaderTextures = null, bool CastsShadow = true, bool CullFront = false, bool Points = false, bool ColorBlend = true,
    bool DepthWrite = true)
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
internal sealed class ModelDrawList
{
    private readonly object _gate = new();
    private readonly List<ModelDraw> _draws = [];
    private readonly List<InstanceGroup> _groups = [];

    /// <summary>The meshes recorded this frame, in recording order.</summary>
    public IReadOnlyList<ModelDraw> Draws => _draws;

    /// <summary>The runs of finished instances recorded this frame, which mesh entities fill.</summary>
    internal IReadOnlyList<InstanceGroup> Groups => _groups;

    /// <summary>
    /// The mesh entities that moved since the frame before, with where each was then, which
    /// per-object motion blur draws as their movement. Recorded only while it is on.
    /// </summary>
    internal MovingDraws Moving { get; } = new();

    /// <summary>Whether nothing is recorded this frame, neither a draw nor a group.</summary>
    public bool IsEmpty => _draws.Count == 0 && _groups.Count == 0;

    /// <summary>
    /// The camera the first mesh drawn into the window was recorded through, a draw before a group,
    /// which the shadow's cascades are fitted to, or null when nothing is drawn into the window.
    /// </summary>
    internal Matrix4x4? WindowViewProjection => ViewProjectionOf(0);

    /// <summary>
    /// The camera the first mesh drawn into <paramref name="target"/> was recorded through, a draw
    /// before a group, or null when nothing is drawn into it.
    /// </summary>
    internal Matrix4x4? ViewProjectionOf(int target)
    {
        foreach (var draw in Span)
            if (draw.Target == target) return draw.ViewProjection;
        foreach (var group in _groups)
            if (group.Count > 0 && group.Template.Target == target) return group.Template.ViewProjection;
        return null;
    }

    /// <summary>The render targets meshes are drawn into this frame, in the order each first is, the window left out.</summary>
    internal List<int> Targets()
    {
        var targets = new List<int>();
        foreach (var draw in Span)
            if (draw.Target != 0 && !targets.Contains(draw.Target)) targets.Add(draw.Target);
        foreach (var group in _groups)
            if (group.Count > 0 && group.Template.Target != 0 && !targets.Contains(group.Template.Target)) targets.Add(group.Template.Target);
        return targets;
    }

    /// <summary>Records a group of instances for the frame, drawn as one batch of the group's mesh and maps.</summary>
    internal void AddGroup(InstanceGroup group)
    {
        lock (_gate) _groups.Add(group);
    }

    /// <summary>The same draws as a span, which the model pass reads by reference, since each is about 200 bytes.</summary>
    internal ReadOnlySpan<ModelDraw> Span => CollectionsMarshal.AsSpan(_draws);

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
        lock (_gate)
        {
            _draws.Clear();
            _groups.Clear();
            Moving.Clear();
        }
    }
}

/// <summary>
/// Instances of one mesh that share their maps, sides and alpha mode, each written whole where it
/// is recorded, so the model pass copies them into its ring as they are and draws them as one batch.
/// </summary>
/// <remarks>
/// <para>
/// Mesh entities are recorded this way, tens of thousands a frame, where a <see cref="ModelDraw"/>
/// of about 200 bytes each, sorted into batches again by the pass, cost more than the rest of
/// their frame (RENDERING.md section 6). A group is kept by its recorder from frame to frame and
/// filled again each frame, and the draw list holds it until the list is cleared.
/// </para>
/// <para>
/// The instances are in segments, each written by one thread into a buffer of its own, so entities
/// recorded in parallel share a group without a lock, and the pass copies the segments one after
/// another.
/// </para>
/// </remarks>
internal sealed class InstanceGroup
{
    private readonly List<(ModelRenderer.Instance[] Items, int Count)> _segments = [];

    /// <summary>What every instance shares: the mesh, the maps, the sides, the alpha mode and the camera. Its world matrix and factors are unused.</summary>
    public ModelDraw Template;

    /// <summary>
    /// The sphere around the mesh in its own space, which the pass culls the instances by, a
    /// block at a time. An infinite radius, the default, culls none.
    /// </summary>
    public (Vector3 Center, float Radius) Sphere = (Vector3.Zero, float.PositiveInfinity);

    /// <summary>How many instances this frame holds.</summary>
    public int Count { get; private set; }

    /// <summary>Forgets last frame's segments.</summary>
    public void Clear()
    {
        _segments.Clear();
        Count = 0;
    }

    /// <summary>Adds the first <paramref name="count"/> instances of <paramref name="items"/>, which stay unwritten until the draw list is cleared.</summary>
    public void Add(ModelRenderer.Instance[] items, int count)
    {
        if (count == 0) return;
        _segments.Add((items, count));
        Count += count;
    }

    /// <summary>Adds each segment to <paramref name="copies"/>, with where its first instance goes when the group's first goes at <paramref name="first"/>.</summary>
    public void AddSegments(List<(ModelRenderer.Instance[] Items, int Count, int At, InstanceGroup Group)> copies, int first)
    {
        foreach (var (items, count) in _segments)
        {
            copies.Add((items, count, first, this));
            first += count;
        }
    }

    /// <summary>Copies the frame's instances into <paramref name="destination"/>, segment after segment.</summary>
    public void CopyTo(Span<ModelRenderer.Instance> destination)
    {
        foreach (var (items, count) in _segments)
        {
            items.AsSpan(0, count).CopyTo(destination);
            destination = destination[count..];
        }
    }

    /// <summary>The frame's instances, copied into an array.</summary>
    public ModelRenderer.Instance[] ToArray()
    {
        var all = new ModelRenderer.Instance[Count];
        CopyTo(all);
        return all;
    }
}

/// <summary>
/// The mesh entities that moved since the frame before, by mesh, each as its world now and then,
/// which per-object motion blur draws as one run of instances a mesh.
/// </summary>
/// <remarks>
/// The entities are found in ranges, a thread each, and each range keeps lists of its own by mesh,
/// which the pass reads mesh by mesh. The lists are kept from frame to frame and emptied, so a frame
/// of moving entities allocates nothing.
/// </remarks>
internal sealed class MovingDraws
{
    /// <summary>One range's moving entities by mesh.</summary>
    internal sealed class Part
    {
        private readonly Dictionary<int, List<MovingInstance>> _byMesh = [];

        public int Count { get; private set; }

        public IReadOnlyDictionary<int, List<MovingInstance>> ByMesh => _byMesh;

        /// <summary>The list mesh <paramref name="mesh"/>'s moving entities are added to.</summary>
        public List<MovingInstance> Of(int mesh)
        {
            if (!_byMesh.TryGetValue(mesh, out var list)) _byMesh[mesh] = list = [];
            return list;
        }

        /// <summary>Adds an entity's movement to its mesh's list.</summary>
        public void Add(List<MovingInstance> list, in MovingInstance instance)
        {
            list.Add(instance);
            Count++;
        }

        public void Clear()
        {
            if (Count == 0) return;
            foreach (var list in _byMesh.Values) list.Clear();
            Count = 0;
        }
    }

    private readonly List<Part> _parts = [];
    private readonly HashSet<int> _meshes = [];

    /// <summary>How many entities moved this frame.</summary>
    public int Count
    {
        get
        {
            var count = 0;
            foreach (var part in _parts) count += part.Count;
            return count;
        }
    }

    /// <summary>Range <paramref name="index"/>'s lists, which <see cref="EnsureParts"/> made.</summary>
    public Part this[int index] => _parts[index];

    /// <summary>Makes sure there are <paramref name="parts"/> ranges' lists, before the threads fill them.</summary>
    public void EnsureParts(int parts)
    {
        while (_parts.Count < parts) _parts.Add(new Part());
    }

    /// <summary>Each mesh some entity of moved this frame, with its lists in range order.</summary>
    public IEnumerable<(int Mesh, IEnumerable<List<MovingInstance>> Runs)> ByMesh()
    {
        _meshes.Clear();
        foreach (var part in _parts)
            foreach (var (mesh, list) in part.ByMesh)
                if (list.Count > 0) _meshes.Add(mesh);
        foreach (var mesh in _meshes)
            yield return (mesh, _parts.Where(part => part.ByMesh.TryGetValue(mesh, out var list) && list.Count > 0).Select(part => part.ByMesh[mesh]));
    }

    /// <summary>Empties every list, keeping them for the next frame.</summary>
    public void Clear()
    {
        foreach (var part in _parts) part.Clear();
    }
}

/// <summary>A moving entity's world now and the frame before, three rows each, as the model pass writes a world and velocity.slang reads it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MovingInstance
{
    public Vector4 NowX, NowY, NowZ, ThenX, ThenY, ThenZ;

    public MovingInstance(in Matrix4x4 now, in Matrix4x4 then)
    {
        (NowX, NowY, NowZ) = (new(now.M11, now.M21, now.M31, now.M41), new(now.M12, now.M22, now.M32, now.M42), new(now.M13, now.M23, now.M33, now.M43));
        (ThenX, ThenY, ThenZ) = (new(then.M11, then.M21, then.M31, then.M41), new(then.M12, then.M22, then.M32, then.M42), new(then.M13, then.M23, then.M33, then.M43));
    }
}

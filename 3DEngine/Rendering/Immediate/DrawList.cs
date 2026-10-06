using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>One vertex of the immediate pass: a position, a texture coordinate and a color, 24 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ImmediateVertex(Vector3 Position, Vector2 Uv, Color Color);

/// <summary>A run of vertices in the <see cref="DrawList"/> drawn with one pipeline and one transform.</summary>
/// <param name="Topology">Whether the vertices are lines or triangles.</param>
/// <param name="Transform">Model to clip space, in <c>System.Numerics</c> order (row vectors).</param>
/// <param name="DepthTest">Whether the run is tested against and writes the depth buffer.</param>
/// <param name="FirstIndex">Where the run's indices start in <see cref="DrawList.Indices"/>.</param>
/// <param name="IndexCount">How many indices the run draws, two a line and three a triangle.</param>
/// <param name="Texture">The <see cref="TextureStore"/> id the run samples, or 0 for plain white.</param>
/// <param name="Uniforms">The named uniforms of its shader as they were when the run was recorded, laid out as the shader declares them, or null for none.</param>
/// <param name="Textures">The textures its shader samples, by their index in the program's textures, then the storage buffers it reads, by their index in its buffers, as they were set when the run was recorded, or null for none.</param>
/// <param name="Target">The render target the run draws into, or 0 for the window.</param>
/// <param name="Shader">The <see cref="ShaderStore"/> id the run draws with, or 0 for the engine's own.</param>
/// <param name="Params">The values the shader reads with <c>param</c>.</param>
/// <param name="Blend">How the run is laid over what is there.</param>
/// <param name="Scissor">The pixels of the target the run is kept to, or null for all of them.</param>
/// <param name="Cull">Which faces of the run's triangles are left out by their winding.</param>
/// <param name="Factors">The factors a custom <paramref name="Blend"/> combines by, and default for the other modes.</param>
internal readonly record struct DrawBatch(
    PrimitiveTopology Topology, Matrix4x4 Transform, bool DepthTest, int FirstIndex, int IndexCount,
    int Texture = 0, int Target = 0, int Shader = 0, ShaderParams Params = default, byte[]? Uniforms = null, int[]? Textures = null,
    BlendMode Blend = BlendMode.Alpha, ScissorRect? Scissor = null, CullMode Cull = CullMode.None, BlendFactors Factors = default);

/// <summary>A rectangle of a target's pixels, from its top left.</summary>
internal readonly record struct ScissorRect(int X, int Y, int Width, int Height);

/// <summary>
/// The lines and triangles recorded for the current frame by the flat API's <c>Draw</c> calls,
/// drawn by <see cref="ImmediateNode"/> after the meshes and before ImGui, and cleared at
/// <see cref="Stage.First"/>.
/// </summary>
/// <remarks>
/// <para>
/// A run of consecutive calls with the same topology, transform, depth mode and texture is one
/// <see cref="DrawBatch"/>, so a scene of shapes drawn through one camera costs two draw calls
/// whatever the number of shapes. This is the scheme raylib's rlgl layer uses.
/// </para>
/// <para>
/// Shapes are drawn by index, so a quad is four vertices and six indices rather than six vertices,
/// which a sprite, a glyph and a rectangle all are.
/// </para>
/// <para>
/// Recording takes a lock while a stage runs systems on several threads, since a system calling a
/// <c>Draw</c> function may then run on a worker, and the order of two systems' shapes within the
/// stage is not fixed. Outside one no lock is taken, as it was half of what recording a sprite
/// cost, so a program drawing from a thread of its own, outside the schedule, is not covered,
/// as raylib's drawing is not.
/// </para>
/// </remarks>
internal sealed class DrawList
{
    /// <summary>
    /// The blend pixels written into a render target are drawn with, which replaces what is there,
    /// alpha and all, as an upload does. It is none of raylib's modes, so the API does not offer it.
    /// </summary>
    public const BlendMode Replace = (BlendMode)(-1);

    private readonly object _gate = new();

    // Holds the lock while systems run in parallel, and nothing otherwise.
    private Guard Enter() => new(Schedule.RunningInParallel ? _gate : null);

    private readonly ref struct Guard
    {
        private readonly object? _gate;

        public Guard(object? gate)
        {
            _gate = gate;
            if (gate is not null) Monitor.Enter(gate);
        }

        public void Dispose()
        {
            if (_gate is not null) Monitor.Exit(_gate);
        }
    }
    private ImmediateVertex[] _vertices = new ImmediateVertex[4096];
    private int _count;
    private uint[] _indices = new uint[6144];
    private int _indexCount;
    private readonly List<DrawBatch> _batches = [];

    // The last batch while calls keep extending it. Its topology, texture and index count are kept
    // here, and the count is written into the list only when another batch opens or the batches
    // are read. Comparing a
    // whole DrawBatch and copying it back on every shape cost most of a sprite's recording. Any
    // change of transform, target, shader, blend mode or scissor closes it.
    private bool _open;
    private PrimitiveTopology _openTopology;
    private int _openTexture;
    private int _openCount;

    /// <summary>The transform the next recorded shapes are drawn through.</summary>
    public Matrix4x4 Transform { get; private set; } = Matrix4x4.Identity;

    /// <summary>Whether the next recorded shapes are depth tested.</summary>
    public bool DepthTest { get; private set; }

    /// <summary>The render target the next recorded shapes draw into, or 0 for the window.</summary>
    public int Target { get; private set; }

    /// <summary>The shader the next recorded shapes draw with, or 0 for the engine's own.</summary>
    public int Shader { get; private set; }

    /// <summary>The values that shader reads.</summary>
    public ShaderParams Params { get; private set; }

    /// <summary>That shader's named uniforms, or null for none.</summary>
    public byte[]? Uniforms { get; private set; }

    /// <summary>That shader's textures, or null for none.</summary>
    public int[]? Textures { get; private set; }

    /// <summary>How the next recorded shapes are laid over what is there.</summary>
    public BlendMode Blend { get; private set; }

    /// <summary>The pixels the next recorded shapes are kept to, or null for the whole target.</summary>
    public ScissorRect? Scissor { get; private set; }

    /// <summary>Which faces of the next recorded triangles are left out, none until a program turns culling on.</summary>
    public CullMode Cull { get; private set; }

    /// <summary>Leaves out the faces of the following triangles that <paramref name="cull"/> names.</summary>
    public void SetCull(CullMode cull)
    {
        using (Enter())
        {
            Cull = cull;
            Close();
        }
    }

    /// <summary>The factors a custom <see cref="Blend"/> combines the next recorded shapes by.</summary>
    public BlendFactors Factors { get; private set; }

    /// <summary>
    /// Lays the following shapes over what is there by <paramref name="blend"/>, the custom modes by
    /// <paramref name="factors"/>.
    /// </summary>
    public void SetBlend(BlendMode blend, BlendFactors factors = default)
    {
        using (Enter())
        {
            Blend = blend;
            // Kept only where they are read, so batches of the other modes still join.
            Factors = blend is BlendMode.Custom or BlendMode.CustomSeparate ? factors : default;
            Close();
        }
    }

    /// <summary>Keeps the following shapes to <paramref name="scissor"/>, or to the whole target for null.</summary>
    public void SetScissor(ScissorRect? scissor)
    {
        using (Enter())
        {
            Scissor = scissor;
            Close();
        }
    }

    /// <summary>
    /// Draws the following shapes with shader <paramref name="shader"/>, reading
    /// <paramref name="parameters"/> and its named <paramref name="uniforms"/>, or with the
    /// engine's own when it is 0.
    /// </summary>
    public void SetShader(int shader, ShaderParams parameters, byte[]? uniforms = null, int[]? textures = null)
    {
        using (Enter())
        {
            Shader = shader;
            Params = parameters;
            Uniforms = uniforms;
            Textures = textures;
            Close();
        }
    }

    private readonly Dictionary<int, Color?> _targetClears = [];

    /// <summary>
    /// The color each render target drawn this frame is cleared to, or null for one that keeps what
    /// it held, which nothing cleared this frame.
    /// </summary>
    public IReadOnlyDictionary<int, Color?> TargetClears => _targetClears;

    /// <summary>Sends the following shapes to render target <paramref name="target"/>, or 0 for the window.</summary>
    public void SetTarget(int target)
    {
        using (Enter())
        {
            Target = target;
            Close();
            if (target != 0) _targetClears.TryAdd(target, null);
        }
    }

    /// <summary>
    /// Draws render target <paramref name="target"/> this frame, cleared to <paramref name="clear"/>
    /// unless something drawn into it set its clear already, as a camera entity drawing into it does.
    /// </summary>
    public void UseTarget(int target, Color clear)
    {
        using (Enter())
            if (target != 0) _targetClears.TryAdd(target, clear);
    }

    /// <summary>Sets the color the current render target is cleared to before it is drawn this frame.</summary>
    public void SetTargetClear(Color color)
    {
        using (Enter())
            if (Target != 0) _targetClears[Target] = color;
    }

    /// <summary>Every vertex recorded this frame.</summary>
    public ReadOnlySpan<ImmediateVertex> Vertices => _vertices.AsSpan(0, _count);

    /// <summary>Every index recorded this frame, into <see cref="Vertices"/>, which the batches draw runs of.</summary>
    public ReadOnlySpan<uint> Indices => _indices.AsSpan(0, _indexCount);

    /// <summary>The runs the vertices are drawn in, in recording order.</summary>
    public IReadOnlyList<DrawBatch> Batches
    {
        get
        {
            using (Enter())
            {
                Close();
                return _batches;
            }
        }
    }

    /// <summary>Sets the transform and depth mode the following shapes are recorded with.</summary>
    public void SetTransform(Matrix4x4 transform, bool depthTest)
    {
        using (Enter())
        {
            Transform = transform;
            DepthTest = depthTest;
            Close();
        }
    }

    // The model transform the following shapes' positions are moved by as they are recorded, as
    // rlgl moves each vertex by its matrix stack, and whether it is anything but the identity.
    private Matrix4x4 _model = Matrix4x4.Identity;
    private bool _hasModel;

    /// <summary>
    /// Moves the positions of the following shapes by <paramref name="model"/> as they are
    /// recorded, before the batch's transform, as rlgl's matrix stack moves each vertex.
    /// </summary>
    public void SetModel(Matrix4x4 model)
    {
        using (Enter())
        {
            _model = model;
            _hasModel = !model.IsIdentity;
        }
    }

    // Moves the positions of the count vertices from at by the model transform. Called inside
    // Enter, after the vertices are written.
    private void Place(int at, int count)
    {
        if (!_hasModel) return;
        for (int i = at; i < at + count; i++)
            _vertices[i] = _vertices[i] with { Position = Vector3.Transform(_vertices[i].Position, _model) };
    }

    /// <summary>Records a line.</summary>
    public void Line(Vector3 from, Vector3 to, Color color)
    {
        using (Enter())
        {
            var at = Reserve(PrimitiveTopology.LineList, 2, 0);
            _vertices[at] = new ImmediateVertex(from, default, color);
            _vertices[at + 1] = new ImmediateVertex(to, default, color);
            Place(at, 2);
            Index(at, 0, 1);
        }
    }

    /// <summary>
    /// Records a line, a triangle or a quad of two triangles over its corners in order around its
    /// edge, by how many corners there are, each corner with its own texture coordinate and color,
    /// sampling <paramref name="texture"/>, as rlgl's vertices are given one at a time.
    /// </summary>
    public void Primitive(ReadOnlySpan<ImmediateVertex> corners, int texture)
    {
        if (corners.Length is < 2 or > 4) throw new ArgumentOutOfRangeException(nameof(corners), "A primitive has two, three or four corners.");
        using (Enter())
        {
            var at = Reserve(corners.Length == 2 ? PrimitiveTopology.LineList : PrimitiveTopology.TriangleList, corners.Length, texture);
            corners.CopyTo(_vertices.AsSpan(at));
            Place(at, corners.Length);
            if (corners.Length == 2) Index(at, 0, 1);
            else if (corners.Length == 3) Index(at, 0, 1, 2);
            else QuadIndices(at);
        }
    }

    /// <summary>
    /// Records a triangle, whose front face is the one its corners go counterclockwise around on
    /// the screen, as raylib's are, for when <see cref="Cull"/> leaves out back or front faces.
    /// </summary>
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        using (Enter())
        {
            var at = Reserve(PrimitiveTopology.TriangleList, 3, 0);
            _vertices[at] = new ImmediateVertex(a, default, color);
            _vertices[at + 1] = new ImmediateVertex(b, default, color);
            _vertices[at + 2] = new ImmediateVertex(c, default, color);
            Place(at, 3);
            Index(at, 0, 1, 2);
        }
    }

    /// <summary>Records a triangle with a color at each corner, blended across it, as a gradient is drawn.</summary>
    public void Triangle(Vector3 a, Color colorA, Vector3 b, Color colorB, Vector3 c, Color colorC)
    {
        using (Enter())
        {
            var at = Reserve(PrimitiveTopology.TriangleList, 3, 0);
            _vertices[at] = new ImmediateVertex(a, default, colorA);
            _vertices[at + 1] = new ImmediateVertex(b, default, colorB);
            _vertices[at + 2] = new ImmediateVertex(c, default, colorC);
            Place(at, 3);
            Index(at, 0, 1, 2);
        }
    }

    /// <summary>Records a quad as two triangles over its four corners, given in order around its edge.</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        using (Enter())
        {
            var at = Reserve(PrimitiveTopology.TriangleList, 4, 0);
            var corners = _vertices.AsSpan(at, 4);
            corners[0] = new ImmediateVertex(a, default, color);
            corners[1] = new ImmediateVertex(b, default, color);
            corners[2] = new ImmediateVertex(c, default, color);
            corners[3] = new ImmediateVertex(d, default, color);
            Place(at, 4);
            QuadIndices(at);
        }
    }

    /// <summary>
    /// Records a quad sampling <paramref name="texture"/>, as two triangles with corners in order
    /// around its edge, each corner with its texture coordinate.
    /// </summary>
    public void TexturedQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, Color tint, int texture)
    {
        using (Enter())
        {
            var at = Reserve(PrimitiveTopology.TriangleList, 4, texture);
            var corners = _vertices.AsSpan(at, 4);
            corners[0] = new ImmediateVertex(a, uvA, tint);
            corners[1] = new ImmediateVertex(b, uvB, tint);
            corners[2] = new ImmediateVertex(c, uvC, tint);
            corners[3] = new ImmediateVertex(d, uvD, tint);
            Place(at, 4);
            QuadIndices(at);
        }
    }

    /// <summary>Forgets every recorded shape, and returns to drawing in screen space.</summary>
    public void Clear()
    {
        using (Enter())
        {
            _count = 0;
            _indexCount = 0;
            _batches.Clear();
            _open = false;
            Transform = Matrix4x4.Identity;
            DepthTest = false;
            Target = 0;
            Shader = 0;
            Params = default;
            Uniforms = null;
            Textures = null;
            Blend = BlendMode.Alpha;
            Factors = default;
            Scissor = null;
            Cull = CullMode.None;
            (_model, _hasModel) = (Matrix4x4.Identity, false);
            _targetClears.Clear();
        }
    }

    // Grows the vertex array for the vertices of a shape and returns where they start. The batch is
    // opened or extended by Index, which counts what it draws. Called inside Enter.
    private int Reserve(PrimitiveTopology topology, int vertices, int texture)
    {
        if (_count + vertices > _vertices.Length)
            Array.Resize(ref _vertices, Math.Max(_vertices.Length * 2, _count + vertices));
        _reservedTopology = topology;
        _reservedTexture = texture;
        var at = _count;
        _count += vertices;
        return at;
    }

    private PrimitiveTopology _reservedTopology;
    private int _reservedTexture;

    // A quad's six indices, written out where the open batch takes them rather than through
    // Index's loop over offsets, since quads are most of what is drawn. Called inside Enter, after
    // Reserve.
    private void QuadIndices(int at)
    {
        if (_indexCount + 6 <= _indices.Length && _open && _openTopology == _reservedTopology && _openTexture == _reservedTexture)
        {
            var indices = _indices.AsSpan(_indexCount, 6);
            var first = (uint)at;
            indices[0] = first;
            indices[1] = first + 1;
            indices[2] = first + 2;
            indices[3] = first;
            indices[4] = first + 2;
            indices[5] = first + 3;
            _indexCount += 6;
            _openCount += 6;
            return;
        }
        Index(at, 0, 1, 2, 0, 2, 3);
    }

    // Appends a shape's indices, relative to its first vertex at, and extends or opens the batch they
    // belong to. Called inside Enter, after Reserve.
    private void Index(int at, params ReadOnlySpan<int> offsets)
    {
        var count = offsets.Length;
        if (_indexCount + count > _indices.Length)
            Array.Resize(ref _indices, Math.Max(_indices.Length * 2, _indexCount + count));
        var first = _indexCount;
        for (int i = 0; i < count; i++) _indices[first + i] = (uint)(at + offsets[i]);
        _indexCount += count;

        var topology = _reservedTopology;
        var texture = _reservedTexture;
        if (_open && _openTopology == topology && _openTexture == texture)
        {
            _openCount += count;
            return;
        }

        // A batch that matches the last one closed, as after a transform set to what it was, extends it.
        Close();
        if (_batches.Count > 0)
        {
            var last = _batches[^1];
            if (last.Topology == topology && last.DepthTest == DepthTest && last.Transform == Transform
                && last.Texture == texture && last.Target == Target && last.Shader == Shader && last.Params == Params
                && ReferenceEquals(last.Uniforms, Uniforms) && ReferenceEquals(last.Textures, Textures) && last.Blend == Blend
                && last.Factors == Factors && last.Scissor == Scissor && last.Cull == Cull && last.FirstIndex + last.IndexCount == first)
            {
                Open(topology, texture, last.IndexCount + count);
                return;
            }
        }

        _batches.Add(new DrawBatch(topology, Transform, DepthTest, first, count, texture, Target, Shader, Params, Uniforms, Textures, Blend, Scissor, Cull, Factors));
        Open(topology, texture, count);
    }

    private void Open(PrimitiveTopology topology, int texture, int count)
    {
        _open = true;
        _openTopology = topology;
        _openTexture = texture;
        _openCount = count;
    }

    // Writes the open batch's count into the list. Called inside Enter.
    private void Close()
    {
        if (!_open) return;
        _open = false;
        if (_batches[^1].IndexCount != _openCount)
            _batches[^1] = _batches[^1] with { IndexCount = _openCount };
    }
}

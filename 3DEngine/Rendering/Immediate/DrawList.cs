using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>One vertex of the immediate pass: a position, a texture coordinate and a color, 24 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ImmediateVertex(Vector3 Position, Vector2 Uv, Color Color);

/// <summary>A run of vertices in the <see cref="DrawList"/> drawn with one pipeline and one transform.</summary>
/// <param name="Topology">Whether the vertices are lines or triangles.</param>
/// <param name="Transform">Model to clip space, in <c>System.Numerics</c> order (row vectors).</param>
/// <param name="DepthTest">Whether the run is tested against and writes the depth buffer.</param>
/// <param name="FirstVertex">Index of the run's first vertex.</param>
/// <param name="VertexCount">Number of vertices in the run.</param>
/// <param name="Texture">The <see cref="TextureStore"/> id the run samples, or 0 for plain white.</param>
/// <param name="Uniforms">The named uniforms of its shader as they were when the run was recorded, laid out as the shader declares them, or null for none.</param>
/// <param name="Textures">The textures its shader samples, by their index in the program's textures, as they were set when the run was recorded, or null for none.</param>
/// <param name="Target">The render target the run draws into, or 0 for the window.</param>
/// <param name="Shader">The <see cref="ShaderStore"/> id the run draws with, or 0 for the engine's own.</param>
/// <param name="Params">The values the shader reads with <c>param</c>.</param>
/// <param name="Blend">How the run is laid over what is there.</param>
/// <param name="Scissor">The pixels of the target the run is kept to, or null for all of them.</param>
public readonly record struct DrawBatch(
    PrimitiveTopology Topology, Matrix4x4 Transform, bool DepthTest, int FirstVertex, int VertexCount,
    int Texture = 0, int Target = 0, int Shader = 0, ShaderParams Params = default, byte[]? Uniforms = null, int[]? Textures = null,
    BlendMode Blend = BlendMode.Alpha, ScissorRect? Scissor = null);

/// <summary>A rectangle of a target's pixels, from its top left.</summary>
public readonly record struct ScissorRect(int X, int Y, int Width, int Height);

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
/// Recording takes a lock, because a system calling a <c>Draw</c> function may run on a worker
/// thread in a parallel stage. The order of two systems' shapes within a stage is then not fixed.
/// </para>
/// </remarks>
public sealed class DrawList
{
    private readonly object _gate = new();
    private ImmediateVertex[] _vertices = new ImmediateVertex[4096];
    private int _count;
    private readonly List<DrawBatch> _batches = [];

    // The last batch while calls keep extending it. Its topology, texture and vertex count are kept
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

    /// <summary>Lays the following shapes over what is there by <paramref name="blend"/>.</summary>
    public void SetBlend(BlendMode blend)
    {
        lock (_gate)
        {
            Blend = blend;
            Close();
        }
    }

    /// <summary>Keeps the following shapes to <paramref name="scissor"/>, or to the whole target for null.</summary>
    public void SetScissor(ScissorRect? scissor)
    {
        lock (_gate)
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
        lock (_gate)
        {
            Shader = shader;
            Params = parameters;
            Uniforms = uniforms;
            Textures = textures;
            Close();
        }
    }

    private readonly Dictionary<int, Color> _targetClears = [];

    /// <summary>The color each render target drawn this frame is cleared to.</summary>
    public IReadOnlyDictionary<int, Color> TargetClears => _targetClears;

    /// <summary>Sends the following shapes to render target <paramref name="target"/>, or 0 for the window.</summary>
    public void SetTarget(int target)
    {
        lock (_gate)
        {
            Target = target;
            Close();
            if (target != 0) _targetClears.TryAdd(target, Color.Blank);
        }
    }

    /// <summary>Sets the color the current render target is cleared to before it is drawn this frame.</summary>
    public void SetTargetClear(Color color)
    {
        lock (_gate)
            if (Target != 0) _targetClears[Target] = color;
    }

    /// <summary>Every vertex recorded this frame.</summary>
    public ReadOnlySpan<ImmediateVertex> Vertices => _vertices.AsSpan(0, _count);

    /// <summary>The runs the vertices are drawn in, in recording order.</summary>
    public IReadOnlyList<DrawBatch> Batches
    {
        get
        {
            lock (_gate)
            {
                Close();
                return _batches;
            }
        }
    }

    /// <summary>Sets the transform and depth mode the following shapes are recorded with.</summary>
    public void SetTransform(Matrix4x4 transform, bool depthTest)
    {
        lock (_gate)
        {
            Transform = transform;
            DepthTest = depthTest;
            Close();
        }
    }

    /// <summary>Records a line.</summary>
    public void Line(Vector3 from, Vector3 to, Color color)
    {
        lock (_gate)
        {
            var at = Reserve(PrimitiveTopology.LineList, 2, 0);
            _vertices[at] = new ImmediateVertex(from, default, color);
            _vertices[at + 1] = new ImmediateVertex(to, default, color);
        }
    }

    /// <summary>Records a triangle. Both faces are drawn, so the winding does not matter.</summary>
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        lock (_gate)
        {
            var at = Reserve(PrimitiveTopology.TriangleList, 3, 0);
            _vertices[at] = new ImmediateVertex(a, default, color);
            _vertices[at + 1] = new ImmediateVertex(b, default, color);
            _vertices[at + 2] = new ImmediateVertex(c, default, color);
        }
    }

    /// <summary>Records a triangle with a color at each corner, blended across it, as a gradient is drawn.</summary>
    public void Triangle(Vector3 a, Color colorA, Vector3 b, Color colorB, Vector3 c, Color colorC)
    {
        lock (_gate)
        {
            var at = Reserve(PrimitiveTopology.TriangleList, 3, 0);
            _vertices[at] = new ImmediateVertex(a, default, colorA);
            _vertices[at + 1] = new ImmediateVertex(b, default, colorB);
            _vertices[at + 2] = new ImmediateVertex(c, default, colorC);
        }
    }

    /// <summary>Records a quad as two triangles, with corners in order around its edge.</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        Triangle(a, b, c, color);
        Triangle(a, c, d, color);
    }

    /// <summary>
    /// Records a quad sampling <paramref name="texture"/>, as two triangles with corners in order
    /// around its edge, each corner with its texture coordinate.
    /// </summary>
    public void TexturedQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, Color tint, int texture)
    {
        lock (_gate)
        {
            var at = Reserve(PrimitiveTopology.TriangleList, 6, texture);
            _vertices[at] = new ImmediateVertex(a, uvA, tint);
            _vertices[at + 1] = new ImmediateVertex(b, uvB, tint);
            _vertices[at + 2] = new ImmediateVertex(c, uvC, tint);
            _vertices[at + 3] = new ImmediateVertex(a, uvA, tint);
            _vertices[at + 4] = new ImmediateVertex(c, uvC, tint);
            _vertices[at + 5] = new ImmediateVertex(d, uvD, tint);
        }
    }

    /// <summary>Forgets every recorded shape, and returns to drawing in screen space.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _count = 0;
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
            Scissor = null;
            _targetClears.Clear();
        }
    }

    // Grows the vertex array and extends or opens the batch the vertices belong to. Called under
    // the lock.
    private int Reserve(PrimitiveTopology topology, int vertices, int texture)
    {
        if (_count + vertices > _vertices.Length)
            Array.Resize(ref _vertices, _vertices.Length * 2);

        var at = _count;
        _count += vertices;

        if (_open && _openTopology == topology && _openTexture == texture)
        {
            _openCount += vertices;
            return at;
        }

        // A batch that matches the last one closed, as after a transform set to what it was, extends it.
        Close();
        if (_batches.Count > 0)
        {
            var last = _batches[^1];
            if (last.Topology == topology && last.DepthTest == DepthTest && last.Transform == Transform
                && last.Texture == texture && last.Target == Target && last.Shader == Shader && last.Params == Params
                && ReferenceEquals(last.Uniforms, Uniforms) && ReferenceEquals(last.Textures, Textures) && last.Blend == Blend
                && last.Scissor == Scissor && last.FirstVertex + last.VertexCount == at)
            {
                Open(topology, texture, last.VertexCount + vertices);
                return at;
            }
        }

        _batches.Add(new DrawBatch(topology, Transform, DepthTest, at, vertices, texture, Target, Shader, Params, Uniforms, Textures, Blend, Scissor));
        Open(topology, texture, vertices);
        return at;
    }

    private void Open(PrimitiveTopology topology, int texture, int count)
    {
        _open = true;
        _openTopology = topology;
        _openTexture = texture;
        _openCount = count;
    }

    // Writes the open batch's count into the list. Called under the lock.
    private void Close()
    {
        if (!_open) return;
        _open = false;
        if (_batches[^1].VertexCount != _openCount)
            _batches[^1] = _batches[^1] with { VertexCount = _openCount };
    }
}

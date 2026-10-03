using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>One vertex of the immediate pass: a position and a color, sixteen bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ImmediateVertex(Vector3 Position, Color Color);

/// <summary>A run of vertices in the <see cref="DrawList"/> drawn with one pipeline and one transform.</summary>
/// <param name="Topology">Whether the vertices are lines or triangles.</param>
/// <param name="Transform">Model to clip space, in <c>System.Numerics</c> order (row vectors).</param>
/// <param name="DepthTest">Whether the run is tested against and writes the depth buffer.</param>
/// <param name="FirstVertex">Index of the run's first vertex.</param>
/// <param name="VertexCount">Number of vertices in the run.</param>
public readonly record struct DrawBatch(
    PrimitiveTopology Topology, Matrix4x4 Transform, bool DepthTest, int FirstVertex, int VertexCount);

/// <summary>
/// The lines and triangles recorded for the current frame by the flat API's <c>Draw</c> calls,
/// drawn by <see cref="ImmediateNode"/> after the meshes and before ImGui, and cleared at
/// <see cref="Stage.First"/>.
/// </summary>
/// <remarks>
/// <para>
/// A run of consecutive calls with the same topology, transform and depth mode is one
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

    /// <summary>The transform the next recorded shapes are drawn through.</summary>
    public Matrix4x4 Transform { get; private set; } = Matrix4x4.Identity;

    /// <summary>Whether the next recorded shapes are depth tested.</summary>
    public bool DepthTest { get; private set; }

    /// <summary>Every vertex recorded this frame.</summary>
    public ReadOnlySpan<ImmediateVertex> Vertices => _vertices.AsSpan(0, _count);

    /// <summary>The runs the vertices are drawn in, in recording order.</summary>
    public IReadOnlyList<DrawBatch> Batches => _batches;

    /// <summary>Sets the transform and depth mode the following shapes are recorded with.</summary>
    public void SetTransform(Matrix4x4 transform, bool depthTest)
    {
        lock (_gate)
        {
            Transform = transform;
            DepthTest = depthTest;
        }
    }

    /// <summary>Records a line.</summary>
    public void Line(Vector3 from, Vector3 to, Color color)
    {
        lock (_gate)
        {
            var at = Reserve(PrimitiveTopology.LineList, 2);
            _vertices[at] = new ImmediateVertex(from, color);
            _vertices[at + 1] = new ImmediateVertex(to, color);
        }
    }

    /// <summary>Records a triangle. Both faces are drawn, so the winding does not matter.</summary>
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        lock (_gate)
        {
            var at = Reserve(PrimitiveTopology.TriangleList, 3);
            _vertices[at] = new ImmediateVertex(a, color);
            _vertices[at + 1] = new ImmediateVertex(b, color);
            _vertices[at + 2] = new ImmediateVertex(c, color);
        }
    }

    /// <summary>Records a quad as two triangles, with corners in order around its edge.</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        Triangle(a, b, c, color);
        Triangle(a, c, d, color);
    }

    /// <summary>Forgets every recorded shape, and returns to drawing in screen space.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _count = 0;
            _batches.Clear();
            Transform = Matrix4x4.Identity;
            DepthTest = false;
        }
    }

    // Grows the vertex array and extends or opens the batch the vertices belong to. Called under
    // the lock.
    private int Reserve(PrimitiveTopology topology, int vertices)
    {
        if (_count + vertices > _vertices.Length)
            Array.Resize(ref _vertices, _vertices.Length * 2);

        var at = _count;
        _count += vertices;

        if (_batches.Count > 0)
        {
            var last = _batches[^1];
            if (last.Topology == topology && last.DepthTest == DepthTest && last.Transform == Transform
                && last.FirstVertex + last.VertexCount == at)
            {
                _batches[^1] = last with { VertexCount = last.VertexCount + vertices };
                return at;
            }
        }

        _batches.Add(new DrawBatch(topology, Transform, DepthTest, at, vertices));
        return at;
    }
}

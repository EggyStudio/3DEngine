using System.Numerics;

namespace Engine;

/// <summary>
/// What an <see cref="EditMesh"/> names, its vertices, its faces, its material slots, its selection
/// and the lines of its recipe, which the flat API's modeling calls change.
/// </summary>
/// <remarks>
/// <para>
/// A face is a loop of three or more vertex indices, wound counterclockwise seen from the side it
/// faces, kept whole until the mesh is built, with a material slot and the angle it is smoothed
/// across. Edges are not kept but read from the faces, each pair of corners that follow one another
/// round a face, so no operation has edges to keep in step.
/// </para>
/// <para>
/// A face's texture coordinates are its corners' places in meters, seen along the axis its normal
/// is nearest, read when the mesh is built, so they follow its corners wherever they move, until a
/// <c>uv</c> operation gives it coordinates of its own, which stay on its corners as they move and
/// are carried to the corners a cut gives it.
/// </para>
/// <para>
/// The selection is a set of vertices and a set of faces. Selecting a face selects its corners, so
/// an operation on vertices moves the faces selected, and an edge is selected where both its ends
/// are. A primitive selects what it made, so the next operation acts on it.
/// </para>
/// <para>
/// Measures are taken as given. The grid step is where <see cref="Snap"/> puts the selection, so a
/// typed measure is never rounded away, as a move of a fifth of a meter on a one meter grid would be.
/// One program shapes a mesh at a time, and nothing here locks, as a list does not.
/// </para>
/// </remarks>
internal sealed partial class EditMeshData
{
    /// <summary>The finest grid step, a sixteenth of a meter.</summary>
    public const float FinestGrid = 1f / 16f;

    /// <summary>The coarsest grid step, sixty-four meters.</summary>
    public const float CoarsestGrid = 64f;

    /// <summary>The name of the first material slot, which every new face takes.</summary>
    public const string DefaultSlot = "default";

    private readonly List<Vector3> _positions = [];
    private readonly List<Face> _faces = [];
    private readonly List<string> _slots = [DefaultSlot];
    private readonly HashSet<int> _selectedVertices = [];
    private readonly HashSet<int> _selectedFaces = [];

    // The edges as the faces have them, read again after a change rather than kept in step, since
    // every operation that moves a corner between faces would otherwise have to mend them.
    private Dictionary<(int Low, int High), EdgeUse>? _edges;

    /// <summary>A face, its corners in order round it, its slot, the angle it is smoothed across and its own texture coordinates.</summary>
    private sealed class Face(int[] corners, int slot, float smooth = 0f, Vector2[]? uvs = null)
    {
        public int[] Corners = corners;
        public int Slot = slot;

        /// <summary>The angle in degrees up to which its corners share a normal with the faces beside them, or zero for flat.</summary>
        public float Smooth = smooth;

        /// <summary>The texture coordinates of its corners, in their order, or null for the world-aligned default.</summary>
        public Vector2[]? Uvs = uvs;
    }

    /// <summary>How often the faces run along an edge from its lower vertex and from its higher.</summary>
    private struct EdgeUse
    {
        public int Forward;
        public int Backward;
    }

    public int VertexCount => _positions.Count;

    public int FaceCount => _faces.Count;

    /// <summary>How many edges the faces have between them, an edge two faces share counted once.</summary>
    public int EdgeCount => EdgeUses().Count;

    public IReadOnlyList<string> Slots => _slots;

    /// <summary>A count that moves on every change to what the mesh builds into, and not on a change of the selection.</summary>
    public int Version { get; private set; }

    /// <summary>The grid step <see cref="Snap"/> rounds to, one meter until <see cref="Grid"/> sets another.</summary>
    public float GridStep { get; private set; } = 1f;

    public Vector3 Position(int vertex) => _positions[vertex];

    /// <summary>A face's vertices, in order round it, counterclockwise seen from the side it faces.</summary>
    public ReadOnlySpan<int> Corners(int face) => _faces[face].Corners;

    public string SlotOf(int face) => _slots[_faces[face].Slot];

    public int SlotIndexOf(int face) => _faces[face].Slot;

    /// <summary>The angle in degrees up to which a face is smoothed into those beside it, or zero where it is drawn flat.</summary>
    public float SmoothOf(int face) => _faces[face].Smooth;

    /// <summary>A face's normal at twice its area, Newell's sum, which a smoothed corner's normal is weighed by.</summary>
    public Vector3 Area(int face) => Newell(_faces[face].Corners);

    /// <summary>The way a face faces, a length of one, or zero for a face with no area.</summary>
    /// <remarks>
    /// Newell's sum over the face's edges, which gives a face whose corners are not quite in one
    /// plane, or a concave one, the normal its outline means, where the cross product of two edges
    /// would give the normal of whichever corner was taken.
    /// </remarks>
    public Vector3 Normal(int face) => Unit(Newell(_faces[face].Corners));

    /// <summary>The middle of a face, the mean of its corners.</summary>
    public Vector3 Center(int face)
    {
        var corners = _faces[face].Corners;
        var sum = Vector3.Zero;
        foreach (var corner in corners) sum += _positions[corner];
        return sum / corners.Length;
    }

    /// <summary>
    /// How many edges are not shared by two faces running along them in opposite directions, which
    /// is none for a closed mesh whose faces all face out or all face in.
    /// </summary>
    /// <remarks>
    /// An edge of a hole is used once, an edge where three faces meet three times, and an edge
    /// between a face turned the wrong way and its neighbor twice in one direction. Each of those
    /// leaves the inside open to the outside, so a collider of it or its volume means nothing.
    /// </remarks>
    public int OpenEdges => EdgeUses().Values.Count(use => use.Forward != 1 || use.Backward != 1);

    /// <summary>The space a closed mesh holds, in cubic meters, negative where its faces face in.</summary>
    /// <remarks>
    /// The sum over the faces of the solid each makes with the origin, which is the volume of a
    /// closed mesh wherever the origin is. A face is taken as a fan from its first corner, which
    /// gives a flat face its area whether it is convex or not, since the parts of the fan outside a
    /// concave face count against those inside. Summed in doubles, so a level of many faces far from
    /// the origin loses nothing in the sum.
    /// </remarks>
    public float Volume
    {
        get
        {
            double sum = 0;
            foreach (var face in _faces)
            {
                var corners = face.Corners;
                var a = _positions[corners[0]];
                for (int i = 1; i < corners.Length - 1; i++)
                {
                    var b = _positions[corners[i]];
                    var c = _positions[corners[i + 1]];
                    // a · (b × c), six times the signed solid of the origin and the triangle.
                    sum += a.X * ((double)b.Y * c.Z - (double)b.Z * c.Y)
                         + a.Y * ((double)b.Z * c.X - (double)b.X * c.Z)
                         + a.Z * ((double)b.X * c.Y - (double)b.Y * c.X);
                }
            }
            return (float)(sum / 6.0);
        }
    }

    /// <summary>The smallest box holding every vertex, or two zeros for a mesh with none.</summary>
    public BoundingBox Bounds => BoundsOf(Enumerable.Range(0, _positions.Count));

    /// <summary>Takes away every vertex, face and slot but the first, and the selection with them.</summary>
    public void Clear()
    {
        _positions.Clear();
        _faces.Clear();
        _slots.RemoveRange(1, _slots.Count - 1);
        _looks.RemoveRange(1, _looks.Count - 1);
        _selectedVertices.Clear();
        _selectedFaces.Clear();
        Changed();
        Did("clear");
    }

    /// <summary>Says the shape changed, so what is read from the faces is read again and the model built again.</summary>
    private void Changed()
    {
        _edges = null;
        Version++;
    }

    private Dictionary<(int Low, int High), EdgeUse> EdgeUses()
    {
        if (_edges is { } known) return known;

        var edges = new Dictionary<(int Low, int High), EdgeUse>();
        foreach (var face in _faces)
        {
            var corners = face.Corners;
            for (int i = 0; i < corners.Length; i++)
            {
                var from = corners[i];
                var to = corners[(i + 1) % corners.Length];
                var key = from < to ? (from, to) : (to, from);
                edges.TryGetValue(key, out var use);
                if (from < to) use.Forward++;
                else use.Backward++;
                edges[key] = use;
            }
        }
        return _edges = edges;
    }

    private BoundingBox BoundsOf(IEnumerable<int> vertices)
    {
        var any = false;
        Vector3 low = default, high = default;
        foreach (var vertex in vertices)
        {
            var point = _positions[vertex];
            (low, high) = any ? (Vector3.Min(low, point), Vector3.Max(high, point)) : (point, point);
            any = true;
        }
        return new BoundingBox(low, high);
    }

    private Vector3 Newell(int[] corners)
    {
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < corners.Length; i++)
        {
            var a = _positions[corners[i]];
            var b = _positions[corners[(i + 1) % corners.Length]];
            x += ((double)a.Y - b.Y) * ((double)a.Z + b.Z);
            y += ((double)a.Z - b.Z) * ((double)a.X + b.X);
            z += ((double)a.X - b.X) * ((double)a.Y + b.Y);
        }
        return new Vector3((float)x, (float)y, (float)z);
    }

    /// <summary>A direction of length one, or zero for no direction, where <see cref="Vector3.Normalize"/> gives NaN.</summary>
    private static Vector3 Unit(Vector3 vector) => vector == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(vector);

    private int AddVertex(Vector3 position)
    {
        _positions.Add(position);
        return _positions.Count - 1;
    }

    private int AddFace(int[] corners, int slot = 0, float smooth = 0f, Vector2[]? uvs = null)
    {
        _faces.Add(new Face(corners, slot, smooth, uvs));
        return _faces.Count - 1;
    }
}

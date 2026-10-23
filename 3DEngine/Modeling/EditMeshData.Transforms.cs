using System.Numerics;

namespace Engine;

// What moves the selection, the grid it lands on, and the slot and the smoothing of its faces.
internal sealed partial class EditMeshData
{
    /// <summary>The angle a face is smoothed across when none is named, thirty degrees, as Blender's is.</summary>
    public const float DefaultSmoothAngle = 30f;

    public void Move(Vector3 by)
    {
        if (!float.IsFinite(by.X) || !float.IsFinite(by.Y) || !float.IsFinite(by.Z))
            throw new ArgumentOutOfRangeException(nameof(by), by, "A move is a finite distance along each axis.");

        foreach (var vertex in SelectedVertices()) _positions[vertex] += by;
        Changed();
        Did($"move {V(by)}");
    }

    /// <summary>Turns the selected vertices about an axis, counterclockwise looking back along it.</summary>
    /// <remarks>
    /// About the middle of the box holding the selection where no pivot is given, rather than the
    /// mean of its vertices, which a side with more vertices than the other would pull toward it, so
    /// a quarter turn of a part that sits on the grid leaves it on the grid.
    /// </remarks>
    public void Rotate(Vector3 axis, float degrees, Vector3? pivot)
    {
        if (axis == Vector3.Zero) throw new ArgumentException("An axis to turn about is not zero.", nameof(axis));
        if (!float.IsFinite(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "A turn is a finite number of degrees.");

        var vertices = SelectedVertices();
        var center = pivot ?? Middle(vertices);
        var turn = Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), float.DegreesToRadians(degrees));
        foreach (var vertex in vertices) _positions[vertex] = center + Vector3.Transform(_positions[vertex] - center, turn);

        Changed();
        Did($"rotate {AxisWords(axis)} {F(degrees)}{Pivot(pivot)}");
    }

    /// <summary>Scales the selected vertices toward or away from a point, the middle of the box holding them where no pivot is given.</summary>
    /// <remarks>
    /// A negative factor mirrors the selection, which would leave a face turned inside out, so a face
    /// whose every corner was scaled is wound the other way when the factors' product is negative,
    /// and faces out as it did. A factor of zero flattens the selection onto a plane, which faces
    /// with no area are left of, as a modeling program leaves them.
    /// </remarks>
    public void Scale(Vector3 factor, Vector3? pivot)
    {
        if (!float.IsFinite(factor.X) || !float.IsFinite(factor.Y) || !float.IsFinite(factor.Z))
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "A scale is a finite factor along each axis.");

        var vertices = SelectedVertices();
        var center = pivot ?? Middle(vertices);
        foreach (var vertex in vertices) _positions[vertex] = center + (_positions[vertex] - center) * factor;

        if (factor.X * factor.Y * factor.Z < 0)
        {
            var moved = vertices.ToHashSet();
            foreach (var face in _faces)
                if (face.Corners.All(moved.Contains)) Array.Reverse(face.Corners);
        }

        Changed();
        var factors = factor.X == factor.Y && factor.Y == factor.Z ? F(factor.X) : V(factor);
        Did($"scale {factors}{Pivot(pivot)}");
    }

    /// <summary>Sets the grid step <see cref="Snap"/> rounds to, a meter or a power of two of it from a sixteenth to sixty-four.</summary>
    /// <remarks>
    /// A power of two, so every finer step divides a coarser one and a point on a coarse grid is on
    /// every finer one, and exact in a float, so a point on the grid stays on it however many steps
    /// it is moved.
    /// </remarks>
    public void Grid(float step)
    {
        if (!(step >= FinestGrid && step <= CoarsestGrid) || !float.IsPow2(step))
            throw new ArgumentOutOfRangeException(nameof(step), step, "A grid step is a meter or a power of two of it, from a sixteenth to sixty-four.");

        GridStep = step;
        Did($"grid {F(step)}");
    }

    /// <summary>Puts each selected vertex on the nearest point of the grid, a coordinate halfway between two steps going to the one further from zero.</summary>
    public void Snap()
    {
        var step = GridStep;
        foreach (var vertex in SelectedVertices())
        {
            var point = _positions[vertex];
            _positions[vertex] = new Vector3(OnGrid(point.X, step), OnGrid(point.Y, step), OnGrid(point.Z, step));
        }
        Changed();
        Did("snap");
    }

    /// <summary>Puts the selected faces in a material slot, which is made where the mesh has none of that name.</summary>
    public void Material(string slot)
    {
        OneWord(slot, nameof(slot));
        var index = SlotIndex(slot);
        foreach (var face in _selectedFaces) _faces[face].Slot = index;
        Changed();
        Did($"material {slot}");
    }

    /// <summary>
    /// Draws the selected faces smoothed into one another up to an angle between two faces, so a
    /// cylinder's sides are round and its ends stay flat, or flat.
    /// </summary>
    public void Shade(bool smooth, float degrees)
    {
        if (smooth && !(degrees > 0 && degrees <= 180))
            throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "A smoothing angle is above zero and up to 180 degrees.");

        foreach (var face in _selectedFaces) _faces[face].Smooth = smooth ? degrees : 0;
        Changed();
        Did(smooth ? $"shade smooth {F(degrees)}" : "shade flat");
    }

    /// <summary>Refuses a name a recipe's line could not hold as one word.</summary>
    private static void OneWord(string name, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, parameter);
        if (name.Any(char.IsWhiteSpace)) throw new ArgumentException("A slot's name is one word.", parameter);
    }

    /// <summary>The middle of the box holding some vertices.</summary>
    private Vector3 Middle(int[] vertices)
    {
        var bounds = BoundsOf(vertices);
        return (bounds.Min + bounds.Max) * 0.5f;
    }

    private static float OnGrid(float value, float step) => MathF.Round(value / step, MidpointRounding.AwayFromZero) * step;
}

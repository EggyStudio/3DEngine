using System.Numerics;

namespace Engine;

// A cascade the eye moved by whole cells along one axis shifted as a clipmap is, the cells it keeps
// moved on the GPU and the slab of cells it brings in built alone from the still meshes within the
// band of them, where the whole cascade was built again from every still mesh within its band.
internal sealed partial class SceneFieldPlan
{
    /// <summary>The cells of a cascade from the first to the last along each axis, both within.</summary>
    internal readonly record struct CellBox(int X0, int Y0, int Z0, int X1, int Y1, int Z1)
    {
        /// <summary>Whether every cell of <paramref name="other"/> is one of these.</summary>
        public bool Holds(CellBox other) => X0 <= other.X0 && Y0 <= other.Y0 && Z0 <= other.Z0 && X1 >= other.X1 && Y1 >= other.Y1 && Z1 >= other.Z1;

        /// <summary>How many cells.</summary>
        public int Count => (X1 - X0 + 1) * (Y1 - Y0 + 1) * (Z1 - Z0 + 1);
    }

    // Whether Rebuild marked every cascade to be built whole this frame.
    private bool _forced;

    // The shift of cascade `cascade` toward `target`, its corner moved along the first axis that
    // differs and the slab it brings in built alone, or null where it is to be built whole: never
    // built, a still mesh came or went within it, a rebuild was asked, or the corner moved half the
    // cascade or more, past which the kept cells are fewer than the slab's.
    private Job? ShiftOf(int cascade, Vector3 target)
    {
        if (_forced || _built[cascade] is not { } built || _changed[cascade] is not null) return null;
        var cell = CellOf(cascade);
        var moved = (target - built) / cell;
        int[] cells = [(int)MathF.Round(moved.X), (int)MathF.Round(moved.Y), (int)MathF.Round(moved.Z)];
        if (cells.Any(along => Math.Abs(along) >= Resolution / 2)) return null;
        var axis = Array.FindIndex(cells, along => along != 0);
        if (axis < 0) return null;
        var by = cells[axis];
        var origin = built;
        origin[axis] = target[axis];
        // The slab at the side the cascade moved toward, its cells new to it.
        var (from, to) = by > 0 ? (Resolution - by, Resolution - 1) : (0, -by - 1);
        var top = Resolution - 1;
        var slab = axis switch
        {
            0 => new CellBox(from, 0, 0, to, top, top),
            1 => new CellBox(0, from, 0, top, to, top),
            _ => new CellBox(0, 0, from, top, top, to),
        };
        return new Job { Cascade = cascade, Origin = origin, Reach = ReachOf(cascade, origin, slab), Cells = slab, ShiftAxis = axis, ShiftCells = by };
    }

    // Where the still meshes a build of cells splats lie, within the band of its cells and of the
    // cells around them, which a cell's distance is read beside (pinched in field_resolve.slang).
    private Box ReachOf(int cascade, Vector3 origin, CellBox cells)
    {
        var cell = CellOf(cascade);
        var low = origin + new Vector3(cells.X0 - 1, cells.Y0 - 1, cells.Z0 - 1) * cell;
        var high = origin + new Vector3(cells.X1 + 2, cells.Y1 + 2, cells.Z1 + 2) * cell;
        return new Box(low, high).Grown(Band * cell);
    }

    // What a mesh a build of cells splats costs of a frame's triangle budget, its triangles times the
    // share of its bounds within the build's reach, and an eighth of them at least, since a triangle
    // whose band misses the cells is passed over at the start, at a thirteenth of the cost of one
    // splatted, 2.2 ns against 28 in the voxel game, and a wall of Sponza crossing a slab whole was
    // counted whole for the few of its triangles within it.
    private int Cost(Instance instance, Job job)
    {
        var triangles = _triangles(instance.Vertices);
        if (job.Cells is null) return triangles;
        var bounds = _still.TryGetValue(instance, out var still) ? still.Bounds : _bounds(instance.Vertices).Transformed(instance.World);
        var cell = new Vector3(CellOf(job.Cascade));
        var size = Vector3.Max(bounds.Max - bounds.Min, cell);
        var within = Vector3.Clamp(Vector3.Min(bounds.Max, job.Reach.Max) - Vector3.Max(bounds.Min, job.Reach.Min), cell, size);
        var share = within.X / size.X * (within.Y / size.Y) * (within.Z / size.Z);
        return Math.Max(1, (int)MathF.Ceiling(triangles * MathF.Max(share, 0.125f)));
    }
}

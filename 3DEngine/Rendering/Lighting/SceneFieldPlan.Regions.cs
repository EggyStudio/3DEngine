using System.Numerics;

namespace Engine;

// The regions of the built cascades that still meshes came or went within, each built again alone,
// its cells within the band of what changed splatted from the still meshes near them, where the
// whole cascade was built again from every still mesh within its band, and how many frames and
// triangles the field took to hold a change.
internal sealed partial class SceneFieldPlan
{
    // The box around the still meshes that came or went within each built cascade since its last
    // build or the last build of its region.
    private readonly Box?[] _regions;

    // Marks the region a box comes within the band of in every built cascade, but for one whose
    // build in progress has taken the change in.
    private void MarkAround(Box bounds, int followed = -1)
    {
        for (int c = 0; c < Cascades; c++)
            if (c != followed && _built[c] is { } origin && bounds.Overlaps(CascadeBox(c, origin).Grown(Band * CellOf(c))))
                _regions[c] = With(_regions[c], bounds);
    }

    // The finest cascade to be built whole or to have a region built, or -1 for none.
    private int NextCascade()
    {
        for (int c = 0; c < Cascades; c++)
            if (_dirty[c] || _regions[c] is not null)
                return c;
        return -1;
    }

    // The build of a cascade, a shift where the eye moved it by whole cells and less than half its
    // width, the whole of it where the eye moved it farther, it was never built or a rebuild was
    // asked, the region that changed within it taken in, and its region alone otherwise.
    private Job Start(int cascade, Vector3 eye)
    {
        // A cascade the eye moved by whole cells shifts, what changed within it built after as its
        // region, which the shift leaves where it lies in the world.
        if (_dirty[cascade] && ShiftOf(cascade, OriginAround(cascade, eye)) is { } shift)
        {
            _dirty[cascade] = false;
            Gather(shift, eye);
            return shift;
        }
        Job job;
        if (_dirty[cascade] || _built[cascade] is not { } origin || _regions[cascade] is not { } region)
        {
            var at = OriginAround(cascade, eye);
            var reach = CascadeBox(cascade, at).Grown(Band * CellOf(cascade));
            job = new Job { Cascade = cascade, Origin = at, Reach = reach, Changed = _built[cascade] is null ? reach : With(_changed[cascade], _regions[cascade]) };
        }
        else
        {
            var cells = CellsAround(cascade, origin, region);
            job = new Job { Cascade = cascade, Origin = origin, Cells = cells, Reach = ReachOf(cascade, origin, cells), Changed = region };
        }
        (_changed[cascade], _regions[cascade]) = (null, null);
        Gather(job, eye);
        return job;
    }

    // A build put off for a finer cascade's, or for its cascade's whole build, what changed within
    // it kept for the build that takes its place.
    private void Shelve(Job job)
    {
        var c = job.Cascade;
        if (job.Cells is not null)
            _regions[c] = With(_regions[c], job.Changed);
        else
        {
            _dirty[c] = true;
            _changed[c] = With(_changed[c], job.Changed);
        }
    }

    private static Box? With(Box? a, Box? b) => a is { } one && b is { } other ? one.With(other) : a ?? b;

    // The cells of a cascade within the band of a box, as a triangle within the box splats them.
    private CellBox CellsAround(int cascade, Vector3 origin, Box box)
    {
        var cell = CellOf(cascade);
        var (low, high) = ((box.Min - origin) / cell - new Vector3(Band), (box.Max - origin) / cell + new Vector3(Band));
        static int Within(float at) => (int)Math.Clamp(at, 0, Resolution - 1);
        return new CellBox(Within(MathF.Floor(low.X)), Within(MathF.Floor(low.Y)), Within(MathF.Floor(low.Z)),
            Within(MathF.Ceiling(high.X)), Within(MathF.Ceiling(high.Y)), Within(MathF.Ceiling(high.Z)));
    }

    /// <summary>
    /// How much of the triangle budget this frame's builds spent, each mesh its triangles by the
    /// share of its bounds within the cells built, an eighth of them at least.
    /// </summary>
    public int Spent { get; private set; }

    /// <summary>The frames the plan has been updated for.</summary>
    public long Frame => _frame;

    /// <summary>
    /// The frame the field last began to change, the frame after which it held the change whole, or
    /// -1 while it has not, and the triangles its builds splatted between.
    /// </summary>
    /// <remarks>
    /// The field changes from the frame a still mesh is kept for one replacing it, a mesh settles
    /// or leaves, or a cascade is to be built, and holds the change once no build waits or runs,
    /// no region is to be built and no still mesh waits for its cascades or is kept.
    /// </remarks>
    public (long From, long To, int Triangles) LastChange { get; private set; } = (0, -1, 0);

    /// <summary>
    /// What keeps the field from holding a change whole, the cascades to be built whole or to have a
    /// region built, the still meshes that wait for their cascades and those kept for meshes
    /// replacing them.
    /// </summary>
    public (int[] Cascades, int Waiting, int Kept) Unsettled =>
        ([.. Enumerable.Range(0, Cascades).Where(c => _dirty[c] || _regions[c] is not null || _job?.Cascade == c)], _pending.Count, _kept.Count);

    private bool _quiet;
    private long _changedAt;
    private int _splattedSinceChange;

    // Counts the frames and the triangles the change in progress has taken.
    private void Track()
    {
        var quiet = _job is null && Array.IndexOf(_dirty, true) < 0 && Array.TrueForAll(_regions, region => region is null)
            && _pending.Count == 0 && _kept.Count == 0;
        if (!quiet && _quiet) (_changedAt, _splattedSinceChange) = (_frame, 0);
        if (!quiet || !_quiet) _splattedSinceChange += Splatted;
        if (!quiet || !_quiet) LastChange = (_changedAt, quiet ? _frame : -1, _splattedSinceChange);
        _quiet = quiet;
    }
}

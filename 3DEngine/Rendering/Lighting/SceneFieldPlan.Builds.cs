using System.Numerics;

namespace Engine;

// The cascades' builds under the budgets, a build of more triangles than a frame's budget splatted
// over as many frames as it takes, nearest the eye first, and the still meshes that come and go
// while it runs taken in. A build is of a whole cascade, or of the cells around what came or went
// within a built one (SceneFieldPlan.Regions.cs).
internal sealed partial class SceneFieldPlan
{
    // A cascade's build that runs over frames, its corner and reach, the still meshes left to splat
    // into it, nearest the eye first by the distance each was put in at, the box around what came or
    // went within it, and whether it splatted anything yet or has to start again, a mesh it splatted
    // having left. A build of a region or a shift's holds the cells it builds alone, and the frame
    // its meshes were gathered in, which every mesh that settled by then is among. A shift's holds
    // the axis and the cells its cascade moves by, which its first part moves it by and no later
    // part again.
    private sealed class Job
    {
        public int Cascade;
        public Vector3 Origin;
        public Box Reach;
        public CellBox? Cells;
        public bool Shifting;
        public int ShiftAxis = -1, ShiftCells;
        public readonly List<(float Distance, Instance Instance)> Left = [];
        // The triangles of every mesh in Left, which the pool is sized for.
        public int Triangles;
        // Where the meshes not yet splatted begin in Left.
        public int Next;
        public Box? Changed;
        public bool Started, Restart;
        public long Gathered;
    }

    private Job? _job;

    /// <summary>The triangles of the meshes the build in progress splats over its frames, or none.</summary>
    public int BuildingTriangles => _job?.Triangles ?? 0;

    /// <summary>
    /// The cascade a shift moved and the cells it brought in, which hold nothing until the shift's
    /// build ends in a later frame, or null where no shift waits.
    /// </summary>
    /// <remarks>A shift's first part moves its cascade, its axis then set to none, and a build begun again keeps it moved.</remarks>
    public (int Cascade, CellBox Cells)? Unbuilt => _job is { Shifting: true, ShiftAxis: < 0, Cells: { } cells } job ? (job.Cascade, cells) : null;

    /// <summary>How many triangles this frame's builds splat.</summary>
    public int Splatted { get; private set; }

    /// <summary>
    /// The most triangles a frame's builds splatted since <see cref="TakeMostSplatted"/> was last
    /// asked, which a frame's count alone loses by the time a command reads it.
    /// </summary>
    private int _mostSplatted;

    /// <summary>The most triangles a frame's builds splatted since this was last asked, started afresh.</summary>
    public int TakeMostSplatted()
    {
        var most = _mostSplatted;
        _mostSplatted = 0;
        return most;
    }

    /// <summary>How many triangles of a build left for later frames wait to be splatted.</summary>
    public int Waiting => _job is null ? 0 : _job.Left.Skip(_job.Next).Sum(entry => _triangles(entry.Instance.Vertices));

    /// <summary>
    /// How many meshes drawn this frame came and have not moved and are not yet still, which the
    /// field holds nothing of until they settle and are built in, as a scene's on its first load.
    /// </summary>
    public int Arriving { get; private set; }

    /// <summary>
    /// Whether the field holds every mesh in reach, every cascade built and every mesh that came
    /// built in, so a ray that leaves it meeting nothing met no mesh there.
    /// </summary>
    /// <remarks>
    /// A build in progress, as the eye's move starts one, leaves the passes reading its cascade as
    /// it was last built, which holds the meshes it held. A shift of the last cascade holds the slab
    /// it brings in empty until its build ends, and the field is not whole until then, the slab
    /// lying at the field's edge. Taken as a field not whole, as it was
    /// while every cascade was built and none waited, the probes' carried rays brought no sky
    /// through Sponza's court for the 40 frames its cascades took to build again after the eye
    /// moved, and the gallery's view read its blue less red at -0.65 in a thousandth of linear
    /// light against +1.00 at rest and a path-traced reference's +0.80.
    /// </remarks>
    public bool Whole => Arriving == 0 && _pending.Count == 0 && Array.TrueForAll(_built, origin => origin is not null)
        && !(_job is { Cells: not null } shifting && shifting.Cascade == Cascades - 1);

    /// <summary>
    /// Whether the field holds the scene as it stands, <see cref="Whole"/> with no build in
    /// progress and none waiting, every cascade built where the eye has it, so only what moves is
    /// stamped as boxes, which a picture of the scene waits for (<c>field.wait</c>).
    /// </summary>
    public bool Built => Whole && _job is null && Array.IndexOf(_dirty, true) < 0 && Array.TrueForAll(_regions, region => region is null);

    /// <summary>How many frames running the field has been <see cref="Built"/>, 0 while it is not.</summary>
    public int FramesBuilt { get; private set; }

    // Starts or goes on with the builds of the dirty cascades and the changed regions of the built
    // ones, the finest cascade first, as many whole cascades a frame as the budget says and as many
    // triangles as the triangle budget, a mesh at least, the still meshes within a build's band
    // splatted nearest the eye first, and says which cascades were built whole this frame. A build
    // the triangles leave unfinished goes on the frame after, its cascade keeping the place and the
    // meshes it was last built with until the build ends, and the still meshes it has not splatted
    // are stamped as boxes meanwhile, as any mesh that settled into a cascade not built since is.
    private bool[] Splat(Vector3 eye)
    {
        (Splatted, Spent) = (0, 0);
        var rebuilt = new bool[Cascades];
        var (cascades, triangles) = (Budget, TriangleBudget);
        while (cascades > 0 && triangles > 0)
        {
            // A finer cascade gone dirty or changed comes first, as the cascades always come, and the
            // coarser build in progress starts again after it, what changed within it kept for that
            // build, as a region's build does where the eye has moved its cascade, but for a shift's,
            // whose cascade has moved and holds the slab it brings in empty.
            if (_job is { Shifting: false } running && (NextCascade() is >= 0 and var finer && finer < running.Cascade || running.Cells is not null && _dirty[running.Cascade]))
            {
                Shelve(running);
                _job = null;
            }
            if (_job is null)
            {
                var next = NextCascade();
                if (next < 0) break;
                _job = Start(next, eye);
            }
            var job = _job;
            var c = job.Cascade;
            // A build whose cascade the eye has carried on, or one of whose splatted meshes left,
            // starts again where the cascade lies this frame. A shift's goes on to its own place, the
            // next shift following it, and starts again where a mesh it splatted left.
            if (job.Cells is not null)
            {
                if (job.Restart) Gather(job, eye);
            }
            else if (job.Restart || job.Origin != OriginAround(c, eye))
            {
                job.Origin = OriginAround(c, eye);
                job.Reach = CascadeBox(c, job.Origin).Grown(Band * CellOf(c));
                Gather(job, eye);
            }
            if (job.Cells is null) _dirty[c] = false;

            var batch = new List<Instance>();
            while (job.Next < job.Left.Count)
            {
                var instance = job.Left[job.Next].Instance;
                var cost = Cost(instance, job);
                if (cost > triangles && (batch.Count > 0 || Splatted > 0)) break;
                batch.Add(instance);
                job.Next++;
                triangles -= cost;
                Spent += cost;
                Splatted += _triangles(instance.Vertices);
            }
            var last = job.Next == job.Left.Count;
            if (batch.Count == 0 && !last) break;
            Builds.Add(new Build(c, job.Origin, CellOf(c), [.. batch], last ? job.Changed : null, !job.Started, last, job.Cells,
                job.ShiftAxis, job.ShiftCells));
            // A shift moves its cascade with its first part, the passes reading it at its new corner
            // from this frame on and the field they read made the still field again.
            if (job.ShiftAxis >= 0) (_built[c], rebuilt[c], job.ShiftAxis) = (job.Origin, true, -1);
            job.Started = true;
            if (job.Cells is null) cascades--;
            if (!last) break;
            if (job.Cells is null) (_built[c], _builtFrame[c], rebuilt[c]) = (job.Origin, _frame, true);
            else if (!job.Shifting) _builtFrame[c] = Math.Max(_builtFrame[c], job.Gathered);
            _job = null;
        }
        return rebuilt;
    }

    // The still meshes within a build's reach, nearest the eye first, none of them splatted yet.
    private void Gather(Job job, Vector3 eye)
    {
        job.Left.Clear();
        foreach (var (instance, (bounds, _)) in _still)
            if (bounds.Overlaps(job.Reach)) job.Left.Add((Distance(bounds, eye), instance));
        job.Left.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
        job.Triangles = 0;
        foreach (var (_, instance) in job.Left) job.Triangles += _triangles(instance.Vertices);
        (job.Next, job.Started, job.Restart, job.Gathered) = (0, false, false, _frame);
    }

    // How far a mesh's bounds' middle is from the eye, squared, which orders the meshes a build splats.
    private static float Distance(Box bounds, Vector3 eye) => Vector3.DistanceSquared((bounds.Min + bounds.Max) / 2, eye);

    // A still mesh that came or went within the reach of the build in progress, which takes it in.
    // One that came is splatted with the rest, in its place among those left by its distance, one
    // that went before it was splatted is dropped, and one that went after starts the build again,
    // as one does whose cells, a region's, do not hold the cells the mesh's band comes within, its
    // cells grown to hold them. Says the cascade of the build that took it in, or -1 where none did.
    private int Follow(Instance instance, Box bounds, bool came)
    {
        if (_job is not { } job || !bounds.Overlaps(job.Reach)) return -1;
        job.Changed = job.Changed is { } box ? box.With(bounds) : bounds;
        if (job.Cells is { } cells && CellsAround(job.Cascade, job.Origin, bounds) is var around && !cells.Holds(around))
        {
            job.Cells = cells.With(around);
            job.Reach = ReachOf(job.Cascade, job.Origin, job.Cells.Value);
            job.Restart = true;
            return job.Cascade;
        }
        var at = job.Left.FindIndex(entry => Keys.Shared.Equals(entry.Instance, instance));
        if (came)
        {
            if (at >= 0) return job.Cascade;
            var distance = Distance(bounds, Eye);
            var place = job.Next;
            while (place < job.Left.Count && job.Left[place].Distance <= distance) place++;
            job.Left.Insert(place, (distance, instance));
        }
        else if (at >= job.Next) job.Left.RemoveAt(at);
        else if (at >= 0) job.Restart = true;
        return job.Cascade;
    }
}

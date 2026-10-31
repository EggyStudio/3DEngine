using System.Numerics;

namespace Engine;

// The cascades' builds under the budgets, a build of more triangles than a frame's budget splatted
// over as many frames as it takes, nearest the eye first, and the still meshes that come and go
// while it runs taken in.
internal sealed partial class SceneFieldPlan
{
    // A cascade's build that runs over frames, its corner and reach, the still meshes left to splat
    // into it, nearest the eye first by the distance each was put in at, the box around what came or
    // went within it, and whether it splatted anything yet or has to start again, a mesh it splatted
    // having left.
    private sealed class Job
    {
        public int Cascade;
        public Vector3 Origin;
        public Box Reach;
        public readonly List<(float Distance, Instance Instance)> Left = [];
        // The triangles of every mesh in Left, which the pool is sized for.
        public int Triangles;
        // Where the meshes not yet splatted begin in Left.
        public int Next;
        public Box? Changed;
        public bool Started, Restart;
    }

    private Job? _job;

    /// <summary>The triangles of the meshes the build in progress splats over its frames, or none.</summary>
    public int BuildingTriangles => _job?.Triangles ?? 0;

    // Starts or goes on with the dirty cascades' builds, the finest first, as many cascades a frame
    // as the budget says and as many triangles as the triangle budget, a mesh at least, the still
    // meshes within a cascade's band splatted nearest the eye first, and says which cascades were
    // built whole this frame. A build the triangles leave unfinished goes on the frame after, its
    // cascade keeping the place and the meshes it was last built with until the build ends, and the
    // still meshes it has not splatted are stamped as boxes meanwhile, as any mesh that settled into
    // a cascade not built since is.
    private bool[] Splat(Vector3 eye, Vector3 ahead)
    {
        Splatted = 0;
        var rebuilt = new bool[Cascades];
        var (cascades, triangles) = (Budget, TriangleBudget);
        while (cascades > 0 && triangles > 0)
        {
            // A finer cascade gone dirty comes first, as the cascades always come, and the coarser
            // build in progress starts again after it, what changed within it kept for that build.
            if (_job is { } running && Array.IndexOf(_dirty, true) is >= 0 and var finer && finer < running.Cascade)
            {
                var rc = running.Cascade;
                _dirty[rc] = true;
                _changed[rc] = _changed[rc] is { } kept && running.Changed is { } more ? kept.With(more) : _changed[rc] ?? running.Changed;
                _job = null;
            }
            if (_job is null)
            {
                var next = Array.IndexOf(_dirty, true);
                if (next < 0) break;
                var origin = OriginAround(next, eye, ahead);
                var reach = CascadeBox(next, origin).Grown(Band * CellOf(next));
                _job = new Job { Cascade = next, Origin = origin, Reach = reach, Changed = _built[next] is null ? reach : _changed[next] };
                _changed[next] = null;
                Gather(_job, eye);
            }
            var job = _job;
            var c = job.Cascade;
            // A build whose cascade the eye has carried on, or one of whose splatted meshes left,
            // starts again where the cascade lies this frame.
            if (job.Restart || job.Origin != OriginAround(c, eye, ahead))
            {
                job.Origin = OriginAround(c, eye, ahead);
                job.Reach = CascadeBox(c, job.Origin).Grown(Band * CellOf(c));
                Gather(job, eye);
            }
            _dirty[c] = false;

            var batch = new List<Instance>();
            while (job.Next < job.Left.Count)
            {
                var cost = _triangles(job.Left[job.Next].Instance.Vertices);
                if (cost > triangles && (batch.Count > 0 || Splatted > 0)) break;
                batch.Add(job.Left[job.Next++].Instance);
                triangles -= cost;
                Splatted += cost;
            }
            var last = job.Next == job.Left.Count;
            if (batch.Count == 0 && !last) break;
            Builds.Add(new Build(c, job.Origin, CellOf(c), [.. batch], last ? job.Changed : null, !job.Started, last));
            job.Started = true;
            cascades--;
            if (!last) break;
            (_built[c], _builtFrame[c], rebuilt[c]) = (job.Origin, _frame, true);
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
        (job.Next, job.Started, job.Restart) = (0, false, false);
    }

    // How far a mesh's bounds' middle is from the eye, squared, which orders the meshes a build splats.
    private static float Distance(Box bounds, Vector3 eye) => Vector3.DistanceSquared((bounds.Min + bounds.Max) / 2, eye);

    // A still mesh that came or went within the reach of the build in progress, which takes it in.
    // One that came is splatted with the rest, in its place among those left by its distance, one
    // that went before it was splatted is dropped, and one that went after starts the build again.
    // Says the cascade of the build that took it in, or -1 where none did.
    private int Follow(Instance instance, Box bounds, bool came)
    {
        if (_job is not { } job || !bounds.Overlaps(job.Reach)) return -1;
        job.Changed = job.Changed is { } box ? box.With(bounds) : bounds;
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

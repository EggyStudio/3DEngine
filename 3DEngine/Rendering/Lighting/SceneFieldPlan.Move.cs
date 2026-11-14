using System.Numerics;

namespace Engine;

// The plan moved with a program's world, every place it keeps moved by the whole step every place
// handed to the engine moved by (E3D.MoveWorld).
internal sealed partial class SceneFieldPlan
{
    /// <summary>
    /// Moves every place the plan keeps by a whole step, in doubles, as a program moved every place
    /// it hands the engine by it: each mesh it knows by its matrix is known by that matrix moved, so
    /// the mesh drawn at it is the same still mesh, and the cascades' corners and the boxes of what
    /// was built, came and went move with them, so the cells built keep what they hold.
    /// </summary>
    /// <remarks>
    /// The cells hold only where the step is a whole count of a cascade's cell, as a step of 1,024
    /// is of every cell of a power of two; a cascade whose cell it is not is built again.
    /// </remarks>
    /// <returns>How many cascades are built again.</returns>
    public int Move(double x, double y, double z)
    {
        if (x == 0 && y == 0 && z == 0) return 0;
        Vector3 Moved(Vector3 p) => new((float)(p.X + x), (float)(p.Y + y), (float)(p.Z + z));
        Box MovedBox(Box box) => new(Moved(box.Min), Moved(box.Max));
        Matrix4x4 MovedMatrix(Matrix4x4 m)
        {
            (m.M41, m.M42, m.M43) = ((float)(m.M41 + x), (float)(m.M42 + y), (float)(m.M43 + z));
            return m;
        }
        Instance MovedInstance(Instance instance) => instance with { World = MovedMatrix(instance.World) };
        void Rekey<T>(Dictionary<Instance, T> set, Func<T, T>? value = null)
        {
            var entries = set.ToArray();
            set.Clear();
            foreach (var (instance, kept) in entries) set[MovedInstance(instance)] = value is null ? kept : value(kept);
        }
        void RekeySet(HashSet<Instance> set)
        {
            var entries = set.ToArray();
            set.Clear();
            foreach (var instance in entries) set.Add(MovedInstance(instance));
        }

        Rekey(_seen);
        Rekey(_movedBefore);
        Rekey(_movedNow);
        Rekey(_still, kept => (MovedBox(kept.Bounds), kept.Added));
        Rekey(_kept);
        Rekey(_builtIn);
        RekeySet(_pending);
        RekeySet(_replacing);
        RekeySet(_movedIn);
        var departed = _departedWorlds.ToArray();
        _departedWorlds.Clear();
        foreach (var world in departed) _departedWorlds.Add(MovedMatrix(world));
        for (int i = 0; i < _orderCount && i < _order.Length; i++) _order[i] = MovedInstance(_order[i]);
        for (int i = 0; i < _nearest.Length; i++) _nearest[i] = MovedInstance(_nearest[i]);
        if (_job is { } job)
        {
            (job.Origin, job.Reach) = (Moved(job.Origin), MovedBox(job.Reach));
            if (job.Changed is { } changed) job.Changed = MovedBox(changed);
            for (int i = 0; i < job.Left.Count; i++) job.Left[i] = (job.Left[i].Distance, MovedInstance(job.Left[i].Instance));
            for (int i = 0; i < job.Took.Count; i++) job.Took[i] = MovedInstance(job.Took[i]);
        }

        var rebuilt = 0;
        for (int c = 0; c < Cascades; c++)
        {
            if (_changed[c] is { } changed) _changed[c] = MovedBox(changed);
            if (_regions[c] is { } region) _regions[c] = MovedBox(region);
            if (_built[c] is not { } origin) continue;
            var cell = (double)CellOf(c);
            static bool Whole(double by, double cell) => Math.Abs(by / cell - Math.Round(by / cell)) < 1e-6;
            if (Whole(x, cell) && Whole(y, cell) && Whole(z, cell))
                _built[c] = Moved(origin);
            else
            {
                (_built[c], _dirty[c], _changed[c], _regions[c]) = (null, true, null, null);
                if (_job?.Cascade == c) _job = null;
                rebuilt++;
            }
        }
        return rebuilt;
    }
}

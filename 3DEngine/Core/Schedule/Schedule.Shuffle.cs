namespace Engine;

public sealed partial class Schedule
{
    /// <summary>The variable naming a seed each stage's systems are shuffled by as an app starts.</summary>
    internal const string ShuffleSeedVariable = "E3D_SCHEDULE_SHUFFLE_SEED";

    /// <summary>
    /// Shuffles the order each stage runs its systems in by <paramref name="seed"/>, keeping the
    /// order a stage holds to, so a system that leans on an order nothing asks for meets another.
    /// </summary>
    /// <remarks>
    /// A stage holds to the order systems were added in between two that conflict, by what they
    /// declare they read and write or by one declaring nothing, and between a main-thread system
    /// and any other, as its batches do, and the shuffle keeps those pairs. Two systems whose
    /// declarations do not conflict may run in either order, which a batch run in parallel allows
    /// already, while a batch light enough to run on the calling thread runs them in the order
    /// added in every frame, so a system reading what another changes without declaring it can
    /// pass every test in that order alone. The same seed and the same systems give the same order.
    /// </remarks>
    internal void Shuffle(int seed)
    {
        var random = new Random(seed);
        lock (_lock)
        {
            foreach (var stage in StageOrder.AllInOrder())
            {
                var systems = _systemsByStage[stage];
                var shuffled = Shuffled(systems, random);
                systems.Clear();
                systems.AddRange(shuffled);
            }
        }
    }

    // A random order of the systems in which every pair the stage holds to keeps the order added,
    // drawn a system at a time from those whose every such earlier system is placed.
    private static List<SystemDescriptor> Shuffled(List<SystemDescriptor> systems, Random random)
    {
        var count = systems.Count;
        var waiting = new int[count];
        var after = new List<int>[count];
        for (int i = 0; i < count; i++) after[i] = [];
        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (!HeldInOrder(systems[i], systems[j])) continue;
                after[i].Add(j);
                waiting[j]++;
            }
        }

        var ready = Enumerable.Range(0, count).Where(i => waiting[i] == 0).ToList();
        var order = new List<SystemDescriptor>(count);
        while (ready.Count > 0)
        {
            var pick = random.Next(ready.Count);
            var next = ready[pick];
            ready[pick] = ready[^1];
            ready.RemoveAt(ready.Count - 1);
            order.Add(systems[next]);
            foreach (var later in after[next])
                if (--waiting[later] == 0) ready.Add(later);
        }
        return order;
    }

    private static bool HeldInOrder(SystemDescriptor earlier, SystemDescriptor later) =>
        earlier.Affinity == ThreadAffinity.MainThread || later.Affinity == ThreadAffinity.MainThread || earlier.ConflictsWith(later);
}

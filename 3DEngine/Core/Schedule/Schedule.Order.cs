namespace Engine;

public sealed partial class Schedule
{
    /// <summary>
    /// Puts each stage's systems in the order they asked for with <see cref="SystemDescriptor.After"/>
    /// and <see cref="SystemDescriptor.Before"/>, and otherwise in the order they were added, once as
    /// the app starts, and gives each the systems it runs after, which the batches and the shuffle keep.
    /// </summary>
    /// <remarks>
    /// A stage none of whose systems asks for an order is left as it is, so it costs one look at each
    /// system. The sort takes, each time, the earliest added of the systems whose predecessors are all
    /// placed, so the order added holds wherever nothing asked otherwise.
    /// </remarks>
    /// <exception cref="InvalidOperationException">A system names one its stage does not hold, or the order asked goes round.</exception>
    internal void Order()
    {
        lock (_lock)
        {
            foreach (var stage in StageOrder.AllInOrder())
            {
                var systems = _systemsByStage[stage];
                if (!systems.Exists(system => system.AsksOrder)) continue;
                var sorted = Sorted(stage, systems);
                systems.Clear();
                systems.AddRange(sorted);
            }
        }
    }

    private static List<SystemDescriptor> Sorted(Stage stage, List<SystemDescriptor> systems)
    {
        var count = systems.Count;
        var index = new Dictionary<SystemDescriptor, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < count; i++) index[systems[i]] = i;
        var before = new HashSet<int>[count];
        for (int i = 0; i < count; i++) before[i] = [];

        IEnumerable<int> Named(SystemDescriptor asking, string name)
        {
            var found = false;
            for (int i = 0; i < count; i++)
            {
                if (systems[i].Name != name || ReferenceEquals(systems[i], asking)) continue;
                found = true;
                yield return i;
            }
            if (!found)
                throw new InvalidOperationException($"System '{asking.Name}' in stage {stage} asks to run next to '{name}', and no other system of the stage is called that.");
        }

        for (int i = 0; i < count; i++)
        {
            foreach (var name in systems[i].RunsAfter)
                foreach (var other in Named(systems[i], name)) before[i].Add(other);
            foreach (var name in systems[i].RunsBefore)
                foreach (var other in Named(systems[i], name)) before[other].Add(i);
        }

        var placed = new bool[count];
        var order = new List<SystemDescriptor>(count);
        while (order.Count < count)
        {
            var next = -1;
            for (int i = 0; i < count && next < 0; i++)
                if (!placed[i] && before[i].All(p => placed[p])) next = i;
            if (next < 0) throw new InvalidOperationException($"The order asked in stage {stage} goes round: {Round(systems, before, placed)}.");
            placed[next] = true;
            order.Add(systems[next]);
        }

        for (int i = 0; i < count; i++)
            systems[i].Predecessors = before[i].Count == 0 ? null : [.. before[i].Select(p => systems[p])];
        return order;
    }

    // A round among the systems not placed, each named after the one it runs after, as 'A' after
    // 'B' after 'A', found by following predecessors until one comes again.
    private static string Round(List<SystemDescriptor> systems, HashSet<int>[] before, bool[] placed)
    {
        var at = Array.FindIndex(placed, p => !p);
        var path = new List<int>();
        while (!path.Contains(at))
        {
            path.Add(at);
            at = before[at].First(p => !placed[p]);
        }
        var round = path.Skip(path.IndexOf(at)).Append(at);
        return string.Join(" after ", round.Select(i => $"'{systems[i].Name}'"));
    }
}

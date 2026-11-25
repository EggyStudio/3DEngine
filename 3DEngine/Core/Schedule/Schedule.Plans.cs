namespace Engine;

public sealed partial class Schedule
{
    // Raised by every change to the systems a stage holds, to their order or to whether a stage
    // runs in parallel, after which each stage's batches are built again when it next runs.
    private int _version;

    private readonly Dictionary<Stage, StagePlan> _plans = [];

    // Raises the version under the lock and lets go of every stage's plan, so that no plan keeps a
    // system the change removed. A stage that ran once, as Startup does, builds no plan again, so
    // its kept plan would otherwise hold a reloaded script's first systems while the app runs.
    private void Changed()
    {
        _version++;
        _plans.Clear();
    }

    // A stage's batches as they were last built, with the names and notes the diagnostics show,
    // kept while the stage's systems, the schedule's version and each system's revision stay as
    // they were, so a frame builds no list and formats no conflict to run the stage.
    private sealed class StagePlan(
        int version,
        int revisions,
        List<List<SystemDescriptor>> batches,
        IReadOnlyList<IReadOnlyList<string>> names,
        IReadOnlyList<IReadOnlyList<string>> notes)
    {
        internal int Version { get; } = version;
        internal int Revisions { get; } = revisions;
        internal List<List<SystemDescriptor>> Batches { get; } = batches;
        internal IReadOnlyList<IReadOnlyList<string>> Names { get; } = names;
        internal IReadOnlyList<IReadOnlyList<string>> Notes { get; } = notes;
    }

    // The stage's plan, built again only when the schedule's version or a system's revision moved
    // since it was built. A parallel stage's systems are batched by their access, and a stage run
    // in order holds each system as a batch of its own with the reason it runs in order.
    private StagePlan PlanOf(Stage stage, List<SystemDescriptor> systems, int version, bool parallel, bool markedParallel)
    {
        var revisions = 0;
        foreach (var system in systems) revisions += system.Revision;
        lock (_lock)
            if (_plans.TryGetValue(stage, out var kept) && kept.Version == version && kept.Revisions == revisions)
                return kept;

        List<List<SystemDescriptor>> batches;
        List<IReadOnlyList<string>> notes;
        if (parallel)
        {
            WarnForMissingAccessMetadata(stage, systems);
            batches = BuildExecutionBatches(systems, out notes);
        }
        else
        {
            var reason = markedParallel
                ? (systems.Count <= 1 ? "single-system-stage" : "serialized-by-conflicts")
                : "stage-configured-single-threaded";
            batches = systems.Select(s => new List<SystemDescriptor> { s }).ToList();
            notes = batches.Select(_ => (IReadOnlyList<string>)[reason]).ToList();
        }
        var names = batches.Select(batch => (IReadOnlyList<string>)batch.Select(desc => desc.Name).ToArray()).ToArray();
        var plan = new StagePlan(version, revisions, batches, names, notes.ToArray());
        lock (_lock) _plans[stage] = plan;
        return plan;
    }
}

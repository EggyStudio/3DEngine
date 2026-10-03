using System.Text;
using ImGuiNET;

namespace Engine;

/// <summary>ImGui overlay showing scheduler batch composition and conflict notes per stage.</summary>
/// <remarks>
/// Toggled with <c>Alt+F3</c> (default off).  Reads <see cref="ScheduleDiagnostics"/>
/// from the world and displays each <see cref="Stage"/>'s parallel/sequential batches
/// with planner notes explaining why systems could or couldn't be batched together.
/// </remarks>
/// <seealso cref="ScheduleDiagnostics"/>
/// <seealso cref="Schedule"/>
[Behavior]
public struct ScheduleDebugHud
{
    private static readonly ILogger Logger = Log.Category("Engine.Schedule.DebugHud");
    private static string? _lastDumpPath;

    /// <summary>Draws the schedule debug overlay, including batch lists and diagnostic buttons.</summary>
    /// <param name="ctx">Behavior context providing world resource access.</param>
    [OnRender]
    [ToggleKey(Key.F3, KeyModifier.Alt, DefaultEnabled = false)]
    public static void Draw(BehaviorContext ctx)
    {
        if (!ctx.World.TryGetResource<ScheduleDiagnostics>(out var diagnostics))
            return;

        var batchesByStage = diagnostics.StageBatches;
        var notesByStage = diagnostics.StageBatchNotes;

        ImGui.Begin("Schedule Debug", ImGuiWindowFlags.NoSavedSettings);
        ImGui.Text("Parallel batch planner diagnostics");
        ImGui.Separator();

        if (ImGui.Button("Log Batch Notes"))
            LogBatchNotes(batchesByStage, notesByStage);

        ImGui.SameLine();
        if (ImGui.Button("Dump to File"))
            _lastDumpPath = DumpToFile(batchesByStage, notesByStage);

        if (_lastDumpPath is not null)
            ImGui.Text($"Saved: {_lastDumpPath}");

        ImGui.Separator();

        foreach (var stage in StageOrder.FrameStages())
        {
            var header = $"{stage} ({GetBatchCount(stage, batchesByStage)} batch(es))";
            if (!ImGui.CollapsingHeader(header))
                continue;

            if (!batchesByStage.TryGetValue(stage, out var batches) || batches.Count == 0)
            {
                ImGui.TextDisabled("No batch data yet.");
                continue;
            }

            notesByStage.TryGetValue(stage, out var noteSets);

            for (int i = 0; i < batches.Count; i++)
            {
                var systems = batches[i];
                var mode = systems.Count > 1 ? "parallel" : "sequential";
                ImGui.BulletText($"batch {i + 1}: {mode} => {string.Join(", ", systems)}");

                if (noteSets is null || i >= noteSets.Count || noteSets[i].Count == 0)
                    continue;

                ImGui.Indent();
                ImGui.TextDisabled($"notes: {string.Join(", ", noteSets[i])}");
                ImGui.Unindent();
            }
        }

        ImGui.End();
    }

    private static int GetBatchCount(Stage stage, IReadOnlyDictionary<Stage, IReadOnlyList<IReadOnlyList<string>>> batchesByStage)
        => batchesByStage.TryGetValue(stage, out var batches) ? batches.Count : 0;

    private static void LogBatchNotes(
        IReadOnlyDictionary<Stage, IReadOnlyList<IReadOnlyList<string>>> batchesByStage,
        IReadOnlyDictionary<Stage, IReadOnlyList<IReadOnlyList<string>>> notesByStage)
    {
        foreach (var stage in StageOrder.FrameStages())
        {
            if (!batchesByStage.TryGetValue(stage, out var batches) || batches.Count == 0)
                continue;

            notesByStage.TryGetValue(stage, out var noteSets);

            for (int i = 0; i < batches.Count; i++)
            {
                var systems = string.Join(", ", batches[i]);
                var notes = noteSets is not null && i < noteSets.Count && noteSets[i].Count > 0
                    ? string.Join(", ", noteSets[i])
                    : "none";
                Logger.Info($"Schedule[{stage}] batch {i + 1}: systems=[{systems}] notes=[{notes}]");
            }
        }
    }

    private static string DumpToFile(
        IReadOnlyDictionary<Stage, IReadOnlyList<IReadOnlyList<string>>> batchesByStage,
        IReadOnlyDictionary<Stage, IReadOnlyList<IReadOnlyList<string>>> notesByStage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Schedule Batch Notes ===");
        sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
        sb.AppendLine();

        foreach (var stage in StageOrder.FrameStages())
        {
            sb.AppendLine($"[{stage}]");
            if (!batchesByStage.TryGetValue(stage, out var batches) || batches.Count == 0)
            {
                sb.AppendLine("  <no batch data>");
                sb.AppendLine();
                continue;
            }

            notesByStage.TryGetValue(stage, out var noteSets);

            for (int i = 0; i < batches.Count; i++)
            {
                var mode = batches[i].Count > 1 ? "parallel" : "sequential";
                sb.AppendLine($"  batch {i + 1} ({mode}): {string.Join(", ", batches[i])}");
                if (noteSets is not null && i < noteSets.Count && noteSets[i].Count > 0)
                    sb.AppendLine($"    notes: {string.Join(", ", noteSets[i])}");
            }

            sb.AppendLine();
        }

        var path = Path.Combine(AppContext.BaseDirectory, "schedule_debug.txt");
        File.WriteAllText(path, sb.ToString());
        return path;
    }
}

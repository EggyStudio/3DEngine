namespace Engine;

/// <summary>Provides ordered stage sequences for iteration.</summary>
internal static class StageOrder
{
    private static readonly Stage[] All =
    [
        Stage.Startup,
        Stage.First,
        Stage.PreUpdate,
        Stage.FixedUpdate,
        Stage.Update,
        Stage.PostUpdate,
        Stage.Render,
        Stage.Last,
        Stage.Cleanup,
    ];

    private static readonly Stage[] Frame =
    [
        Stage.First,
        Stage.PreUpdate,
        Stage.FixedUpdate,
        Stage.Update,
        Stage.PostUpdate,
        Stage.Render,
        Stage.Last,
    ];

    private static readonly Stage[] AfterDrawing = [Stage.PostUpdate, Stage.Render, Stage.Last];

    /// <summary>All stages in execution order (Startup → … → Cleanup).</summary>
    public static ReadOnlySpan<Stage> AllInOrder() => All;

    /// <summary>Per-frame stages only (First → … → Last), excluding Startup and Cleanup.</summary>
    public static ReadOnlySpan<Stage> FrameStages() => Frame;

    /// <summary>The stages <see cref="App.EndFrame"/> runs: <see cref="Stage.PostUpdate"/> through <see cref="Stage.Last"/>.</summary>
    public static ReadOnlySpan<Stage> EndFrameStages() => AfterDrawing;
}

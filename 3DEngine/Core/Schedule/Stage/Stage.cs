namespace Engine;

/// <summary>Fixed execution phases processed in a strict order each frame.</summary>
/// <example>
/// <code>
/// // Register a system to the Update stage
/// app.AddSystem(Stage.Update, static world =>
/// {
///     var ecs = world.Resource&lt;EcsWorld&gt;();
///     foreach (var (e, pos, vel) in ecs.Query&lt;Position, Velocity&gt;())
///         ecs.Update(e, new Position(pos.X + vel.X, pos.Y + vel.Y));
/// });
/// </code>
/// <code>
/// // One-time init in Startup, teardown in Cleanup
/// app.AddSystem(Stage.Startup, LoadAssets);
/// app.AddSystem(Stage.Cleanup, ReleaseGpuResources);
/// </code>
/// </example>
public enum Stage
{
    /// <summary>Runs once at application start before the main loop.</summary>
    Startup,
    /// <summary>First per-frame stage - time updates, input polling.</summary>
    First,
    /// <summary>Pre-update logic - physics preparation, AI sensing.</summary>
    PreUpdate,
    /// <summary>Main gameplay logic.</summary>
    Update,
    /// <summary>Post-update logic - constraint solving, transform propagation.</summary>
    PostUpdate,
    /// <summary>Rendering commands - draw calls, GPU submission.</summary>
    Render,
    /// <summary>Last per-frame stage - diagnostic flush, event cleanup.</summary>
    Last,
    /// <summary>Runs once after the main loop exits - teardown and resource disposal.</summary>
    Cleanup,

    /// <summary>
    /// Runs between <see cref="PreUpdate"/> and <see cref="Update"/>, zero or more times a frame, once
    /// for every whole step of <see cref="FixedTime"/> that has accumulated.
    /// </summary>
    /// <remarks>
    /// The last value of the enum so the values of the others are unchanged. Execution order is
    /// <see cref="StageOrder"/>'s, not the enum's.
    /// </remarks>
    FixedUpdate,
}

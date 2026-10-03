namespace Engine;

public sealed partial class App
{
    /// <summary>Registers a system delegate to a given execution stage.</summary>
    /// <param name="stage">The <see cref="Stage"/> during which the system should execute.</param>
    /// <param name="system">The system delegate to invoke each time the stage runs.</param>
    /// <returns>This <see cref="App"/> instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// app.AddSystem(Stage.Update, static world =>
    /// {
    ///     var time = world.Resource&lt;Time&gt;();
    ///     Console.WriteLine($"Delta: {time.DeltaSeconds:F4}s");
    /// });
    /// </code>
    /// </example>
    /// <seealso cref="SystemFn"/>
    /// <seealso cref="Stage"/>
    public App AddSystem(Stage stage, SystemFn system)
    {
        Schedule.AddSystem(stage, system);
        Logger.Trace($"System registered to stage {stage}: {system.Method.DeclaringType?.Name ?? "?"}.{system.Method.Name}");
        return this;
    }

    /// <summary>Registers a system with a <c>run_if</c> condition to a given stage.</summary>
    /// <param name="stage">The <see cref="Stage"/> during which the system should execute.</param>
    /// <param name="system">The system delegate to invoke each time the stage runs.</param>
    /// <param name="runCondition">
    /// A predicate evaluated each frame before the system runs. The system is skipped when
    /// this returns <c>false</c>.
    /// </param>
    /// <returns>This <see cref="App"/> instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// app.AddSystem(Stage.Update, MySystem,
    ///     BehaviorConditions.ResourceIs&lt;GameState&gt;(s => s.IsPlaying));
    /// </code>
    /// </example>
    /// <seealso cref="SystemDescriptor.RunIf"/>
    public App AddSystem(Stage stage, SystemFn system, Func<World, bool> runCondition)
    {
        Schedule.AddSystem(stage, system, runCondition);
        Logger.Trace($"System registered to stage {stage} (conditional): {system.Method.DeclaringType?.Name ?? "?"}.{system.Method.Name}");
        return this;
    }

    /// <summary>Registers a fully configured <see cref="SystemDescriptor"/> to a stage.</summary>
    /// <param name="stage">The <see cref="Stage"/> during which the system should execute.</param>
    /// <param name="descriptor">A pre-configured system descriptor with name, conditions, and resource access metadata.</param>
    /// <returns>This <see cref="App"/> instance for fluent chaining.</returns>
    /// <seealso cref="SystemDescriptor"/>
    public App AddSystem(Stage stage, SystemDescriptor descriptor)
    {
        Schedule.AddSystem(stage, descriptor);
        Logger.Trace($"System descriptor registered to stage {stage}: {descriptor.Name}");
        return this;
    }

    /// <summary>Removes every system tagged with the given <see cref="SystemDescriptor.Source"/> from every stage.</summary>
    /// <param name="source">The provenance tag to match.</param>
    /// <returns>The total number of systems removed.</returns>
    /// <remarks>
    /// Used by hot-reload pipelines (e.g. <c>RuntimeBehaviorCompiler</c>) to evict the previous
    /// generation's systems before re-registering freshly compiled ones under the same tag.
    /// </remarks>
    public int RemoveSystemsBySource(string source)
    {
        var n = Schedule.RemoveSystemsBySource(source);
        if (n > 0) Logger.Trace($"Removed {n} system(s) tagged source='{source}'.");
        return n;
    }
}

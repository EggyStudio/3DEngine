namespace Engine;

/// <summary>Spawns a batch of entities with <see cref="EntityCounter"/> when Space is pressed.</summary>
/// <remarks>
/// Creates <c>100_000</c> entities per press via <see cref="EcsCommands.SpawnBatch{T}(int)"/>.
/// Commands are deferred and applied after PostUpdate. The batch API pre-allocates
/// capacity to avoid resize storms and uses a single delegate allocation instead of N.
/// </remarks>
/// <seealso cref="EntityCounter"/>
/// <seealso cref="EcsCommands"/>
[Behavior]
public struct StressTestSpawner
{
    private const int BatchSize = 100_000;
    private static readonly ILogger Logger = Log.Category("Engine.StressTest");

    /// <summary>Spawns a batch of entities on a single Space press (not held).</summary>
    /// <param name="ctx">Behavior context providing input and deferred command access.</param>
    [OnUpdate]
    public static void OnSpacePressed(BehaviorContext ctx)
    {
        if (!ctx.Input.KeyPressed(Key.Space))
            return;

        Logger.Info($"Spawning {BatchSize:N0} entities...");

        ctx.Cmd.SpawnBatch<EntityCounter>(BatchSize);

        Logger.Info($"Queued {BatchSize:N0} entity spawns (will apply after PostUpdate).");
    }
}

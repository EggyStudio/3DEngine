namespace Engine;

/// <summary>Tracks a per-entity tick counter and displays entity statistics in the HUD.</summary>
/// <seealso cref="PerformanceHud"/>
/// <seealso cref="StressTestSpawner"/>
[Behavior]
public struct EntityCounter
{
    /// <summary>Number of Update ticks this entity has experienced.</summary>
    public int Ticks;

    /// <summary>Increments this entity's tick counter each update.</summary>
    [OnUpdate]
    public void Tick(BehaviorContext ctx) =>
        Ticks++;
}

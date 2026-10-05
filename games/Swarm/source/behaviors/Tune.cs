using Engine;

// The game's numbers, compiled by the running game rather than with it. Saving this file while
// Swarm runs puts the new numbers in play within a second, so a wave is tuned as it is fought.
[Behavior]
public struct Tune
{
    [OnUpdate]
    public static void Apply(BehaviorContext ctx)
    {
        var tuning = ctx.Res<Tuning>();
        tuning.PlayerSpeed = 6;
        tuning.FireInterval = 0.14f;
        tuning.ShotSpeed = 30;
        tuning.EnemySpeed = 1;
        tuning.DamagePerSpeed = 2.2f;
        tuning.FirstWave = 14;
        tuning.MorePerWave = 10;
    }
}

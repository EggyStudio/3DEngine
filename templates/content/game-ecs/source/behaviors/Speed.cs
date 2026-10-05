using Engine;

// Compiled by the running game rather than with it. A number changed here and saved while the game
// runs turns the cubes at the new speed within a second.
[Behavior]
public struct Speed
{
    [OnUpdate]
    public static void Apply(BehaviorContext ctx) => ctx.Res<Tuning>().DegreesPerSecond = 90;
}

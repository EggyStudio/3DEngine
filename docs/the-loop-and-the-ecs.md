# The loop and the ECS

A game on this engine is written two ways, and both are the engine's. The loop of flat calls, as
raylib writes a game, opens the window, runs each frame between `BeginDrawing` and `EndDrawing`,
draws, and puts the interface on top, the calls in the order the frame happens. The ECS holds the
things that move and the data that lives from one frame to the next as entities, with behaviors
that run over them, queries that find them, states that switch them and a schedule that runs it all
in its stages, on several threads where the work allows. raylib has the loop alone, and the ECS is
this engine's own. Both run in the same frames, so a game takes each where it fits, and the two
read each other's data.

## A behavior moves, the loop draws

The `ecs_behaviors` example's balls are entities, each a `Ball` behavior whose fields are that
ball's own, and a method of it falls and bounces each one every frame:

```csharp
[Behavior]
public struct Ball
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Color Color;

    [OnPostUpdate]
    public void Move(BehaviorContext ctx)
    {
        var dt = (float)ctx.Time.DeltaSeconds;
        Velocity.Y -= 9.81f * dt;
        Position += Velocity * dt;
        if (Position.Y < 0.3f)
        {
            Position.Y = 0.3f;
            Velocity.Y = MathF.Abs(Velocity.Y) * 0.8f;
        }
    }
}
```

The loop draws them. `BeginDrawing` runs the frame's stages, the behavior among them, and the loop
reads the balls where the behavior left them:

<!-- compiled with:
struct Ball { public Vector3 Position; public Color Color; }
var camera = new Camera3D(new Vector3(12, 8, 12), new Vector3(0, 2, 0), Vector3.UnitY, 45);
-->
```csharp
var ecs = GetApp().World.Resource<EcsWorld>();
while (!WindowShouldClose())
{
    BeginDrawing();
    ClearBackground(Color.RayWhite);
    BeginMode3D(camera);
    foreach (var (_, ball) in ecs.Query<Ball>())
        DrawSphereEx(ball.Position, 0.3f, 8, 8, ball.Color);
    DrawGrid(20, 1);
    EndMode3D();
    DrawText($"{ecs.Count<Ball>()} balls", 10, 10, 20, Color.DarkGray);
    EndDrawing();
}
```

## Which fits where

The loop fits what happens once a frame in an order, as the frame itself does, what is drawn and in
what order, the camera, the interface, a menu, and a small game whose state is a few variables. A
call says what it does where it is made, and the cheatsheet holds them all.

The ECS fits many things of a kind that each keep their own state, as balls, enemies, bullets or
pickups do, and data that lives across frames and that several parts of a game read. A behavior
keeps the code beside the data it runs over, a query finds every entity with what it asks for, a
state runs some behaviors only on a screen or in a mode, observers answer an entity's arrival, and
the schedule runs behaviors that touch different data at once. Scenes save and load entities, and
`./e3d` asks a running game about them.

A game starts in the loop, as the [first game](first-game.md) does, and moves a part into the ECS
when the part grows into many things or lives across screens, one part at a time, nothing in the
loop being unlearned.

## The voxel game

The voxel game [Voxel-Sandbox](https://github.com/EggyStudio/Voxel-Sandbox) splits the two this
way. The player, the mobs, the stacks dropped on the ground, the falling blocks, the fires and the
lit TNT are entities moved by behaviors of their own, each part of a section of the world is a mesh
entity the engine draws, and the world's loading, the game's mode and the menus are states. Its
loop runs the frame, draws the sky, the world and the mobs through the flat API, and puts the hand,
the crosshair, the hotbar and the menus on top.

## See also

- Examples: [`ecs_behaviors`](../3DEngine.Examples/Ecs/EcsBehaviors.cs)
- [Behaviors and the ECS](behaviors-and-the-ecs.md), for behaviors, stages, spawning and queries
- [States](states.md), for screens and modes
- Previous: [A first game](first-game.md)
- Next: [The window and the frame](window-and-frame.md)

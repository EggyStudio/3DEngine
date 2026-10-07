# States

A game moves between screens and modes: a title, playing, paused, a game over. A state is an enum
the engine keeps, with the value the game is in, and behaviors run, start and stop by it, so code
for the title screen does not have to ask on every line whether the title is showing.

## A state and its moves

A state is an enum of its values, added once with the value to start in. The `ecs_states` example
has three screens:

```csharp
/// <summary>The screens of the example, as a state the app moves between.</summary>
public enum Screen { Title, Playing, Paused }
// ...
// Adding the state wakes the Orbiter's transition and InState methods. The other
// examples never add it, so the behavior stays asleep there.
GetApp().AddState(Screen.Title);
```

The flat API has the same calls: `AddState(Screen.Title)` adds it, `GetState<Screen>()` gives the
value it is in, `IsState(Screen.Paused)` asks whether it is at one, and `SetState(Screen.Playing)`
moves it. The example reads and moves the state from its loop through the world's resources,
which those calls reach as well:

<!-- compiled with:
public enum Screen { Title, Playing, Paused }
World world = null!;
-->
```csharp
// A move is queued here and applied at the start of the next frame, so everything this
// frame still sees the screen it began on.
var screen = world.Resource<State<Screen>>().Current;
var next = world.Resource<NextState<Screen>>();
if (screen == Screen.Title && IsKeyPressed(Key.Return)) next.Set(Screen.Playing);
if (screen == Screen.Playing && IsKeyPressed(Key.P)) next.Set(Screen.Paused);
if (screen == Screen.Paused && IsKeyPressed(Key.P)) next.Set(Screen.Playing);
if (screen != Screen.Title && IsKeyPressed(Key.Q)) next.Set(Screen.Title);
```

A move is queued and made once a frame, after the pre-update stage, so the update and the drawing
see one value. Two moves asked in a frame end at the second, and a move to the value the state is
in does nothing. The loop draws by the value it read:

<!-- compiled with:
public enum Screen { Title, Playing, Paused }
Screen screen = Screen.Title;
-->
```csharp
if (screen == Screen.Title)
{
    DrawText("STATES", 330, 160, 40, Color.DarkGray);
    DrawText("Enter to play", 330, 220, 20, Color.Gray);
}
else
{
    // ...
    if (screen == Screen.Paused)
    {
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Color.RayWhite.Fade(0.6f));
        DrawText("PAUSED", 340, 200, 40, Color.DarkGray);
    }
}
```

## Behaviors that follow a state

A behavior's method runs by a state with an attribute:

| Attribute | Runs |
|---|---|
| `[OnEnter(Screen.Playing)]` | Once, in the frame the state reaches the value |
| `[OnExit(Screen.Title)]` | Once, in the frame it leaves it |
| `[OnTransition(Screen.Paused, Screen.Playing)]` | Once, on that move alone |
| `[InState(Screen.Playing)]` | In its stage every frame while the state is at the value |

The example builds its level on leaving the title, rather than on entering play, so coming back
from a pause keeps it, and clears it on returning to the title:

<!-- compiled with:
public enum Screen { Title, Playing, Paused }
public struct Orbiter { public float Angle, Speed; }
public float Angle, Speed;
-->
```csharp
/// <summary>Builds the level on leaving the title, so resuming from a pause keeps it.</summary>
[OnExit(Screen.Title)]
public static void SpawnLevel(BehaviorContext ctx)
{
    for (int i = 0; i < 12; i++)
    {
        var orbiter = new Orbiter { /* ... */ };
        ctx.Cmd.Spawn((entity, ecs) => ecs.Add(entity, orbiter));
    }
}

/// <summary>Clears the level on returning to the title.</summary>
[OnEnter(Screen.Title)]
public static void ClearLevel(BehaviorContext ctx)
{
    foreach (var (entity, _) in ctx.Ecs.Query<Orbiter>())
        ctx.Cmd.Despawn(entity);
}

/// <summary>Runs per orbiter while playing and not while paused.</summary>
[OnUpdate]
[InState(Screen.Playing)]
public void Orbit(BehaviorContext ctx) => Angle += Speed * (float)ctx.Time.DeltaSeconds;
```

Pausing is then nothing more than a state the update methods are not in. A behavior reads and
moves a state through its context, with `ctx.State<Screen>()` and `ctx.SetState(Screen.Paused)`.
A state that is never added leaves its methods asleep, which is how the example's behavior stays
out of the other examples.

## Entities that go with a state

Clearing a level by hand, as above, needs a query for each kind of thing in it. An entity tied to
a value instead is despawned, with everything below it, when the state leaves that value:

<!-- compiled with:
public enum Screen { Title, Playing, Paused }
BehaviorContext ctx = null!;
-->
```csharp
var enemy = ctx.Ecs.Spawn();
ctx.Ecs.DespawnOnExit(enemy, Screen.Playing);
```

`DespawnOnEnter` is the other edge, for what should be gone by the time a value comes back, such as
a notice put up on leaving it, and it goes before the value's enter systems run, so what they
spawn stays. Where neither edge says it, a rule over the transition does, and the entity goes at
the first transition the rule answers true for:

<!-- compiled with:
public enum Screen { Title, Playing, Paused }
BehaviorContext ctx = null!;
int notice = 0, hint = 0;
-->
```csharp
ctx.Ecs.DespawnOnEnter(notice, Screen.Title);
ctx.Ecs.DespawnWhen<Screen>(hint, transition => transition.To is Screen.Paused or Screen.Title);
```

## States within states

A sub-state exists only while another state is at a value, as a pause that has a meaning only
during play. It is declared on its enum, or added with `AddSubState`:

<!-- compiled with:
public enum Screen { Title, Playing, Paused }
-->
```csharp
[SubStateOf(Screen.Playing)]
public enum Pause { Running, Paused }
```

While the screen is not `Playing`, `Pause` has no value, so `IsState(Pause.Paused)` is false and
methods `[InState(Pause.Running)]` do not run. A computed state is worked out from another every
time it changes, with null for none:

<!-- compiled with:
public enum Screen { Title, Playing, Paused }
public enum InGame { Yes }
-->
```csharp
[ComputedState]
public static InGame? FromScreen(Screen screen) => screen is Screen.Playing ? InGame.Yes : null;
```

## See also

- Examples: [`ecs_states`](../3DEngine.Examples/Ecs/EcsStates.cs)
- The cheatsheet's [States](../CHEATSHEET.md#states)
- Previous: [Behaviors and the ECS](behaviors-and-the-ecs.md)
- Next: [Scenes](scenes.md)

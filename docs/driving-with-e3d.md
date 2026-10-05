# Driving a program with e3d

`e3d` talks to a running program from a terminal: it asks what is in the world, presses keys,
captures frames and reads the log, while the program runs in a window or with none. It is how a
change is checked without sitting at the game, how a script tests a game, and how an AI agent
working on one sees what it does.

## Starting a program that answers

Any program built on the engine answers `e3d` when it is started with `--serve`. In a checkout of
this repository, `./e3d` builds and runs the command line, and `./e3d open` starts an example, or
a program given by its path, serving, and returns once it is ready:

```bash
./e3d open models_loading --hidden            # renders into a window that is never shown
./e3d open models_loading --offscreen         # renders with no window and no display at all
./e3d open ecs_behaviors --headless           # no window, no GPU: logic, ECS and the flat API's state only
./e3d open shapes_basic_3d                    # a visible window, for a person to watch
./e3d open core_basic_window --frames 300     # closes itself after 300 frames
./e3d open ../MyGame/bin/Debug/net10.0/MyGame --hidden
```

| Flag | Variable | The program |
|---|---|---|
| `--serve` | `E3D_SERVE=1` | Answers `e3d` on a local socket |
| `--hidden` | `E3D_HIDDEN=1` | Renders into a window that is never shown, so captures work and nothing appears |
| `--offscreen` | `E3D_OFFSCREEN=1` | Renders with no window and no display, as on a build server or over SSH |
| `--headless` | `E3D_HEADLESS=1` | Has no window and no renderer, frames paced at 60 a second |
| `--frames N` | `E3D_FRAMES=N` | Closes after N frames |

A hidden or offscreen run draws everything a visible one does, so captures show the game as a
player sees it. `./e3d status` says what is serving, and `./e3d stop` closes it, as its close
button would.

## Asking the world

`./e3d list` gives every command the program answers, with what each takes. Every program has the
engine's own, which read and change the ECS:

```bash
./e3d command entity.count                          # how many entities there are
./e3d command entity.list 20                        # the first twenty, with names and components
./e3d command entity.find Player                    # the id of the entity named Player
./e3d command entity.get 42                         # one entity's fields
./e3d command entity.set 42 Transform.Position 0,5,0
./e3d command state.set Screen Playing              # move a state, answering once it has moved
./e3d command scene.save level.json                 # the world, as a scene file
./e3d command memory.collect                        # what it holds, after a full collection
```

`memory` answers with what the program holds, as name and number pairs: the managed heap, the GPU's
buffers, images, descriptor sets, pipelines and memory, the entities with the range of their
ids, and the assets the asset server knows, which a level streamed in climbs by until what it
leaves behind is let go. Read at intervals while a game is played, as `build/soak.sh` does, a value that keeps
climbing is a leak.

`window.size 1280 720`, `window.minimize`, `window.restore`, `window.position` and `window.monitor`
resize and move the window while it draws, and in an offscreen run resize the images it draws
into, as a window's swapchain is made again, or make it zero across, when no frame is drawn until
it is restored. `build/storm.sh` puts a program through a run of them under the validation layer. `window.vsync true` turns vsync on, and `false` off, which makes the swapchain again on
the next frame.

`entity.set` writes vectors, quaternions and colors as numbers joined by commas, enums by name,
and an array as its items split by semicolons, so
`./e3d command entity.set 2 Mesh.Positions "0,1,0;-1,-1,0;1,-1,0"` gives a mesh entity a new
triangle. `component.list`, `resource.list` and `schedule.list` say what the world holds and what runs
in each stage.

## Input and captures

The `input.*` commands press keys, move and click the mouse, type, touch and press gamepad buttons
through the engine itself, so they reach a hidden or offscreen program that no desktop tool can:

```bash
./e3d command input.key W 30                        # hold W for thirty frames
./e3d command input.click 400 225                   # click at a window position
./e3d command input.drag Left 200 0 10              # drag right with the left button
./e3d command input.text "Player One"               # type into the game and ImGui
./e3d command input.button 0 South 30               # press a gamepad's south button
./e3d shot after.png                                # the next frame, as a PNG
```

`input.key` and `input.click` answer once the input is released, so a `shot` after them sees what
it did. `input.button` and `input.axis` make a pad when none is connected, which is how the
`core_input_gamepad` example is captured with no pad on the machine. `./e3d command frames.wait
60` waits for sixty frames, for a scene to settle before it is captured. An answer is waited for up
to 30 seconds, and `--timeout 600` waits longer, both in the terminal and in the program, as a long
wait on a device that draws slowly needs.

## The log

`./e3d logs -n 50` gives the program's last fifty lines, from its own log, so a shader that did not
compile, a file that was not found or an exception is read from the terminal. When `./e3d open`
fails with `NOT_READY`, it prints the tail of the log, which says why.

## Commands of a game's own

A static method marked `[Command]` is a command of the program's console, a verb of `e3d` and a
line of `./e3d list` at once, with its parameters read from the command line:

```csharp
[Command("enemy.spawn", "Spawns enemies: enemy.spawn <count>")]
internal static string Spawn(int count)
{
    // ...
    return $"spawned {count}";
}
```

```bash
./e3d command enemy.spawn 20
```

Commands run on the main thread between frames, so they read and change the world as a behavior
does. A game gives itself a cheat, a level skip or a report of its own state this way, and a test
script calls them.

## Scripts and agents

Every verb takes `--json` and prints one envelope with `success`, the answer under `data`, and any
errors with a code, and exits with `0` when it worked, `2` for a bad command, `4` when nothing is
serving and `6` when the command failed. A script checks a game this way:

```bash
./e3d open ../MyGame/bin/Debug/net10.0/MyGame --offscreen
./e3d command input.key Enter 2
./e3d command frames.wait 120
./e3d command entity.find Player --json
./e3d shot game.png
./e3d stop
```

The repository's own captures of the examples are taken by `build/capture-example.sh` in the
same way. An AI agent working on a game drives it through the same commands, which the skill at
[`.claude/skills/e3d-cli/SKILL.md`](../.claude/skills/e3d-cli/SKILL.md) teaches.

The socket listens on 127.0.0.1 alone, and each request carries a token from a session file only
the program's user can read.

## See also

- The [skill](../.claude/skills/e3d-cli/SKILL.md), with every command and the failure modes
- [BUILDING.md](../.github/BUILDING.md), on the flags and building the command line
- [`build/capture-example.sh`](../build/capture-example.sh), a script that drives every example
- Previous: [Scenes](scenes.md)
- Next: [Shipping a game](shipping-a-game.md)

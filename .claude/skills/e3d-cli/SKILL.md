---
name: e3d-cli
description: Use when working in the 3DEngine repository, to run an example or a program headless or in a hidden window, ask it what is in its world, drive its input, capture what it draws, or read its log, from the terminal and without opening a visible window. Prefer it over launching a program per question, over xdotool, and over guessing what a change looks like.
allowed-tools:
  - Bash
---

# Driving a running 3DEngine app with e3d

## Check for a live session first

**Before asking anything about a running app, run `./e3d status`.** If a session answers, drive
it. A round trip against a running app is a frame. Starting one per question is a second of
startup and a guess about which frame to look at.

```bash
./e3d status                                  # what is serving, and whether it answers
./e3d list                                    # the commands this app answers, with parameters
./e3d command entity.count                    # ask it something
```

If nothing is serving, start one and wait for it to be ready:

```bash
./e3d open models_loading --hidden            # renders into a window that is never shown
./e3d open ecs_behaviors --headless           # no window, no GPU: logic, ECS and the flat API's state only
./e3d open shapes_basic_3d                    # a visible window, when a person should see it
./e3d open core_basic_window --frames 300     # closes itself after 300 frames
```

`open` returns once the app has written that it is ready, so the next command is answered.
`--hidden` is the default choice for an agent: everything renders and `shot` works, and nothing
appears on the desktop. `--headless` is for logic, because there is nothing to capture.

Any program built on the engine takes the same flags (`--serve`, `--headless`, `--hidden`,
`--frames N`) or the variables `E3D_SERVE`, `E3D_HEADLESS`, `E3D_HIDDEN`, `E3D_FRAMES`, so a game
outside the examples is driven by running it with `--serve`.

## The catalog is the API

`./e3d list` is authoritative. The ones every app has:

| command | does |
|---|---|
| `app.quit` | closes the app, as its close button does (`./e3d stop` does this and waits) |
| `frames.wait <n>` | answers once n more frames have run |
| `shot <path>` | writes the next frame to a PNG and answers once it is written (`./e3d shot <path>`) |
| `log.tail <n>` | the last n lines logged (`./e3d logs -n <n>`) |
| `entity.count`, `entity.list <limit>`, `entity.get <id>` | entities, their component types, and one entity's fields |
| `component.list`, `resource.list`, `schedule.list` | what the world holds and what runs each stage |
| `input.key <name> <frames>` | holds a key (`W`, `Space`, `Escape`, `F2`, `LShift`) for that many frames |
| `input.move <x> <y>`, `input.click <x> <y>` | moves the pointer, and clicks, in window coordinates |
| `input.drag <button> <dx> <dy> <frames>` | holds a button while moving the pointer, as a camera drag |
| `input.wheel <amount>` | turns the wheel |
| `input.state` | the keys and buttons down, the pointer and the gamepads, as the engine sees them |
| `input.button <pad> <button> <frames>` | holds a gamepad button (`South`, `East`, `DpadUp`, `Start`), on a console pad when none is connected |
| `input.axis <pad> <axis> <value>` | sets a stick or trigger (`LeftX`, `RightTrigger`) until it is set again |

A game adds its own with a static method:

```csharp
[Command("enemy.spawn", "Spawns enemies: enemy.spawn <count>")]
internal static string Spawn(int count) { /* ConsoleHost.Ecs, ConsoleHost.World */ return $"spawned {count}"; }
```

Commands run between frames on the main thread, so they may read and change the world.

## Input goes through the engine, not the desktop

`input.*` writes into the engine's `Input` and hands mouse events to ImGui, so it works in a
hidden or headless run and on a locked session, where xdotool's events do not arrive. Use it
rather than xdotool.

Injected input is made when the loop next processes events, as a real key would be, so a program
that reads `IsKeyPressed` before `BeginDrawing` sees it. `input.key` and `input.click` answer after
the input is released, so the next `shot` sees the result. Each `./e3d` call is a process of its
own and takes a few frames to start, so a result shorter than that (a quarter-second sound) may be
over before a separate `shot` arrives. `input.state` says whether input arrived.

```bash
./e3d command input.key W 40 && ./e3d shot /tmp/after.png
./e3d command input.click 77 143 && ./e3d command frames.wait 2 && ./e3d shot /tmp/after.png
```

## Reading the answer

Every verb prints a readable answer, or with `--json` one envelope:

```json
{"success": true, "command": "command", "data": {"command": "entity.count", "result": "100", "frame": 94}, "errors": [], "warnings": []}
```

Exit codes: `0` ok, `2` bad arguments or an unknown command, `4` nothing to talk to (no session,
or no renderer for a capture), `6` the command failed. A failure carries a code in `errors[0].code`
(`NO_SESSION`, `AMBIGUOUS_SESSION`, `UNKNOWN_COMMAND`, `NO_RENDERER`, `TIMEOUT`).

## More than one app

When several are serving, a verb refuses with `AMBIGUOUS_SESSION` and lists them. Choose with
`--name <part of the title or entry>` or `--session <pid>`.

## When it says nothing is running

- `./e3d doctor` removes the session files of apps that are gone and says whether the examples are
  built and `slangc` is fetched.
- `e3d open` failing with `NOT_BUILT` means `dotnet build 3DEngine.slnx` has not run.
- `e3d open` failing with `NOT_READY` prints the tail of the app's log, which lives in
  `build/sessions/<example>.log`. A shader error, a missing asset or a crash is there.

## Building and testing

```bash
build/fetch-slang.sh            # slangc, once per checkout
dotnet build 3DEngine.slnx
dotnet test 3DEngine.Tests
```

A change to the engine needs the examples rebuilt before `e3d open` shows it, which
`dotnet build 3DEngine.slnx` does.

## Security notes

The socket listens on 127.0.0.1 only, on a port the system picks, and every request carries a
token from the session file, which only its owner can read. Session files live in
`~/.local/share/3DEngine/sessions` (or `E3D_SESSIONS`).

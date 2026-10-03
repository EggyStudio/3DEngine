# 3DEngine

A C# game engine on .NET 10 with SDL3, Vulkan, Dear ImGui and Slang, used the way raylib is used. A
program opens a window, draws each frame with plain static calls, and draws ImGui in the same frame.
An ECS with source-generated behaviors runs underneath for programs that grow into it.
[.github/DESIGN.md](.github/DESIGN.md) sets out the API's rules and is read before adding to it.

## Driving the engine with `./e3d`

`./e3d` talks to a running app over a local socket. **Before asking anything about a running
program (what is in its world, what a key does, what it looks like), check `./e3d status` and drive
the live session instead of launching a process per question.**

```bash
./e3d status                          # is anything serving?
./e3d open models_loading --hidden    # start an example, rendering in a window never shown
                                      # (--offscreen renders with no display at all)
./e3d list                            # what that app can be asked
./e3d command input.key W 30          # input through the engine, not the desktop
./e3d shot /tmp/x.png                 # capture the next frame
./e3d stop
```

The workflow, the commands and the failure modes are in `.claude/skills/e3d-cli/SKILL.md`. Read it
before driving a session.

## Documents

| File | Holds |
|---|---|
| [.github/DESIGN.md](.github/DESIGN.md) | The flat API, the frame, immediate drawing, the dependency policy |
| [.github/RENDERING.md](.github/RENDERING.md) | The renderer as it is, and the order it grows in |
| [.github/BUILDING.md](.github/BUILDING.md) | Prerequisites, commands, platforms |
| [.github/TODO.md](.github/TODO.md) | Outstanding work, in the order it blocks making a game |
| [.github/REVIEW.md](.github/REVIEW.md) | Direction from the reviewing session, which comes before TODO.md's order |
| [.github/STYLE.md](.github/STYLE.md) | Rules for every comment, message and Markdown file |
| [.github/COMMITS.md](.github/COMMITS.md) | How and when work is committed |

## Building and testing

```bash
build/fetch-slang.sh          # slangc, once per checkout
dotnet build 3DEngine.slnx
dotnet test 3DEngine.Tests
dotnet run --project 3DEngine.Examples
```

The suite uses `NullGraphicsDevice` wherever a test would otherwise need a GPU, so it runs on a
machine with no display. Tests that need `slangc`, a Vulkan device or an audio device report as
skipped, with the reason, where it is missing (`3DEngine.Tests/Needs.cs`), so a run's summary says
how much of the suite ran.

Captures and input go through `./e3d` (above), which works in a hidden window and on a locked
desktop session. xdotool does not, because its events reach a window only while it has focus and
the session accepts them. The flat API can also run without a window in a test, through
`Engine3D.UseApp(app)`, as `Engine3DAudioTests` does.

A new or changed example gets a fresh capture in `.github/assets/examples/<name>.png`
(`./e3d open <name> --hidden`, then `./e3d shot`), and the README links captures by
`https://raw.githubusercontent.com/EggyStudio/3DEngine/main/...`, so they show once the commit is
pushed.

## Where things are

| Path | Holds |
|---|---|
| `3DEngine/Api` | `Engine3D`, the flat API, one file per area, with `Color` and `Camera3D` |
| `3DEngine/Core` | `App`, `Config`, `World` (resources), the schedule and stages, plugins, events, input, time, logging |
| `3DEngine/Ecs` | `EcsWorld` (sparse sets), `EcsCommands`, the frame's change bits |
| `3DEngine/Behaviors` | `[Behavior]` attributes, `BehaviorContext`, `BehaviorsPlugin`, the runtime behavior compiler |
| `3DEngine/Components` | `Transform`, `Camera`, `Mesh`, `Material` and the render-side mirrors |
| `3DEngine/Platform` | The SDL3 window, main loop and input, and audio under `Audio/` |
| `3DEngine/Graphics` | The Vulkan device over Vortice.Vulkan, and `SlangCompiler` |
| `3DEngine/Rendering` | `Renderer`, `RenderPlugin`, the render graph, extracts, pipelines, lighting, the `DrawList` and its pass under `Immediate/`, and the model pass with `MeshEntityDraws` under `Models/` |
| `3DEngine/Gui` | Dear ImGui's context and input, and its Vulkan pass under `Vulkan/` |
| `3DEngine/Assets` | `AssetServer`, handles, textures (StbImageSharp), models (Assimp), materials |
| `3DEngine/Scenes` | The scene model a reader produces, and the spawner that turns it into entities |
| `3DEngine/Physics` | Rigid bodies over BepuPhysics |
| `3DEngine/Shaders` | Built-in Slang shaders, staged under `source/shaders` beside every program |
| `build/` | `fetch-slang.sh`, and the compiler it downloads under `tools/` |
| `3DEngine.Generator` | The behavior and command source generators. The engine also compiles the behavior one in for scripts |
| `3DEngine.Tests` | xUnit tests, in folders matching the engine's |
| `3DEngine/Diagnostics` | `[Command]` and the console catalog, the log ring, built-in and input commands, `PngWriter` |
| `3DEngine/Cli` | The server side of `./e3d`: socket, request queue, session files, `CliPlugin` |
| `3DEngine.Cli` | The `e3d` client, which `./e3d` builds and runs |
| `3DEngine.Examples` | raylib-style example programs, run by name |
| `.github/assets/examples` | A capture of each example, which the README shows |

## Conventions

- Comments explain **why**, in prose. Match the surrounding density rather than trimming them.
- Public API carries XML docs, with a `<remarks>` section where there is reasoning or a trap to
  record.
- The library does not reflect at runtime where the generator can emit the registration instead,
  so that it survives trimming and AOT.
- There is no editor application, and none is planned (DESIGN.md §7). Tools are ImGui windows a
  program draws in its own frame.
- A `[Command]` method is a console command, an `e3d command` verb and a line in `e3d list` at
  once. Adding one is writing one.
- Every public function of the flat API has its line in `.github/CHEATSHEET.md`, changed in the same
  commit as the function.
- No dependency is added beyond what [.github/DESIGN.md](.github/DESIGN.md) §8 allows without that
  section being changed to say why.
- Prose in this repository follows `.github/STYLE.md`, which governs comments, XML documentation,
  messages and Markdown. Read it before writing any of them.
- Each finished batch of work is committed on `main` and pushed to `origin/main`, never with force,
  with a message whose subject is three invisible marks and whose description is one plain
  sentence. Before each commit,
  `.github/STYLE.md` is read and applied to what is staged, the message included.
  `.github/COMMITS.md` has the exact form, that pass, and how to split a batch that shares a file
  with another.
- `.github/REVIEW.md` is read before a batch is started and before each commit. A second session
  writes it after reading the code and the history, and its Now list comes before TODO.md's order.
  Only its Replies section is edited here, for an item that is disputed or blocked, and the file is
  committed with whichever batch comes next.
- The code before the redesign is on the local `legacy-modules` branch, as git submodules under
  `Modules/`. It is read for reference and not merged back.

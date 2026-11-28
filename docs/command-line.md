# The command line

`e3d` talks to a running program from a terminal. It asks what is in the world, presses keys,
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
./e3d open models_geometric_shapes            # a visible window, for a person to watch
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
| `--frame-time S` | `E3D_FRAME_TIME=S` | Advances time by S seconds a frame and reads no clock, so a capture of something moving is the same on every machine |
| `--samples N` | `E3D_SAMPLES=N` | Draws a window with N samples a pixel where its program asks for none, over `window.samples` for the run, 1 drawing as raylib does |
| `--seed N` | `E3D_SEED=N` | Seeds the random generator with N as the window opens, in place of the clock, so a program that sets no seed places things alike each run |
| `--live`, `--no-live` | `E3D_LIVE=1`, `E3D_LIVE=0` | Loads again the files it loaded by name as they are saved, or does not, whatever its config says ([Editing while it runs](editing-while-running.md)) |

Each flag's value is a setting a running program reads back. `run.serve`, `run.hidden`,
`run.offscreen`, `run.headless`, `run.frames` and `run.seed` only read, the seed
the clock's seconds where the run names none, and the variable of `time.frame` or `live.reload`
overrides it, so a set of either holds for the run only where the variable is unset. A hidden or offscreen run draws everything a visible one
does, so captures show the game as a player sees it. `./e3d status` says what is serving, and `./e3d stop` closes it, as its close
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
./e3d command memory.buffers 20                     # the GPU's buffers by where each was made
./e3d command memory.images 20                      # the GPU's images by size, format and use
```

`memory` answers with what the program holds, as name and number pairs for the managed heap,
the process's resident memory, the GPU's buffers, images, descriptor sets, pipelines and memory, the
entities with the range of their ids, and the assets the asset server knows, which a level streamed
in climbs by until what it leaves behind is let go. Read at intervals while a game is played, as
`build/soak.sh` does, a value that keeps climbing is a leak. `memory.buffers` names what the GPU's
buffers are, by the file and line of the engine that made each and what it is for, the most first,
as Manor's were named as one vertex and one index buffer for each mesh of each of its cells, 176
meshes at sixteen cells, until a model's meshes were shared by every entity that shows it.
`memory.images` names the GPU's images by their size, format, use and levels, the most first, as
Manor's walk held 27 textures of 128 by 128 at its most and 16 as it began.

`window.size 1280 720`, `window.minimize`, `window.restore` and `window.position` resize and move
the window while it draws, and in an offscreen run resize the images it draws into, as a window's
swapchain is made again, or make it zero across, when no frame is drawn until it is restored.
`build/storm.sh` puts a program through a run of them and of the setting `window.monitor` under the
validation layer. The setting `window.vsync on` turns vsync on, and `off` off, which makes the
swapchain again on the next frame, `window.mode borderless`, `borderlessfullscreen`, `fullscreen` or
`windowed` sets how the window stands on its screen, `window.resolution 1280x720` or `native` its
size as a window and as a borderless one and its mode in fullscreen, `render.scale 67%` the share of
that the scene is drawn at, `render.filter linear`, `nearest` or `sharp` how the picture is brought
onto the window, `window.monitor 1` moves it to the second screen, and `window.state` says whether
the window is shown or hidden, covered by others, focused, minimized or maximized, its mode where it
covers its screen, and its size.

The engine's values a line sets are settings, each read by its name alone, as `window.vsync`,
answered with its value, its default where it differs and the variable that gives it, set by its
name and a value, and put back by `reset` and its name. `list` names them beside the commands, and
the settings panel F5 opens shows them a page to each first word ([Window and
frame](window-and-frame.md#the-debug-keys)), where the pages and the file a player's are kept in
are listed.

`imgui.viewports on` lets an ImGui window dragged out of the program's window have a window of its
own, as a program does by setting `ImGuiConfigFlags.ViewportsEnable`, and `off` keeps them in.
`imgui.windows` lists ImGui's viewports, where each is and its window and swapchain, and `imgui.shot
1 viewport.png` writes the next frame of the first of them to a file, as `shot` does the program's
window. `shot.shown screen.png` writes the frame as the window shows it, which at a `render.scale`
or a `render.aspect` of its own is the picture brought onto the window as `render.filter` and
`render.fit` say with the interface over it, where `shot` writes the picture as it is drawn. A
window of a hidden program's is never shown either.

`profile` gives where a frame's time goes, averaged over about a second, and `profile.slowest` the
slowest frame since it was last asked, every stage, system and render phase measured in it, so a
stall of one frame shows with what held it, the program's own work or a wait on the GPU or the
display (BUILDING.md, Timing a program). The setting `gi.passes on` splits global illumination's
GPU time there by pass, each cascade's trace, the merges, the gather and the screen's probes as
`gpu.gi_` lines, which `gi.passes off` takes back to the one `gpu.global_illumination`.

`model.room house 6 3 5 0.2`, `model.select house +z`, `model.cut house window 1.2 1 4.5 1` and the
other `model.*` commands run the lines of an edit mesh's recipe on a model by its name, each
answering the model's counts, and `model.show house 0 0 0` draws it as it is cut, so a level is
blocked out from the terminal ([Modeling](modeling.md#blocking-out-from-the-terminal)).

Where a program builds the scene's distance field, the setting `field.show 0` draws its first
cascade over the window as the field holds the scene, `-1` none, `field.state` says whether it is built and for how
many frames, where each cascade lies, the far one named, the GPU memory the field takes, how many
meshes are in it, how many triangles the frame's builds splatted and left waiting, of a budget
raised where the frame has room, and the frames and the triangles the field took to hold its last
change, from a block broken to the field holding it, or what it still waits for,
`field.wait` answers once it is built and its light has run 60 frames on it, as a picture of the
scene waits before it is taken, `pipelines.wait` answers once no pipeline is being made, on the
workers or in the driver, so a check that reads a pixel after the first sight of a material reads
it after its pipelines, and
`field.rebuild 400` builds a cascade every frame for four hundred frames,
so `profile` gives what a build costs, and `field.march 17 1 1 -0.86 0.51 0 sun` marches the field
from a point along a way as global illumination's passes march it, answering where it met a surface,
in which cascade and cell, and each step, then marches on from that surface toward the sun as a
probe's hit does where the window's shadow map does not hold it. Where global illumination is on, `gi.state` says the quality,
the cascades of probes and their rays, the screen's probes, the reflections, and the GPU memory each
takes, the setting `gi.rays off` or `on` turns off or on the GPU's own rays High traces what the field misses
through ([Materials, light and shadows](materials-light-and-shadows.md)), and on a GPU that traces
rays `gi.reference ref.png 1024` path traces the view as a reference, 1,024 paths a pixel,
`gi.reference once.png 1024 1` one of a single bounce, `gi.reference shiny.png 1024 -1 specular`
one whose first faces reflect the indirect light too, `gi.reference glossy.png 1024 -1 gloss` one
whose every face does, and `gi.compare ref.png` gives the light of
each wall, floor and block in the frame against it, with a picture of the difference and the frame's
own light beside it. `gi.probe 0 2.5 0 256` reads the probe nearest a point against a reference of
the light arriving at it from each way, where its rays met a surface, merged with the cascades
above, and on each of its six faces, each face also by where its ways were met, within their
interval, before it began or in the cascade above, with the ways that carry most of the face
furthest from the reference's, each with how far its rays went against what the reference's path
through the texel's middle first met and how far along, and whether the probe was carried past its
interval with its rays moving, and `gi.screenprobe 400 300 256` reads the screen's probe under a
pixel, each of its sixteen rays where it was met, on the screen, in the field or past its reach,
against the reference's light along the same way, both printing light to three significant
figures, so a scene lit in physical units, whose shade holds a ten-thousandth of a unit of light
or less, reads. `light.terms 400 300` reads a pixel's linear light by the model pass's terms,
each drawn alone in turn, the probes' indirect light, the glow lights', the reflections, the
environment's, the ambient lights', the lights' own and what the surface gives off, less what the
frame holds there with none of them, as a fog's light past the surface, beside their sum and the
light drawn with every term, so a pixel brighter than its surfaces shows which term carries it.
The setting `gi.show` draws what the light holds over the
window, `tiles`, `light`, `filtered` and `history` the screen's probes, `rays`, `merged` and `probes`
the cascade `gi.show_cascade` names, and `none` the window again, and `gi.difference ref.png` draws
the frame against a reference. The setting `gi.merge off` leaves the merge of the cascades out, as
`gi.history off` and `gi.filter off` do the screen's probes' parts, `gi.screen on` puts the
screen's probes in, off unless set, so what they cost and give can be read,
`gi.screen_reflection on` steps a glossy surface's reflection through the window's depth before
the field, off unless set, `gi.gather pixels` takes one ray from each
texel of a grid of the window's pixels in their place at the size `gi.gather_size` sets, traced as far as `gi.gather_reach` sets, half its ways drawn toward the cascades' light with `gi.gather_guide cascades`, and its rays meeting the meshes through the device's own ray tracing at High with `gi.gather_rays on`,
`gi.again off` the light past the first bounce from the frame before's probes, so light bounces once, `gi.follow off` the probes taking the
indirect light at the share their own light kept, so the indirect light of a light switched off
fades over the frames each bounce takes, `gi.glow off` the small emissive surfaces carried as
lights, whose light the rays leave out all the same, so what they give can be seen and timed,
`gi.specular off` the surfaces' reflection of the indirect light, so the diffuse light is read apart
from it, `gi.spread off` the coarser cascades' trace of half their probes a frame, so each traces
all of them every frame, `gi.points off` a hit's faint glow lights taken as one point at their
middle, so every light takes its own form, `gi.footprint off` the sun at a still probe's hit read
over the patch of the face its ray stands for, so it is read at the hit alone, `gi.under off` the
probes below every surface left out where a surface does not see them, so they keep a twentieth of
their weight as every probe a surface does not see, `gi.own_place off` each cascade's probes placed
by the field from the cascade they stand in outward, so they are placed by the finest cascade
holding each spot and a probe stands elsewhere each time a finer cascade's box moves with the eye,
`gi.cascade 2` keeps the third cascade's light alone, and `on` or `-1` puts each
back. `gi.meshes on` traces the world's
probes' rays through the GPU's own
rays at `High` in place of the field, crossing what lets light through as the field does, a step
that stays off until it reads every view at least as near a path-traced reference as the field, and
`off` puts the field back. `gi.own_field on` marches each cascade's probes' rays through the field
from the cascade they stand in outward rather than from the finest cascade holding each point, so a
coarser cascade's light holds still where a finer cascade's box moves with the eye, a step that
stays off since it lights some walls well past a path-traced reference, and `off` puts the finest
first back. `gi.toward on` has a pixel read each of its probes' faces moved toward the
surface the face's ray met, as far as the pixel lies behind the probe toward it, as a probe's hit
reads them, a step that stays off since a face moved so is not tested from the pixel, so light
reaches a floor behind a thin wall, and `off` reads the faces as they stand. `gi.surface on` has
each pixel take the world's probes' light at its point of the window's depth as the
RadianceCascades module gathers a surface point, a step that stays off since, once the bounce has
settled, it reads four of the six parts of a wall beside a door further from a path-traced
reference than the faces do, and `off` reads the faces. `gi.layout split` lays the probes a pixel
takes the world's light from out as the RadianceCascades module's split cascades, kept near the
points of the window's depth the eye sees, each traced by a ray a point a frame and gathered at each
pixel's point about the normal the model pass shades it with, the grid's probes still lighting what
those rays meet, a layout that stays off since, once the bounce has settled, it reads a wall beside
a door at 1.23 of a path-traced reference as the grid does, two of its six parts further from it
than the grid's, and `dense` puts the grid back. `gi.screen_cascades on` has the screen's
probes stand in cascades of their own at `High` where the device traces rays, each level's probes
twice as far apart with twice as many ways along each side, each tracing a stretch twice as long
through the device's rays and the last on to the scene's end, with nothing kept from the frames
before, a step that stays off since, though it reads a shaded court, a far doorway and a wall
beside a door nearer a path-traced reference, a turn moves the light of eighteen times as many
pixels and the frame takes 0.44 ms more on Sponza and 1.69 ms more on the Bistro, and `off` puts
the screen probes' rays back. `gi.side 8` holds
every cascade to 8 probes a side, `gi.side 8,8,16` the first two to 8 and the rest to 16, and
`reset gi.side` 8 for the first two and 12 past them on any device, the default on a GPU, where a
device that draws on its CPU holds 8 a side unless set, and `gi.shell_parts 4` traces the probes
past each cascade's inner eight a side a quarter a frame, `0` every frame.

`entity.set` writes vectors, quaternions and colors as numbers joined by commas, enums by name,
and an array as its items split by semicolons, so
`./e3d command entity.set 2 Mesh.Positions "0,1,0;-1,-1,0;1,-1,0"` gives a mesh entity a new
triangle. `component.list`, `resource.list` and `schedule.list` say what the world holds and what runs
in each stage, and `schedule.dump` writes to a file how the last frame ran them, which systems shared
a batch and ran in parallel and the planner's notes on why the others could not, and answers its
path.

## Input and captures

The `input.*` commands press keys, move and click the mouse, type, touch and press gamepad buttons
through the engine itself, so they reach a hidden or offscreen program that no desktop tool can:

```bash
./e3d command input.key W 30                        # hold W for thirty frames
./e3d command input.click 400 225                   # click at a window position
./e3d command input.click 400 225 2                 # a double click, which a game reads as a double tap
./e3d command input.drag Left 200 0 10              # drag right with the left button
./e3d command input.text "Player One"               # type into the game and ImGui
./e3d command input.button 0 RightFaceDown 30       # press a gamepad's bottom face button
./e3d shot after.png                                # the frame being drawn, as a PNG, with its number
./e3d command frame.read 0 0 64 64                  # a rectangle of the next frame, its mean color and largest step
./e3d command sky.time 18                           # the day sky at six in the evening
```

`input.key` and `input.click` answer once the input is released, so a `shot` after them sees what
it did. A script asserts a picture without downloading one with `frame.read x y w h`, which reads a
rectangle of the next frame drawn, in window pixels from the top left, or the whole frame with no
measures, through the capture `shot` takes, and answers its mean sRGB color, the largest step
between neighboring pixels along its middle row and column with the pixel the step starts at, and
the frame's number, as `mean 81 137 218 step 0 at 0 0 frame 94`. A sky's color is read as the color
it was cleared to, and the edge of a shape or a shadow shows as a step, so `frame.read` over a box's
side says whether it is lit, and over the line where it meets the floor whether its shadow is
there. `sky.time 18` sets the hour of the day sky a program runs, so a script takes its pictures at
the hours it names, and answers where the sky stands, its time, the hour of the day, the day, the
sun's height in degrees and the moon's phase, as `time 18.00 hour 18.00 day 0 sun 0.0 moon 0`.
The setting `sky.hours` holds the same hours, on the panel's `sky` page.
`input.button` and `input.axis` make pads up to the one they name where fewer are
connected, four at most, which is how the `core_input_gamepad` example is captured with no pad on
the machine and `games/Sumo`'s second player is controlled on pad 1. `./e3d command frames.wait
60` waits for sixty frames, for a scene to settle before it is captured. An answer is waited for up
to 30 seconds, and `--timeout 600` waits longer, both in the terminal and in the program, as a long
wait on a device that draws slowly needs.

A run is recorded as an animated GIF or PNG, by the file's extension, and the client waits for the
file, however long the app's clock takes to reach the length on the machine:

```bash
./e3d record walk.gif 5s --every 250ms              # twenty pictures a quarter second apart
./e3d record walk.png 120f --every 15f --scale 0.5  # by frames of the clock, at half the size
./e3d record walk.gif 8 --play 100ms                # eight pictures, each played for a tenth
```

Captures already on disk are made into one animation by `build/animate.py`, a GIF, an animated PNG
or an animated WebP by the extension, with Pillow where it is installed and the engine's own
writers through `./e3d animate` where it is not, for PNG pictures and a GIF or an animated PNG:

```bash
python3 build/animate.py build/walk.gif captures/walk-*.png --delay 250
```

A recording of a view held still is read for flicker by `build/flicker.py`, picture to picture, the
share of the pixels that changed by 12 and by 25 levels in any channel and a map of where, from a
folder of pictures numbered from `0001.png`, as F2 held writes one, or from an animated PNG, so a
still view that moves is measured the same way each time:

```bash
./e3d record still.png 120f --every 1f               # every frame, two seconds at 60
python3 build/flicker.py still.png --map where.png
```

A camera turned about its point is read the same way with `--turn` and the first picture's camera,
each picture carried into the next by the turn, and its map carried back into the first picture's
camera, so a place that flickers stays one place on it. `--against` reads a second recording of the
same turn pair by pair beside the first and counts only the pixels that move in the first and not in
the second, so one part's flicker is read apart from what both runs share. `--blur` blurs both
pictures of a pair first, so the hard edges of pixel textures read still and the light's blocks stay:

```bash
python3 build/flicker.py on.png --turn 0.75 --eye 0,0,0 --target 0,0,-1 --fov 70 --levels 4,12 \
    --against off.png --blur 3 --map where.png
```

## The log

`./e3d logs -n 50` gives the program's last fifty lines, from its own log, so a shader that did not
compile, a file that was not found or an exception is read from the terminal. When `./e3d open`
fails with `NOT_READY`, it prints the tail of the log, which says why, and where the program had
already exited, its exit code and what a crash's code means, an access violation or a signal, since
a program that dies in native code logs nothing of it. A program that closes of its own before it
was ready logs a warning saying so.

## Commands of a game's own

A static method marked `[Command]` is a command of the program's console, a verb of `e3d` and a
line of `./e3d list` at once, with its parameters read from the command line. The attribute is the
Diagnostics module's, in the namespace `Diagnostics`, which the engine's package carries:

```csharp
using Diagnostics;

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

## C# typed at a running program

`./e3d eval` compiles C# against the running program and runs it on the main thread between
frames, for what no command was written for. The world is `world`, the ECS `ecs` and the app
`app`, with the namespaces an example's file has, the flat API among them. A last expression with
no semicolon answers with its value:

```bash
./e3d eval 'ecs.EntityCount'
./e3d eval 'ecs.Query<Transform>().Select(row => row.Item2.Position)'
./e3d eval 'var e = ecs.Spawn(); ecs.Add(e, new Transform(new Vector3(0, 5, 0))); e'
./e3d eval 'SetTargetFPS(30)'
./e3d eval -f look.cs
```

A file is a program's top-level statements, as a `Program.cs` is, so it may begin with using
directives and declare local functions and types after its statements, and the game's own types
are in reach. A fragment that does not compile is refused with the compiler's errors and their
lines, and one that throws with the exception's type and message, its stack in the log. The first
fragment holds its frame while the compiler starts, about two seconds, and each after for about
150 milliseconds, as `./e3d command profile.slowest` read after a fragment on
`models_reflection_probe`. A native build has no compiler and refuses it.

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
same way. An AI agent working on a game controls it through the same commands, which
[`.github/E3D.md`](../.github/E3D.md) teaches.

The socket listens on 127.0.0.1 alone, and each request carries a token from a session file only
the program's user can read. `eval` runs any code with the program's rights, which are that user's
own.

## See also

- [E3D.md](../.github/E3D.md), with every command and the failure modes
- [BUILDING.md](../.github/BUILDING.md), on the flags and building the command line
- [`build/capture-example.sh`](../build/capture-example.sh), a script that captures every example
  through the command line
- Previous: [Scenes](scenes.md)
- Next: [Shipping a game](shipping-a-game.md)

# Feature test

`games/FeatureTest` puts every area of the engine on one map, for a tester to try a build with and
for the workflow to walk. It is built outside the solution from the package `build/pack.sh` makes,
as the games are, as [BUILDING.md](../../.github/BUILDING.md) says.

## The map

You start on the hub, a plaza with a cube turning on a pedestal and a signpost at its edge for each
zone, its road running out to it. West is the drawing zone, every 3D shape on a plinth of its own
along the road, a grid laid through rlgl's matrix, and an easel showing a texture that 2D shapes and
text are drawn into each frame. East is the course, six lanes running east side by side, each a
station the character is tried on: a ramp of 25 degrees, stairs of 0.25, a gap to jump, a platform
that carries you, crates to push and a tunnel to crouch through. A trigger at each lane's end turns
green once you reach it. North-west is the models zone, every mesh the engine generates on a plinth,
a heightmap and a maze from images, an OBJ, a skinned arm, a robot run by an animation state machine
it names by its file's id, strips of morph targets and four hundred cubes in one instanced draw.
North is the lights zone, a red lamp and a blue spot that cast shadows, a block the sun throws a
shadow from, a glowing sphere, balls from rough to polished and from dielectric to metal, and a room
a reflection probe lights with itself. North-east is a white room with a red wall and a green one,
where global illumination is on while you are there, and off again as you leave, since it costs
every frame. South-east is the particles zone, a fountain, a fire, smoke, sparks that bounce, lit
particles and a burst every two seconds. South is the effects zone, a board with a red patch and a
glowing bar that bloom, a vignette, colour grading, exposure, depth of field, motion blur and a
tonemapping curve are shown on in turn as you arrive, a knot drawn through a toon shader, a texture
a compute shader paints, and the Game of Life and a greying shader read back unseen. South-west is
the audio zone, a speaker whose drone is panned toward it as you move, music, a tone a stream makes
and a coin every two seconds, and past it on the outer ring the text zone, a page of Lato past
Latin-1, Hebrew read right to left, a colour glyph, the default font, a distance field font turned
and a ligature measured with and without. Beyond the drawing zone on the outer ring is the scenes
zone, a level written as a scene file and started as a saved game, a prefab a scene places, a stack
of crates whose numbers are a data file found by its id after a rename, a block moved and one
spawned that the save brings back, and how often you came, kept between runs. Beyond the effects
zone is the interface zone, screens stepped by Enter and P, a sign you type on, an ImGui window and
a health bar and a gauge in 2D.

## Keys

| Key | Does |
|---|---|
| W A S D | walk, relative to the camera |
| Shift | run |
| Space | jump, and rise while flying |
| C | crouch, and sink while flying |
| F | fly, with gravity held off and walls still stopping you |
| R | go back to the start of the zone you are in |
| Tab | hold the mouse in the window to look with it |
| Arrows | look |
| Enter, P | in the interface zone, start from the title and pause |
| Backspace | in the interface zone, take back a character from the sign |
| F1 | the panel |
| F2 | a screenshot, into a `screenshots` folder beside the program |
| F3 | the engine's performance window |

## The panel

F1 opens it, an ImGui window the keyboard and a gamepad move through as well as the mouse, and while
it holds the keyboard you are not walked by it. Its Frame part shows the frame's costs as the
profile averages them, the frame, the program's update and drawing, the render graph and the GPU's
passes, and opens the engine's profile window with all of them. Its Engine part turns the sun's
shadows, ambient occlusion, FXAA and the frame rate's cap on and off. Each zone has a part of its
own with its status, its features to turn on and off, and buttons that take you to its start and its
spots.

## When something goes wrong

An exception nothing catches ends the program and writes `crash-` and the time, `.txt`, beside it:
what was thrown, where you stood, each zone's last status, the frame's costs, the system and the last
lines of the engine's log. The next run names the newest one in its log and on the panel. Send it
with a report. The engine writes its own `logs/Crash.log` there too, with the exception alone.

## The console

Each zone says what it found in a status line, the easel's from its texture read back from the GPU
at the pixels its shapes were drawn at, and the course's from the triggers its stations end in.
`./e3d` reads them while the program runs:

```bash
./e3d open games/FeatureTest/bin/Debug/net10.0/FeatureTest --hidden
./e3d command feature.status              # each zone's line
./e3d command feature.status course       # one zone's
./e3d command feature.zone drawing        # go to a zone's start
./e3d command feature.zone course tunnel  # go to a station, which is tried afresh
./e3d command player                      # where you stand, how you move and which zone you are in
./e3d command player.put 0 0 7 0          # stand somewhere, facing a bearing from north
./e3d command player.fly on
./e3d command feature.toggles             # every feature the panel turns on and off
./e3d command feature.toggle engine shadows off
./e3d command feature.costs               # the frame's costs the panel shows
./e3d command feature.panel on
./e3d command feature.crash               # throw at the next frame, to see the crash file written
```

`build/drive-game.sh FeatureTest` walks it as the workflow does, and fails at the first zone or
station that does not say it did its work.

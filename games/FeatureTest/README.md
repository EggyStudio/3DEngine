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
green once you reach it.

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
```

`build/drive-game.sh FeatureTest` walks it as the workflow does, and fails at the first zone or
station that does not say it did its work.

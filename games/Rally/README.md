# Rally

`games/Rally` is three laps of a dirt road over hills against the clock, and against the ghost of
the best lap driven before. The hills are a heightmap from Perlin noise with the road pressed flat
into it, a static collider of its own mesh, with eight gates along the road as triggers, and the
car is the engine's vehicle, a box held up by four raycast wheels as springs, driven by the keys, a
gamepad or an autopilot. Its engine plays as a music stream and its skids and the race's beeps as
sounds, dust from its rear wheels is lit particles, the race's screens are states, and the best lap
is saved to a file.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.

# Sumo

`games/Sumo` is for two players on one screen, two marbles on a ring each trying to knock the other
off. The window is split in two, each half a render texture drawn through its player's camera, with
the second player on a second gamepad or the arrow keys, the marbles drawn by a shader of the game's
own, the floor painted each frame by a compute shader with a ripple from each bump, a crowd of
hundreds in one instanced draw, numbers over the marbles as billboards and the score in a distance
field font.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.

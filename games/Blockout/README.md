# Blockout

`games/Blockout` is a level blocked out with the modeling tools and walked in, a hall with a room
over it, a corridor and a second room, stairs between the two floors, two doors and a window, in the
prototype materials. The level is made at load from one recipe, `resources/levels/blockout.model`,
BevyCSharp's Blockout's level file taken as it is, and walked on through the collider built from
it by a character controller, with the mouse or the arrows to look. Tab opens the modeling window
on the level, so it is reshaped while it is walked in, its model and collider made again on each
change, and F5 saves the recipe. The day sky stands at two in the afternoon, its sun through the
window and the open room and global illumination lighting the rest, with a lamp in the hall.

The model commands reach the level as `level`, and `blockout.status` and `blockout.autopilot`
say where the walker is and walk it up the stairs, down them, round the hall or through every room.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.

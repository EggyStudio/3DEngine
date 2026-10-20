# Span

`games/Span` is a bridge built over a gap from planks and ropes for a heavy load to roll across.
Seen from the side, a click on two nodes of the grid lays a plank between them, and a right click on
a tower and then on a node a plank ends at hangs a rope. The planks are boxes joined by hinges
across the bridge where they meet and to kinematic anchors at the cliffs, the ropes are distance
joints, and each frame the game reads every joint's force with `GetPhysicsJointForce` and breaks
the joints held harder than their strength. A road of planks alone holds the load as a chain does,
pulled near straight, and breaks, where a truss under it, or ropes to its middle, carry the load
across.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.

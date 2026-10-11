# Tactics

`games/Tactics` is a strategy board seen from above: soldiers, archers and a knight on grass, woods,
hills and water, picked with the mouse by a ray from the camera to the tile or unit it meets, or
several at once by a box dragged round them and listed in an ImGui panel, walking as far as their
moves allow by the cheapest way, which Dijkstra's search finds. Each side sees only what its units
can, past no woods, and the rest is fog. The computer's side heads for the nearest enemy it sees by
A*, and a match is saved to a file and taken up again.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.

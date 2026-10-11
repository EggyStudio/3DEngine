# Swarm

`games/Swarm` is written in the ECS, its loop only opening and closing frames: waves of creatures
placed from prefabs walk at the player, hundreds at once, each moved by a behavior taking its
components, shots are triggers that harm by their speed, the waves go by states and sub-states, a
HUD is drawn in ImGui, many short sounds overlap, and a script of the game's numbers is compiled
again whenever it is saved while the game runs. `games/Rally` races a car round a dirt road over
hills from noise: a heightmap solid as it is drawn, a car of one box held up by four rays cast to
the ground as springs, gates that are triggers counting the laps, a ghost of the best lap kept in a
file, dust from the tyres, motion blur with speed, and an engine's note pitched by it.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.

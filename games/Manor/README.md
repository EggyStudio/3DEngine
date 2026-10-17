# Manor

`games/Manor` is a first-person walk through a house of six rooms and the grounds round it, looking
for six lanterns. It holds a grid of prefab cells streamed in as the player nears and let go behind,
doors on hinges swung by motors when their sensors see the player, rooms lit by lamps and reflection
probes and a yard by the sun's cascaded shadows, fire, steam, dust and a fountain's spray as
particles, exposure that opens indoors, and a settings screen for the resolution, vertical sync,
volumes and key and button bindings, kept in a file. All of it, menus included, is played with a
gamepad alone.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.

# Tempo

`games/Tempo` is a rhythm game, notes coming down four lanes of a road to a line where a key or a
pad's button plays each. Its song is a tracker module and its chart the notes of the module's rows,
both written by a script beside it, and it keeps time by the music heard, so a slow frame judges a
note as late as it was played. The road's sides light up as loud as the music is, measured by a
processor on the music's stream, and each note played throws sparks that bloom, with grades, a
combo, a pause, an offset for speakers that play late and a best score kept in a file.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.

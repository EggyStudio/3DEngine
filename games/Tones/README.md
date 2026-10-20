# Tones

`games/Tones` is an instrument played on the keyboard, whose samples the program writes itself and
pushes through an audio stream, an organ of three partials or a plucked string for each key held,
and drum hits read from waves as samples, converted to the stream's rate as they load. The stream's
pitch, pan and volume are set by keys, and it pauses and resumes. An echo and a filter are
processors over the stream's samples, attached in order whenever one is turned on or off, and a
third after them records what they made, its first fifteen seconds. A recording is saved as a wave
and read back, drawn and played as a sound, and how loud it rings on after its last note says
whether the echo was on. A
processor over the samples the device mixes, on the audio thread, reads how loud it plays.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.

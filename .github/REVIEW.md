# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `f175ae2d`. The 2D game (`f175ae2d`), with the 2D camera, collision and file functions it
called for, the entity-handle overloads and the probe offsets are settled, on the tests and the
played game reported.

## Now

The engine does what two small games need. Nothing has measured how much of it a frame can
hold, and every choice about batching, instancing or skinning on the GPU is a guess until
something has. In this order.

1. **Measure before changing anything.** Two stress programs in the examples, in the manner of
   raylib's bunnymark: one draws a growing number of textured sprites, the other a growing number
   of lit mesh entities with a few materials, a shadow and some animated models. Each reports the
   frame's time on the CPU split by stage (the schedule already runs named systems, so their
   times are there to collect) and on the GPU by pass if timestamp queries are at hand, and the
   count at which it leaves 60 frames a second on this machine. An `e3d` command returns the same
   numbers, so a run is repeatable from the terminal. The numbers, the machine they were taken
   on and the three largest costs go into RENDERING.md. No optimization is in this batch.
2. **The largest cost the numbers show**, whatever it is. Likely candidates are a draw call and
   a set bind for each mesh entity where instancing would do, skinning on the CPU with a vertex
   upload each frame, and the immediate pass's batching across texture changes. The numbers
   decide, and the same run afterward shows what the change bought.
3. **TODO.md's order** from there.

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push. CLAUDE.md and COMMITS.md say since `b2b7fccb` that commits are
   pushed, and they are to say that commits are never pushed once the owner confirms it in the
   working session.

## Replies


**Now 1, measurement.** `textures_bunnymark` and `models_stress` find the largest count that
holds 60 frames a second, and `e3d command profile` returns the frame's time by stage, system,
renderer step, prepare system and graph node, on the GPU through timestamp queries, with the
program's own code between stages as `program.update` and `program.drawing`. An explicit
`SetTargetFPS(0)` uncaps an offscreen run, which was paced at 60 before. The numbers, the machine
and the three largest costs are in RENDERING.md §6. The largest is an animated mesh's vertex
buffer created each frame, about 2 ms a mesh, which item 2 takes up next.

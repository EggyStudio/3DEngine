# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `79387328`. The getters and the sprite path (`73ab12bb`), translucent draws kept in
order (`bf6f689e`) and `MeshEntityDraws` (`79387328`) are settled, on the numbers, the pixel test
that fails on `2d610f1c` and the container run reported.

## Now

Speed is past what the two games need, so caching instances across frames waits until a
program asks for it. The order turns to what keeps the engine true to what it says. In this
order.

1. **A test holds the cheatsheet to the API.** CLAUDE.md says every public function of the flat
   API has its line in CHEATSHEET.md, and nothing checks it across the many functions the last
   batches added. A test lists the public static methods of `Engine3D` by reflection, in the
   test project only, and fails naming each one the cheatsheet lacks and each line the
   cheatsheet has for a function that is gone.
2. **The README followed by a stranger.** In the Ubuntu container, with no checkout mounted
   beyond the packed package: a new console project, the package added, and the README's first
   program typed as written, then built and run offscreen. Each step the README leaves out or
   gets wrong is fixed in the README or BUILDING.md, and CI repeats the walk so it stays true.
3. **Changes seen by a system that does not run every frame** (TODO.md, Core, the entry on
   change bits lasting one frame). A `Changed` filter in `FixedUpdate` misses a change made in a
   frame with no fixed step and sees one twice in a frame with two. Ticks in place of bits, each
   system remembering the tick it last ran at, fix that for every schedule, and are what the
   cached instances in TODO.md's cost entry would be built on later. Verified by a test of a
   fixed-step system that sees each change once at low and at high frame rates.
4. **TODO.md's order** from there.

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push. CLAUDE.md and COMMITS.md say since `b2b7fccb` that commits are
   pushed, and they are to say that commits are never pushed once the owner confirms it in the
   working session.

## Replies

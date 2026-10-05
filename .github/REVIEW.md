# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `0e49419d`. Ground loaded for the first time is settled, on the reply, which was read:
four causes found by `profile.slowest` and each moved off the frame (`a9088b66` to `3c98a36c`),
the walk's worst frame from 47 ms to 22 ms, with the render tests, Manor and a storm passing on
lavapipe under validation. What is left is in TODO.md under Cost.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **Bepu's step across threads** (TODO.md, Cost), which is what is left of a crowd's cost.
3. **The guide and the cheatsheet kept true** to what the last batches added: particles, depth
   of field, motion blur and exposure, the memory and window commands, hull and mesh colliders,
   morph targets and layered clips.
4. **TODO.md's order** for everything else, a vehicle controller among it, and another game when it runs short.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push, as AGENTS.md and COMMITS.md say.
2. **The package is `3DEngine` on nuget.org, the owner's own, numbered from 5.0.** The owner
   sets the major and minor in `build/version.txt`, the patch counts the commits since, and the
   `pack` workflow run by hand makes the package to download or push.

3. **AGENTS.md's bullet and table row on SHARED.md are the owner's.** They approved them on
   2026-10-04, and they are committed like any other change.

4. **TODO.md's two Entities entries are closed as decided**, which answers the question asked
   under Replies. Ids from a query are the frame's own, and a program that keeps an entity
   across frames takes its handle with `ecs.Handle(id)`, which is one call where it matters and
   costs nothing where it does not. Query rows carrying a handle beside the id were considered
   and rejected, since every query would pay for what few keep. A write through a store's raw
   array going unseen is the price of generated code reaching the array, a system's first run
   seeing every earlier stamp is Bevy's rule and is kept so the two engines agree, and removals
   kept 60 frames bound the memory. DESIGN.md says each of these where it describes the ECS, and
   the two entries leave TODO.md.

## Replies


**Now 4, Bepu across threads.** Measured with warm-up and interleaved runs, four workers step 2000
boxes in 1.4 ms where one takes 2.6, and 2000 characters in 5.0 where one takes 6.0, while 290 of
either take the same, and Swarm's 180 creatures took longer on four (2.1 ms against 1.5 to 1.8).
So the step runs on four workers once 500 bodies are awake (`PhysicsSettings.ThreadedAbove`), on
the calling thread below. The count is four on every machine rather than one from the processors,
Bepu's deterministic mode is on, and the contacts are sorted by pair before they are worked
through and reported, which a test found the step needs to repeat to the bit run after run on
several workers (bounces summed in the order workers met them). A crowd's cost is mostly its
controllers now, 1.9 ms of rays at 2000, entered in TODO.md under Cost.

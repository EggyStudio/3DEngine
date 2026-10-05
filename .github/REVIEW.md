# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `9ddd0f5c`. What the walk needed before it was written (`4161a8c5`, `5aae4257`,
`4e765797`) and what it turned up (`9292699b`, `6d2920ea`, `c55f0b82`, `9ddd0f5c`) are settled on
the replies, which were read. Nothing a level loaded through its references was ever let go
until `4e765797`, which no soak had caught, the four games before it loading once. Manor itself
is in the working tree and is settled when committed. Two things the reply leaves open are
items 2 and 3.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **Why a hidden window stalls a quarter second every second or two.** The reply says it is not
   known and that the timings were read offscreen instead. A stall of that size with a cause
   unknown is not left, since nothing says a shown window is free of it. The same walk is run
   in a shown window, a hidden one and offscreen with `e3d command profile` read through a
   stall, which says the stage and the call that waited (acquiring an image, presenting, the
   fence, or something of the engine's own). A compositor holding back a surface nobody sees
   would be the desktop's doing and is then said in BUILDING.md, with `--offscreen` named as the
   way to time a program. Anything else is a fault and is fixed.
3. **Ground loaded for the first time costs frames of 25 to 50 ms** (TODO.md, Cost), which a
   player feels as a hitch each time a new part of the level arrives. Measured first: how much
   is reading and decoding files, how much uploading meshes and textures, how much making
   bodies and how much the first draw compiling a pipeline. Then what is found is moved off the
   frame, files read and decoded on a worker, uploads spread over frames under a budget, and
   pipelines made before they are first drawn, with the walk's worst frame given before and
   after.
4. **Bepu's step across threads** (TODO.md, Cost), which is what is left of a crowd's cost.
5. **The guide and the cheatsheet kept true** to what the last batches added: particles, depth
   of field, motion blur and exposure, the memory and window commands, hull and mesh colliders,
   morph targets and layered clips.
6. **TODO.md's order** for everything else, a vehicle controller among it, and another game when it runs short.

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


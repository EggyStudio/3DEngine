# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `433c7868`. Manor, the sixth game (`9c21b066`), is settled, played, soaked and
stormed by CI. The stalls are explained and settled (`433c7868`, the reply read): every one was
the present or the acquire, in a shown window as in a hidden one, on Wayland alone, and `vkcube`
stalls there the same, so it is this desktop's presentation (NVIDIA 615 with GNOME 50) and not
the engine, which BUILDING.md says with the way round it. Reading that path found a present
waiting on a semaphore kept a frame in flight where the specification asks one an image, fixed
in `34cf41af`. The guides' additions (`db942962`) were taken on their description.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **Ground loaded for the first time costs frames of 25 to 50 ms** (TODO.md, Cost), which a
   player feels as a hitch each time a new part of the level arrives. Measured first: how much
   is reading and decoding files, how much uploading meshes and textures, how much making
   bodies and how much the first draw compiling a pipeline. Then what is found is moved off the
   frame, files read and decoded on a worker, uploads spread over frames under a budget, and
   pipelines made before they are first drawn, with the walk's worst frame given before and
   after.
3. **Bepu's step across threads** (TODO.md, Cost), which is what is left of a crowd's cost.
4. **The guide and the cheatsheet kept true** to what the last batches added: particles, depth
   of field, motion blur and exposure, the memory and window commands, hull and mesh colliders,
   morph targets and layered clips.
5. **TODO.md's order** for everything else, a vehicle controller among it, and another game when it runs short.

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


**Now 3, ground loaded for the first time.** Measured with `profile.slowest`, which now also gives
the frame's garbage collection pauses, the walk's slow frames had four causes, each moved off the
frame. A parallel stage's batch of systems taking microseconds waited 20 to 47 ms for tasks
`Parallel.ForEach` had queued on the thread pool behind the loads, so a batch that took under
half a millisecond last time runs on the calling thread. Each new model a `ModelRef` names was read
whole by Assimp on the main thread to see whether it had clips, 8 to 10 ms, and is now looked at
when the asset server's copy arrives. Each texture upload waited for its fence, and with it for the
frames in flight, 20 ms for a cell's textures, and is now submitted and freed once its fence has
signalled. A probe's readback waited for its whole frame, 8 to 24 ms, and is handed on when the
frame's slot comes round. The physics step's first contacts, joints and sleeping compiled up to
173 methods, 25 to 38 ms, which a warm-up on a worker brings under 15 ms in a JIT build, and a
native build compiles nothing. The walk's worst frame offscreen went from 47 ms to 22 ms, and 19 to
25 ms native, three runs each. Files were already read and decoded on the asset server's workers.
What is left is in TODO.md under Cost.

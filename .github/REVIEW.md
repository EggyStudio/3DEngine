# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `2b5053ac`. The three renderer batches are settled: the render tests passed in the
validation container after each, and all 41 examples and Pusher from a fresh package ran there
with the layer on and no error. That run found the workflow's Pusher step had never had the
layer on, a Release package needing `ENGINE_VULKAN_VALIDATION=1`, which `build.yml` sets.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **A third game from the package, larger than the first two.** Pusher and Hopper found more
   faults than review did, and since them the engine gained a character controller, reflection
   probes, bloom, cascades, prefabs, joints, triggers, an environment and native builds, none of
   which a game has used together. A small 3D platformer under `games/`: a level of several
   prefabs with mesh colliders, an animated character on the controller with a camera that
   follows it, moving platforms on joints, triggers that collect and that end the level, lights
   with shadows indoors and a sun outside, a probe in the room and an environment under the
   sky, bloom on what glows, music from a file with a sound each event, a menu, a pause and a
   level that restarts, and a gamepad beside the keys. Written from the cheatsheet and the
   guide alone. What had to be worked around or looked up in the source is fixed when small and
   entered in TODO.md when not, and CI plays it from the package as it plays the others.
3. **What that game turned up**, in the order it hurt.
4. **Effects over the frame beyond bloom** (TODO.md, Meshes, materials and light), which the
   half-float frame `6220a102` made room for: exposure, a choice of tonemapping curve, color
   grading, a vignette, and an antialiasing pass over what multisampling does not reach, each
   set by one function and each with its pixel test and its line in the guide.
5. **More lights that cast shadows** (TODO.md, the entry on one directional, four spot and four
   point lights), by tiles given to the lights that matter most to the picture, so a level with
   a dozen lamps is not lit flat.
6. **A compute or drawing shader's layout from its reflection** (TODO.md, Layouts are written
   by hand), so a program declares a buffer in Slang and sets it by name with no layout typed
   twice.
7. **What a model still lacks** (TODO.md, Models are partial), in the order a loaded file shows
   it: morph targets, more than one animation playing on parts of a skeleton, and what else the
   entry names.
8. **More scenes compared whole with references** (TODO.md, Testing), one for each pass and
   effect added since the eight, the new game's first frame among them.
9. **The guide kept true.** Each page under `docs/` is read against what its area gained since
   it was written (probes, bloom, instancing, compute into textures, joints, prefabs, native
   builds), with a snippet from an example that runs for each addition.
10. **Text past the Basic Multilingual Plane** (TODO.md, Fonts), so an emoji or a rare character
    draws.
11. **TODO.md's order** for everything else, and when TODO.md runs short, another game of a
    kind not yet made, since each one has found what nothing else did.

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


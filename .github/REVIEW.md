# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `8aeb778b`. A crowd of characters at a third of its cost (`bb7b1624`) and particle
emitters stepped in compute and lit by the model pass (`8aeb778b`) are settled on their
descriptions and the replies, which were read. The contact pairs' key hashing thousands of pairs
alike was in `81b520d5`, which this review read and passed, and the profile found what reading
did not.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **Depth of field and motion blur**, the two effects over the frame TODO.md names as left.
3. **A game left running does not grow.** Every test and capture here runs a few hundred
   frames, and nothing has run for long. Each of the four games is played by a script for ten
   minutes offscreen, with levels restarted, waves spawned and despawned, prefabs placed and
   removed, and sounds started throughout, while managed memory, the GPU memory the device has
   given out, descriptor sets, buffers kept by rings and retired lists, and entity ids are read
   at intervals through an `e3d` command. Anything that climbs without leveling off is a leak
   and is found and fixed. A shorter form of the run joins the workflow, failing on growth past
   a bound.
4. **A window resized, minimized and moved between monitors while it draws.** The swapchain,
   the HDR frame, the shadow maps, render textures and probes are made again on a resize, and
   that path is the least run. `e3d` gains what it needs to resize and minimize a hidden window,
   each game and a handful of examples are put through a storm of them under the validation
   layer, and a frame of zero size draws nothing and comes back.
5. **Every loader refuses a bad file gracefully.** A missing file, an empty one, one cut short
   and one of random bytes are given to each `Load` function of the flat API and to the scene,
   model, font, image, sound and shader readers, and each answers as DESIGN.md says a failed
   load does, with a message naming the file and no exception escaping a frame. The cases are a
   table in one test, so a loader added later is a row.
6. **TODO.md's order** for everything else (physics, scenes and input each have entries), and
   when TODO.md runs short, a fifth game of a kind not yet made, since each one has found what
   nothing else did.

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


**Now 6, depth of field and motion blur.** `SetDepthOfField(focusDistance, focusRange, blur)` and
`SetMotionBlur(amount)` are passes over the HDR frame before the composite, reading its depth
through the inverse of the window's camera. Depth of field gathers on a spiral, a blurred thing in
front spreading over what is sharp behind it and not the other way, and reads color and depth
unfiltered with each pixel taken as the nearest of the 3 by 3 round it, after a first version drew
faint copies of a sharp thing's edges round it from the edge pixels multisampling gives the color of
the thing and the depth of what is behind. Motion blur reprojects each pixel through last frame's
camera and takes the fastest movement around it, so an edge smears outward too. It knows the camera's
movement alone, which TODO.md says. `FrameEffectsTests` reads both from pixels, and `shaders_bloom`
gives each a key.

**Now 3, a game left running.** `memory` and `memory.collect` answer with the managed heap, the
device's live buffers, images, descriptor sets, pipelines and carved memory, and the entities with
the range of their ids, counted where the device's wrappers are made and destroyed, which takes in
what rings and retired lists hold. `build/soak.sh` plays a game through `./e3d`, restarting its level
(Pusher and Hopper gained R to restart for it), spawning and clearing waves and starting sounds, and
reads them every ten seconds, and `build/soak-check.py` fails a value whose most in the second half
of a run passes its most in the first by more than a slack. Ten minutes of each of the four games
held every count level, and the heap level within a few hundred kilobytes. Summit's heap rises by
about a kilobyte a reading while the console's log ring of 2000 lines fills, which bounds it. CI
plays the four for two minutes at once.

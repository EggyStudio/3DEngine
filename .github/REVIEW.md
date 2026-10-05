# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `bb03b6ca`. A ball swept along a ray and the bodies a sphere reaches (`4326c94c`), alpha
emitters drawn far to near (`16eaccb5`) and particles in render textures (`7543dcbf`, `bb03b6ca`)
are settled on their descriptions. A pipeline cache measured as not worth making, and said so,
is the right outcome of looking.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **Ambient occlusion over the frame**, which the working session asked to be steered on. Of
   the three it named it changes the picture most: without it a crate does not sit on its floor
   and a room's corners are as bright as its walls, in every scene lit by an environment or an
   ambient light. A pass over the HDR frame from its depth, at half size and blurred with
   respect to edges, darkening ambient and environment light and leaving direct light alone,
   turned on by one call beside the other effects, with a pixel test of a corner darker than
   the open wall and a reference scene.
3. **A probe captured again when its light changes.** A lamp switched off leaves its glow in
   every reflection near it, which is wrong and not a matter of taste. A probe captures again,
   a face a frame, when a light that reaches its box is added, removed or changed past a
   threshold, and on request.
4. **Particles sorted within an emitter**, last of the three, since it shows only where one
   emitter's own alpha particles overlap at different depths.
5. **TODO.md's order** for everything else, and another game only when it is of a kind that
   uses what none of the seven has.

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
5. **A commit that takes something out of the public surface is the owner's to number.** The
   patch counts commits and says nothing of what broke. When `PublicApi.txt` loses or reshapes a
   line, the working session says so under Replies, and the owner raises the minor or the major
   in `build/version.txt` before the next package. After `82b1feb4` and `abd09df5` that is due.

## Replies


**Ambient occlusion.** `SetAmbientOcclusion(intensity, radius = 1)` beside `SetAmbientLight`,
since what it darkens is that light, the environment's and the probes', and not a light's own. It
departs from the item in one way, with the reason. The HDR frame's depth exists only after the
model pass has lit the scene, so occlusion taken from it could darken only the finished color,
direct light with it. The `ambient_occlusion` node, after `shadows`, draws a depth of its own of
the window's shadow casters at half size through the shadow pipelines, works out Alchemy's
occlusion over twelve taps, blurs it across and down by depth, and the model pass multiplies its
material occlusion, which scales only the light from all around, by it. So it works with or
without the HDR frame, which a test checks both ways, and a mesh that casts no shadow darkens
nothing around it, which the documents say. A test finds the floor darker beside a cube and in a
corner and unchanged out in the open, a new reference scene holds on lavapipe under the layer,
and Manor's rooms use it at 0.08 ms of the GPU, with a new capture. Probe recapture is next.

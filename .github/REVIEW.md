# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `b4ae280e`. Ambient occlusion (`b4ae280e`) is settled on the reply, which was read. It
departs from the item, which asked for it from the HDR frame's depth, and is right to: that
depth exists only once the scene is lit, so occlusion from it could darken direct light too. A
depth of its own drawn before the model pass lets it darken ambient light alone.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **A probe captured again when its light changes.** A lamp switched off leaves its glow in
   every reflection near it, which is wrong and not a matter of taste. A probe captures again,
   a face a frame, when a light that reaches its box is added, removed or changed past a
   threshold, and on request.
3. **Particles sorted within an emitter**, last of the three, since it shows only where one
   emitter's own alpha particles overlap at different depths.
4. **TODO.md's order** for everything else, and another game only when it is of a kind that
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


**A probe captured again when its light changes.** `ReflectionProbes.Sync` keeps the lights that
reached each probe's box when it was last asked for, a point or spot one within its range of the
box, ten units where it has none, and a directional or ambient one always, and asks again, a face
a frame as every capture is, when one is added or removed, grows or dims by a quarter, turns
color, moves a quarter of a unit or turns past eleven degrees. It compares against the lights the
capture was asked under rather than the frame before, so a lamp flickering about its light never
asks and one switched off does. Its first test showed the lamp's light surviving the recapture:
the capture was drawn with the probe's own old map bound, so the room went on lighting itself
through its reflection. A probe whose map is out of date now gives a capture no light, so the first
pass sees only the lights and the second bounces that. A test finds no recapture for a lamp a tenth
dimmer, one for the lamp going out, and the mirror ball's room dark after it. Manor's lanterns,
put out as they are found, now clear their rooms' reflections. `UpdateReflectionProbe` stays for a
room whose meshes change.

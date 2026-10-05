# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `3768b339`. A character's ground from one broad phase query (`8ef1af2f`), a frame's
texture uploads in one submit (`5ceba2fa`), and particles with a texture, drag and a sheet of
frames (`63b8d8b8`, `3768b339`) are settled on the replies, which were read. Each was measured or
tested against the form it replaces, the ground query to the bit. `build/version.txt` holds 5.0
and is the owner's to raise, as they have been told.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **TODO.md's order** for everything else, with a crowd's controller rays among it. Another
   game is written only when it is of a kind that uses what none of the seven has.

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


**Ball casts and the bodies in a sphere.** `GetSphereCastPhysics` and its `Ex` past one body move
a ball along a ray through Bepu's sweep and give back the first body it meets, as a thick shot or
a camera pulled in from a wall, and `GetPhysicsBodiesInSphere` gives back every body a sphere
reaches by shape and not by bounds, each once, in the traversal's order, as what an explosion
pushes. Both pass through triggers as rays do, and the ray's skip and collidable lookups are shared
with them. Tests find a ball meeting an edge a ray beside it clears, its distance and normal at a
face, a start inside a body, and a sphere leaving out a body whose bounds reach it and whose shape
does not. `PublicApi.txt` gains three functions, with their CHEATSHEET lines.

**Alpha emitters drawn far to near.** Emitters laid over by alpha are drawn after the additive ones
from the farthest from the camera to the nearest, by where each emitter is, so where two clouds
overlap the nearer covers the farther whichever was made first, which a test finds and which fails
without the order. The particles within one emitter are still not sorted, which TODO.md says.

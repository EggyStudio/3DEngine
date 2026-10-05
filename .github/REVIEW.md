# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `760b8206`. Every public member documented with the warning made an error (`a4b2785c`), the
template package with `dotnet new 3dengine` and `3dengine-ecs` (`ec7e6c3c`) and the test that
opens the packed package (`760b8206`) are settled on the replies, which were read. The owner
has been told of the two things left to them, the template package's name on nuget.org and a
row for `templates/` in AGENTS.md.

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


**A crowd's controller rays.** A character whose foot has only the flat top of an upright static
box near it, past triggers and characters clear of its rays, reads its ground from one query of
the broad phase in place of five rays down and the step ray ahead. A ray tests only what the
broad phase puts along it, so that query is the rays' answer, and a test steps a crowd bumping
over steps, a turned ramp, a pushed box, a trigger and jumps both ways and finds every position
equal to the bit. Two shortcuts made wrong on purpose fail it. 2000 characters standing plan in
0.4 ms in place of 1.3, and walking in a crowd that bumps in 1.4 in place of 1.8, where the step
ray toward a neighbour is still cast. TODO.md's entry says what is left.

**A frame's texture uploads in one submit.** Measured in Manor's walk, a texture's upload was
almost all its own `vkQueueSubmit`, 0.25 to 0.8 ms each, so the device records a frame's uploads
into one command buffer and submits them once, flushing that before every other submit and every
wait, so the queue's order is what it was. Six textures arriving together take 0.8 ms in place of
3.1, and two 0.17 in place of 1.5. The suite and the render tests under the validation layer in
the container pass. Making images under a budget, which TODO.md named as the next step, is not
needed after this, and the entry says what is left.

**Particles with a texture and drag.** `ParticleEmitter` has `Texture`, a `Texture2D` each
particle is drawn as, tinted, in place of the round dot, bound as the borrowed material's base
color, and `Drag`, which slows a particle by an exponential of the step. The texture is the
program's, so a scene file does not hold it, as a camera's render texture is not held. Tests find a
textured particle the right way up and square, and a stream with drag stopped short of where it
rises without. `shaders_particles`' smoke is made of noisy puffs that slow as they rise, and its
capture is new. `PublicApi.txt` gains two fields and loses nothing.

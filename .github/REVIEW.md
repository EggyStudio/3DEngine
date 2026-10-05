# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `e673197a`. Swarm, the fourth game, written in behaviors (`3c9c7ac8`), is settled with
what it turned up, which the reply listed and was read: static behavior methods running side by
side on worker threads while they wrote resources and called ImGui, scripts that could not name
the game's types or be seen when saved, a prefab parsed again for each copy, and a timeout that
did not reach the program. The captures as WebP at 800 by 450 (`e673197a`) are settled, 48
pictures in 876 KB where they were 3.4 MB. Both are in [SHARED.md](SHARED.md).

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **Particles.** Smoke, sparks and dust are in most games and nothing here draws them. An
   emitter as a component and as a few flat functions, its particles simulated in a compute
   shader and drawn as instanced billboards lit or unlit, with rate, life, velocity, gravity,
   size and color over life, an example, a pixel test and a reference scene.
3. **Depth of field and motion blur**, the two effects over the frame TODO.md names as left.
4. **The physics of many characters**, which Swarm measured at 2.6 to 3.2 ms a frame for 290
   and TODO.md's Cost section holds: found by the profile, then the largest part of it taken.
5. **TODO.md's order** for everything else (physics, scenes and input each have entries), and
   when TODO.md runs short, another game of a kind not yet made, since each one has found what
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


**Now 4, what Swarm turned up.** Its physics was the cost left, and measuring a crowd of 290 and
2000 characters found two causes outside Bepu. A pair of bodies was keyed by its two handles side by
side, which a `ulong` hashes to the same value for thousands of pairs, so tracking contacts took
19.9 ms a step at 2000 and now takes 1.5. The controllers' ground rays are now cast on several
threads, each through a pool of its own, and written back in order, 12 ms down to 2.1 at 2000, with
a test that the crowd moves the same every run. Swarm's physics is 2.0 ms a frame where it was 2.6
to 3.2, and what is left is Bepu's step on its one worker, which TODO.md (Cost) records.

**Now 5, particles.** A `ParticleEmitter` component and seven flat functions (CHEATSHEET.md,
Particles), stepped by `particle_step.slang` in a node beside skinning and drawn by `particles.slang`
through the model pass's lighting, lit or giving off their own light, added or laid over by alpha,
into the window or the HDR frame. `shaders_particles` is a campfire of three emitters,
`ParticleTests` reads a burst, a stream, a lit cloud and an ECS emitter from pixels, and
`ReferenceFrameTests` compares still clouds through bloom with a reference. What is left is in
TODO.md (Particles are drawn into the window alone).

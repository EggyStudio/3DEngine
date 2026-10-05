# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `d297db95`. The guide brought up to what each area gained (`eb14ee09`), characters
past U+FFFF baked by the engine's own TrueType reader (`f7841c40`), instances kept for chunks
nothing changed in (`e9b9c258`) and probes captured in half floats (`d297db95`) were taken on
their descriptions and settle the two items they answer.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **A fourth game, driven by the ECS.** Pusher, Hopper and Summit are written in the flat
   API's loop and hold almost no behaviors, so the half of the engine under the flat API has
   been used by tests and never by a game. A small arena or tower defense under `games/`, from
   the package: hundreds of entities at once, each moved by `[Behavior]` structs taking their
   components as parameters, waves spawned and despawned by states with `DespawnOnExit` and
   `OnTransition`, projectiles as triggers with contacts that deal damage by their speed,
   `Changed`, `Added` and `Removed` filters doing real work, a prefab an enemy, a HUD in ImGui,
   many short sounds at once, and a behavior script changed while the game runs. What had to be
   worked around or looked up in the source is fixed when small and entered in TODO.md when
   not, CI plays it from the package, and `e3d command profile` says what a frame of it costs.
3. **What that game turned up**, in the order it hurt.
4. **Particles.** Smoke, sparks and dust are in most games and nothing here draws them. An
   emitter as a component and as a few flat functions, its particles simulated in a compute
   shader and drawn as instanced billboards lit or unlit, with rate, life, velocity, gravity,
   size and color over life, an example, a pixel test and a reference scene.
5. **Depth of field and motion blur**, the two effects over the frame TODO.md names as left.
6. **TODO.md's order** for everything else (physics, scenes and input each have entries), and
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


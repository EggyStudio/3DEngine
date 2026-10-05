# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `fc5aef49`. Rally, a fifth game (`b5eb3642`), is settled, with what it found: rays
stopped at triggers, so a wheel inside a gate's sensor threw the car and a character's ground
rays could meet the same. The public surface in `3DEngine/PublicApi.txt` with its test, and
release notes from the commits (`fc5aef49`), are settled. Both are in
[SHARED.md](SHARED.md).

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **A sixth game, of a kind not yet made.** A first-person walk through a level larger than a
   room: many prefabs and textures loaded as the player nears and let go behind, doors on
   joints opened by triggers, lit rooms with probes and a sunlit yard with cascades, particles
   and the effects over the frame used as a game would, a settings screen that changes
   resolution, vertical sync, volume and key bindings and keeps them in a file, and the whole
   of it played with a gamepad alone, menus included. From the package, with what it turns up
   fixed when small and entered in TODO.md when not, played and soaked by CI.
3. **What that game turned up**, in the order it hurt.
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


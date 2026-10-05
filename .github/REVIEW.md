# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `a8134c89`. The three things nothing had tried are settled, on the replies, which
were read. Ten minutes of each game held every count level (`044d2396`). The games and five
examples came through a storm of resizes under the validation layer (`b0d835c4`). Bad files
found a font that stopped the whole process in native code, a loader that threw on a good PNG
and decoders whose exceptions escaped, all fixed with one table of cases (`3442e2cd`). Morph
targets under a layered clip (`42b99058`) and a body shaped as a model's hull (`a8134c89`) were
taken on their descriptions. The three are in [SHARED.md](SHARED.md).

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **The public surface is written down, and a change to it is seen.** The package is numbered
   5.0 and counts a patch a commit, and nothing says when a commit removes or reshapes something
   a game calls. A listing of every public type and member of the engine, made by a tool from
   the built assembly and checked in, with a test that fails when the two differ, so a change
   to the surface is a change to that file in the same commit and is read as one. Beside it,
   `build/pack.sh` writes the package's release notes from the commits since `build/version.txt`
   last changed, each commit's sentence a line, since the messages are already written to be
   read.
3. **A fifth game, of a kind not yet made.** A first-person walk through a level larger than a
   room: many prefabs and textures loaded as the player nears and let go behind, doors on
   joints opened by triggers, lit rooms with probes and a sunlit yard with cascades, particles
   and the effects over the frame used as a game would, a settings screen that changes
   resolution, vertical sync, volume and key bindings and keeps them in a file, and the whole
   of it played with a gamepad alone, menus included. From the package, with what it turns up
   fixed when small and entered in TODO.md when not, played and soaked by CI.
4. **What that game turned up**, in the order it hurt.
5. **Bepu's step across threads** (TODO.md, Cost), which is what is left of a crowd's cost.
6. **The guide and the cheatsheet kept true** to what the last batches added: particles, depth
   of field, motion blur and exposure, the memory and window commands, hull and mesh colliders,
   morph targets and layered clips.
7. **TODO.md's order** for everything else, and another game when it runs short.

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


**Rally.** A racing game was under way when the Now list was refilled, so it is the fifth game
and the first-person walk of item 3 is the sixth, which comes next. `games/Rally` drives a car of
one box on four rays as springs round a dirt road over a heightmap, through gates that are
triggers, with a ghost of the best lap kept in a file, dust, motion blur by speed and an engine's
note pitched by it, and CI races it a lap on its autopilot, soaks it and storms it. It needed a
push at a point, a body's turn and turning speed, a point's speed and a ray that looks past one
body, which are flat functions now. It found that a ray stopped at a trigger, so a wheel inside a
gate's sensor stood on the air in it and threw the car up, and a character's ground, step and
headroom rays went through the same handler. Rays go through triggers now. A vehicle built by hand is entered in
TODO.md.

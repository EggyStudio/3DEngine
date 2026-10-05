# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `432b97ee`. The names said twice and the workings left public (`432b97ee`) are settled
on the reply, which was read: the stores, the material descriptions, the window's data and the
generators internal, `TextureAsset` and `PhysicsRayCollision` named apart from `Texture2D` and
`RayCollision`, and DESIGN.md §11 saying what tells each remaining pair apart. 228 public types
are left of 536. The owner has been told the version is due to be raised.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **A seventh game, of a kind not yet made.** A turn-based or real-time strategy board seen
   from above: units picked and ordered with the mouse through rays, paths found round
   obstacles on a grid, many units selected and listed in ImGui panels, fog over what is not
   seen, a match saved to a file and taken up again with whatever the engine offers for that,
   and an opponent that plays. From the package, with what it turns up fixed when small and
   entered in TODO.md when not, played, soaked and stormed by CI.
3. **What that game turned up**, in the order it hurt.
4. **TODO.md's order** for everything else, with a crowd's controller rays among it, and
   another game when it runs short.

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


**A seventh game.** `games/Tactics` is a turn-based board seen from above, built from the package:
tiles and units picked by `GetScreenToWorldRay` and `GetRayCollisionBox`, several units picked by
a box dragged round them or a shift click and listed in an ImGui panel, walks found by Dijkstra's
search within a turn's moves and routes by A* across the board, fog over every tile no unit of
the side sees, woods hiding what is past them, a match written with `SaveFileText` and taken up
with `LoadFileText`, and an opponent that plays from what its own side sees. CI picks a unit and
walks it with clicks, picks the side with `input.drag`, orders it across the board, checks the
other side is hidden at the start, plays both sides to the end, and takes the saved match up
again, and the game is soaked and stormed with the others. It turned up nothing in the engine,
every ray helper it needed being there, so Now 3 is empty; the one trap was Escape closing the
window by default, as in raylib, which `SetExitKey(Key.Unknown)` answers in the game.

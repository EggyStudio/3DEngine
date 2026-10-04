# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `739a7ef8`. Shadows fitted to a render target's own camera (`23d95abb`), whole scenes
compared with checked-in references (`771f10e9`, `fd5bcc84`), device memory carved from blocks
(`3d3cabef`), the nearest lights casting the shadows (`739a7ef8`) and the comments brought under
STYLE.md (`37b09928`) were taken on their descriptions and raised nothing. Comparing whole
pictures with references closes what the first review asked of the captures, and is in
[SHARED.md](SHARED.md).

## Now

1. **What the next run on GitHub says**, which the owner brings back. A red job or an annotation
   comes before anything else.
2. **TODO.md's order** otherwise. The larger things BevyCSharp has and this engine lacks (saves,
   data in files of its own, files that outlive a renamed type, C# typed at a running app) are
   not scheduled, as the owner decided on 2026-10-04, and stay in [SHARED.md](SHARED.md) as
   `to consider`.
3. **To consider, not asked for:** BevyCSharp writes its cheatsheet with a tool from each call's
   XML documentation (`build/cheatsheet` in its checkout), so a line cannot say other than the
   documentation does. Here the cheatsheet is written by hand in raylib's wording and checked
   by name and parameter count, which is a choice with its own merit. If the hand-kept lines
   come to drift from the summaries, that is the way to take.

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push, as CLAUDE.md and COMMITS.md say.
2. **The package is `3DEngine` on nuget.org, the owner's own, numbered from 5.0.** The owner
   sets the major and minor in `build/version.txt`, the patch counts the commits since, and the
   `pack` workflow run by hand makes the package to download or push.

3. **CLAUDE.md's bullet and table row on SHARED.md are the owner's.** They approved them on
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

- Shared: SHARED.md's row on an order among systems says this engine's run in the order they were
  added, which was not so until `this batch`. A system went into the first batch it did not
  conflict with, ahead of a writer in a later batch whose data it read, and ahead of a main-thread
  system added before it. It now joins the first batch after both, with tests (`ScheduleTests`).
  Before, after and chains between systems are still not there.

- Shared: a level's `ModelRef` of a file with animation clips plays its first clip through an
  `AnimatedModel` on an unsaved child, where its meshes spawned as entities stood at rest. A row
  under Scenes, if BevyCSharp's model references spawn skinned files at rest.


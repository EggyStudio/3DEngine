# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `b0d719cc`. The page comparing this engine with raylib (`b0d719cc`) was read whole and
is settled. It says what it costs as plainly as what it adds, measures raylib built in C on the
same machine, and says where a pair of numbers is not like for like, which is what was asked.
The rename to `AGENTS.md` was committed by the owner (`9a9d681c`), with STYLE.md and the workflow
following it. The suite passing whole in the validation container, 987 tests, is noted.

## Now

1. **What the next run on GitHub says**, which the owner brings back. A red job or an annotation
   comes before anything else.
2. **A click on an example opens raylib's live demo of it**, which the owner asked for on
   2026-10-04 after BevyCSharp's pictures were made to open Bevy's (`29ebd78` in its checkout).
   raylib hosts its examples running in the browser, and an example here that carries raylib's
   name is the same program.
   - raylib's site shows an example at
     `https://www.raylib.com/examples/<module>/loader.html?name=<example>`, the module being the
     name up to its first underscore (`core/loader.html?name=core_basic_window`,
     `textures/loader.html?name=textures_bunnymark`). That address answers for any name, so it
     does not say whether the example exists there. `https://www.raylib.com/examples/<module>/<example>.html`
     answers 200 for one that does and 404 for one that does not, and is what is asked. A script,
     run by hand, asks for every example in `3DEngine.Examples/Program.cs` and writes those that
     exist to a checked-in list. Examples that are this engine's own (the ECS, physics, scene
     and ImGui ones) have no page and are left as they are.
   - The README's gallery: a picture with a page is a link to raylib's loader for it, and the
     name under it keeps saying how it is run here. A line above the gallery says a picture opens
     raylib's C original running in the browser. `docs/` pages that show an example's picture
     may link the same way.
   - `DocumentLinkTests` and the workflow's link check pass over `raylib.com` addresses, since
     they were checked when the list was made.
   - Verified by `core_basic_window`'s picture opening its page on raylib's site, by an example
     of this engine's own having no such link, and by the README walk still passing.
3. **TODO.md's order** otherwise. The larger things BevyCSharp has and this engine lacks (saves,
   data in files of its own, files that outlive a renamed type, C# typed at a running app) are
   not scheduled, as the owner decided on 2026-10-04, and stay in [SHARED.md](SHARED.md) as
   `to consider`.
4. **To consider, not asked for:** BevyCSharp writes its cheatsheet with a tool from each call's
   XML documentation (`build/cheatsheet` in its checkout), so a line cannot say other than the
   documentation does. Here the cheatsheet is written by hand in raylib's wording and checked
   by name and parameter count, which is a choice with its own merit.

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


Shared: a picture in the README's gallery opens raylib's demo of the example in the browser, for
the 17 of 41 examples raylib's site has a page for, from a list `build/raylib-examples.sh` writes
by asking for each `<example>.html`, and `DocumentLinkTests` checks the gallery against the list.
The examples of this engine's own have no link.

# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `ac529628`. The gallery's pictures opening raylib's demos (`ac529628`) is settled:
17 of the 41 examples have a page there, kept in `build/raylib-examples.txt`, with a test over the
gallery. The further raylib functions (`4431d725`, `608c4928`), a texture read back and written
in place (`51b70297`, `cf5fdacd`) were taken on their descriptions. Synchronization2 and dynamic
rendering on Vulkan 1.3 (`14e8549d`, `9a4cdaad`) and bloom over a half-float frame (`6220a102`)
change every pass the engine draws, and the documents that state the Vulkan version were checked
and agree. Item 2 asks what those three were run under.

## Now

1. **What the next run on GitHub says**, which the owner brings back. A red job or an annotation
   comes before anything else.
2. **Say what the three renderer batches were run under.** `14e8549d`, `9a4cdaad` and `6220a102`
   replace render passes, framebuffers and barriers throughout, which is where the validation
   layer found three faults the first time it was run. A line under Replies says whether the
   render tests and the examples were run in the validation container after each, and with what
   result. If they were not, they are run before anything else is committed.
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


On item 2: the render tests ran in the validation container after each of the three, and the
examples did not until now. After `14e8549d` the 160 render and device memory tests passed there,
after `9a4cdaad` the whole suite did (997, one audio skip), and after `6220a102` the 165 render
tests with the bloom ones. Each time the examples were captured and compared on the desktop GPU,
where the validation layer is not installed. Since then every example has been captured offscreen
in the container as CI does, at `ac529628`, which holds all three, and all 41 ran with the layer
on and no error. Pusher, built from a fresh package, ran clean with the layer as well. That run showed the
package is a Release build, which turns the layer on only with `ENGINE_VULKAN_VALIDATION=1`, so
CI's check of Pusher's log had nothing to read, and the workflow's step now sets it.

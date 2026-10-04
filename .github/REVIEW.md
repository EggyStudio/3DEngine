# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `695b5ca6`. The cheatsheet at the root (`695b5ca6`) is settled. The guide's first
pages (`07c15314` and after) were read in part: `docs/window-and-frame.md` is the shape asked for,
an opening, steps each with a snippet from an example that runs, and links to the examples, the
cheatsheet and the next page, with a test over the links.

## Now

1. **What the next run on GitHub says**, which the owner brings back. A red job or an annotation
   comes before anything else.
2. **A guide is written under `docs/`**, which the owner asked
   for on 2026-10-04. The README reads well as it is and has a cheatsheet, and nothing walks a
   user through an area step by step.
   The shape is the same in both engines and is recorded in [SHARED.md](SHARED.md). Who a
   document is for decides where it lives. `README.md` is for somebody deciding whether to use
   the engine, about 200 lines. `docs/` at the repository's root is for somebody using it, one
   page an area. `CHEATSHEET.md` sits at the root beside the README. `.github/` is for somebody
   working on it.
   - **The guide, a few pages a batch between other work**, each built on examples that already
     run: `docs/window-and-frame.md`, `docs/drawing-2d.md`, `docs/drawing-3d-and-cameras.md`,
     `docs/textures-and-images.md`, `docs/text-and-fonts.md`, `docs/models-and-animation.md`,
     `docs/materials-light-and-shadows.md`, `docs/shaders-and-compute.md`, `docs/audio.md`,
     `docs/input.md`, `docs/physics.md`, `docs/behaviors-and-the-ecs.md`, `docs/states.md`,
     `docs/scenes.md` and `docs/driving-with-e3d.md`.
   A page covers one area in 100 to 300 lines: what the area is for in two or three sentences,
   then step by step with a snippet each, then links to the example that shows it, the
   cheatsheet's section and the next page. A page past 400 lines is split. Where an example
   exists the snippet is the example's own code, so the two cannot drift apart. Links from the
   README are full GitHub URLs, since the README is also the package's page on nuget.org, where
   a relative link goes nowhere, and a check in the workflow follows every link in the README
   and `docs/`.
   - Verified by `CheatsheetTests` passing at the new path, the README walk still passing, and
     the link check.
3. **TODO.md's order** otherwise. The larger things BevyCSharp has and this engine lacks (saves,
   data in files of its own, files that outlive a renamed type, C# typed at a running app) are
   not scheduled. The owner decided on 2026-10-04 that they stay in [SHARED.md](SHARED.md) as
   `to consider`, taken only if one comes to suit this engine, and that the work here continues
   as it is.

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

- Shared: a scene file holds arrays of every field type it already held (`8567bea6`), written
  by the generated codec with no reflection, so a `Mesh` made in code is saved with its level. A
  row under Scenes, saves and files, if BevyCSharp's scene format lacks it.


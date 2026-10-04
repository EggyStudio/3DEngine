# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `c6619ab5`. Reflection probes (`13857b43`, `86dbe008`), a placed model's clip playing
(`ba328b18`), more scenes compared with references (`7cdc3b8c`), named GPU objects (`bf7fb68a`)
and a system kept after every earlier one it conflicts with (`c6619ab5`) were taken on their
descriptions and raised nothing. The last was a fault the ledger's row turned up, a reader
running ahead of the writer added before it, and the row is corrected in
[SHARED.md](SHARED.md). The glTF texture coordinates turned over by Assimp (`f3260e46`) are this
engine's alone, since BevyCSharp reads models through Bevy's own loader.

## Now

1. **What the next run on GitHub says**, which the owner brings back. A red job or an annotation
   comes before anything else.
2. **The instructions for agents are `AGENTS.md`**, which the owner asked for on 2026-10-04.
   `CLAUDE.md` was renamed by the reviewing session with its content unchanged, since Claude Code
   reads `AGENTS.md` where there is no `CLAUDE.md`, as other coding agents do. The rename and the
   removal of the old file are uncommitted and go in with the next batch. STYLE.md's scope,
   COMMITS.md where it names the file, and `paths-ignore` in `.github/workflows/build.yml`
   follow it in the same commit.
3. **A page comparing this engine with raylib**, `docs/compared-with-raylib.md`, which the
   owner asked for on 2026-10-04, after the batch in progress.
   The page has four parts. What is the same as raylib. What this engine adds, each as a thing
   a user can do with where to see it. What it costs, said as plainly as the gains. And what was
   measured, on one machine, with the command that runs it again, since a claim about speed is a
   number or it is left out. The README gains four or five lines under its first snippet saying
   why not raylib itself, ending in a link to the page.
   - The same: one flat API, a loop the program owns, a cheatsheet, examples by name.
   - Adds: C# with no binding layer; the Vulkan renderer with metallic-roughness materials,
     cascaded, spot and point shadows, an environment and reflection probes, instancing and
     compute; the ECS with generated behaviors under the flat API; physics with a character,
     joints and contacts; skeletal animation; scene files and prefabs; behavior scripts compiled
     while the game runs; Dear ImGui inside the frame; a program driven from the terminal with
     `e3d`; native AOT builds.
   - Costs: desktop only, where raylib also runs on the web, on phones and on small boards; a
     .NET runtime or a larger native binary; Vulkan required; younger and less proven; the
     raylib functions left out, which TODO.md lists with reasons.
   - Measured, first, as a batch of its own: raylib's `textures_bunnymark` built in C and run on
     this machine beside the one here, and a count of models drawn beside `models_stress`, each
     at the count that holds 60 frames a second, and the share of raylib's cheatsheet the flat
     API carries, counted by a script from `raylib.h` with what is left out named. RENDERING.md
     §6 stays the home of the numbers, which the page quotes and links. raylib is built for the
     measurement only and is not a dependency.
4. **TODO.md's order** otherwise. The larger things BevyCSharp has and this engine lacks (saves,
   data in files of its own, files that outlive a renamed type, C# typed at a running app) are
   not scheduled, as the owner decided on 2026-10-04, and stay in [SHARED.md](SHARED.md) as
   `to consider`.
5. **To consider, not asked for:** BevyCSharp writes its cheatsheet with a tool from each call's
   XML documentation (`build/cheatsheet` in its checkout), so a line cannot say other than the
   documentation does. Here the cheatsheet is written by hand in raylib's wording and checked
   by name and parameter count, which is a choice with its own merit. If the hand-kept lines
   come to drift from the summaries, that is the way to take.

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

- Now item 2: the rename was committed by another session as `9a9d681c`. STYLE.md's scope and
  `build.yml`'s `paths-ignore` follow it in this batch. COMMITS.md does not name the file. The
  working session made no change to the file itself.

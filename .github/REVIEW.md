# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `71cae9ce`. The five warnings are gone and a warning fails the build (`71cae9ce`),
which is settled on the build and suite reported. The shadow pass sharing the model pass's
instances (`1894249e`), the immediate pass drawn by index (`b207aa72`) and the splines
(`8db9f197`) were taken on their descriptions.

## Now

1. **What the next run on GitHub says**, which the owner brings back. A red job or an annotation
   comes before anything else.
2. **TODO.md's order** otherwise. The larger things BevyCSharp has and this engine lacks (saves,
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

## Replies

- **Shared:** a contact says how hard its pair hit, as the speed they closed at along its normal
  (`ContactStarted.Speed`, `c5227118`). The solver slows a pair in the steps before it touches, so
  the speed is the fastest the pair closed at while it was near, which a ball dropped half a unit
  shows landing at the square root of twice gravity times that. A ball joint can be kept within a
  cone it swings and twists in, and a distance joint's range changed after it is made, as a winch
  does, in the same commit.

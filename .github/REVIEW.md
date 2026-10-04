# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `c84d473d`. The audio tests and music closed at shutdown (`4b6414a4`), the tests in a
workflow others call (`22c766be`, read) and the version at 5.0 (`c84d473d`, checked with
`build/version.sh`) are settled as far as can be told here. Windows is proven only by its job on
GitHub after the owner's push.

## Now

1. **What the first green or red run on GitHub says**, which the owner brings back. A red job
   comes before anything else.
2. **TODO.md's order** otherwise.

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push, as CLAUDE.md and COMMITS.md say.
2. **The package is `3DEngine` on nuget.org, the owner's own, numbered from 5.0.** The owner
   sets the major and minor in `build/version.txt`, the patch counts the commits since, and the
   `pack` workflow run by hand makes the package to download or push.

## Replies


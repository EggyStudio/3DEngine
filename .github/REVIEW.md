# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `048c072c`. Component parameters on a behavior method (`91f0f793`), `DespawnOnExit`
(`8a96917c`) and the capture script with the session listing joined from two (`048c072c`) are
settled, on the tests reported. A `ref` the method never writes going unreported is right, for
the reason the reply gave. Both things offered for the ledger are in [SHARED.md](SHARED.md).

## Now

1. **What the next run on GitHub says**, which the owner brings back. A red job or a new
   annotation comes before anything else.
2. **TODO.md's order** otherwise. The larger things SHARED.md lists as to take here (saves, data
   in files of its own, files that outlive a renamed type, C# typed at a running app) are each a
   design of their own and are placed one at a time later.

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push, as CLAUDE.md and COMMITS.md say.
2. **The package is `3DEngine` on nuget.org, the owner's own, numbered from 5.0.** The owner
   sets the major and minor in `build/version.txt`, the patch counts the commits since, and the
   `pack` workflow run by hand makes the package to download or push.

## Replies


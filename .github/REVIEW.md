# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `fbe06ea3`. Instances written whole and recorded on threads (`d12e6451`, `3b1fe5b0`),
which the stress run puts at 266,673 entities, a contact's closing speed with the joint limits
(`c5227118`), the input method's window at an ImGui field (`5cf8156f`), `DrawMeshInstanced`
(`9f0c83d2`) and the README and BUILDING.md changes were taken on their descriptions and raised
nothing. What was offered for the ledger is in [SHARED.md](SHARED.md).

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


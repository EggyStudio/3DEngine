# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `ab052859`, the `Removed` list, taken on its description. A second ImGui context in a process is refused with an exception (`f98c9547`),
which settles the verdict. Refusal over a context for each app is the right choice, since a
program's own ImGui calls cannot be guarded.

## Now

1. **TODO.md's order.** Nothing read argues for changing it.

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push. CLAUDE.md and COMMITS.md say since `b2b7fccb` that commits are
   pushed, and they are to say that commits are never pushed once the owner confirms it in the
   working session.
2. **The package's name is the owner's to choose.** nuget.org already has an unrelated package
   called `3DEngine`, which `dotnet add package 3DEngine` installs in place of this one unless a
   source mapping stops it (`e98e93a1`). Publishing under that id is not possible, and a user who
   forgets the mapping gets the wrong library with no error. The id is to change before anything
   is published, to one nobody holds, and the owner picks it. Until then the mapping in the
   README stands.

## Replies

# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `03d66fa2`. The generators' attribute tables with the test over them (`9bfd44e3`)
and the masked shadow (`03d66fa2`) are settled, on the tests reported.

## Now

In this order.

1. **The verdict below.**
2. **TODO.md's order** from there.

## Verdicts

1. **Two apps with ImGui in one process crash natively** (found by `9bfd44e3`, which moved its
   test into the `"Engine3D"` collection to avoid it). Dear ImGui's current context is one
   pointer for the process, `SdlImGuiPlugin` creates a context for each app, and a second app
   built beside the first, as xUnit builds test classes, ends in a native crash with no managed
   message. Putting each such test in a collection holds only until somebody writes one that is
   not. The plugin is to make this impossible or loud: each app keeps its own context and sets
   it current before every ImGui call it makes, under a lock where two threads could meet, or a
   second context in a process is refused with an exception that says so. Whichever is chosen,
   a test builds two apps with the default plugins at once and gets a working pair or the
   exception, and ARCHITECTURE.md says which.

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

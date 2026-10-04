# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `0f6e6e51`. Image noise and text, anisotropic filtering and the resizable flag were
taken on their descriptions and raised nothing.

## Now

1. **TODO.md's order.** Nothing read argues for changing it.
2. **MP3 and FLAC** wait on the owner (decision 3).

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
3. **A decoder for MP3 and FLAC is proposed, and the owner decides.** DESIGN.md §8 admits no
   dependency for them, and raylib reads both. NLayer decodes MP3 in managed code with no native
   library, as NVorbis does for Ogg, and fits §8's rule of a basic job done completely. FLAC has
   no managed decoder of the same standing, so it would stay unread, which TODO.md would say.
   Nothing is done on this until the owner says so in the working session.

## Replies

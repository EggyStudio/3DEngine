# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `a07a3568`. Generator fixes offered in an editor, contact points and triggers, draws
kept across frames, descriptor pools that grow, and joints in the flat API were taken on their
descriptions and raised nothing. `6854a52b` puts decision 1 into CLAUDE.md and COMMITS.md.

## Now

1. **TODO.md's order.** Nothing read argues for changing it.
2. **MP3 and FLAC** wait on the owner (decision 3).

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push, as CLAUDE.md and COMMITS.md say.
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

- **Decision 2.** The owner said on 2026-10-04 that the `3DEngine` package on nuget.org is theirs,
  so the id stays and the engine is published under it. The `pack` workflow, run from the Actions
  tab, builds, tests and packs a version from `build/version.txt` and the commits since it changed,
  and pushes it to nuget.org when asked. The README leads with `dotnet add package 3DEngine`, and
  the local pack with its source mapping is the route for a build from a checkout.
- **Decision 1.** The owner confirmed in the working session that they push `main` and the
  session commits locally, and CLAUDE.md and COMMITS.md say so.

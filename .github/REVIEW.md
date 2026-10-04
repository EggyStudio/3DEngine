# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `58924752`. The spot light's shadow (`51cac7a1`) and the `Added` filter with the
generator's repair (`58924752`) are settled, on the tests reported.

## Now

`58924752` found that a behavior using `[Changed]` had not compiled since `e612ac63`, and the
suite passed throughout because nothing in the repository used the attribute. In this order.

1. **Every attribute the generators accept is compiled and run by a test.** One test project
   input, or one theory, a case for each stage attribute, each filter (`[With]`, `[Without]`,
   `[Changed]`, `[Added]`), `[RunIf]`, the state attributes, toggle keys, `[Command]` and
   `[SceneComponent]`, each compiled through the generator and run for a frame with an assertion
   that it ran when it should and not when it should not. The list of attributes is taken from
   the generator's own table where it has one, so a new attribute without a case fails the test.
2. **A masked surface casts the shadow of its cutout**, by a fragment stage in the shadow pass
   that discards below the cutoff for masked draws only.
3. **TODO.md's order** from there.

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

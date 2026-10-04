# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `0623ccff`. The README walk (`e98e93a1`), change ticks (`e612ac63`) and the audio
systems' metadata (`0623ccff`) are settled, on the walk and the tests reported.

## Now

In this order.

1. **A material's alpha mode** (TODO.md's entry on it). A glTF file says whether a material is
   opaque, cut out at a threshold or blended, and the model pass reads none of it, so foliage
   and fences draw as solid cards and glass is opaque unless a tint makes it clear. Mask
   discards below the cutoff and stays in the opaque batches, blend joins the translucent draws
   `bf6f689e` ordered, and a texture's alpha counts as a color's does. Verified by pixel tests of
   a cut-out showing what is behind its holes and a blended surface mixing with it, and the
   validation container.
2. **Shadows past forty units and from more than one light** (TODO.md, the entry on one map).
   Cascades for the directional light, fitted to the camera's range and snapped as the single map
   is, then a spot light's shadow. A level larger than a room has no shadows today beyond the
   first forty units.
3. **`Added` beside `Changed`**, which the ticks of `e612ac63` make a comparison of two numbers.
4. **TODO.md's order** from there.

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

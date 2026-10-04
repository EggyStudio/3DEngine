# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `2d610f1c`. The vertex ring (`a10259a6`) is settled by its numbers. Instanced draws
(`2d610f1c`) were read, are settled as to speed, and raised the verdict below.

## Now

In this order.

1. **The verdict below**, since a program that drew correctly before it draws wrongly after it.
2. **The flat API's getters do not look a resource up each call**, which the batch in progress
   has begun, with `DrawTexture`'s cost a sprite looked at beside it.
3. **`MeshEntityDraws`**, which RENDERING.md §6 names as the largest cost left at the new count.
4. **TODO.md's order** from there.

## Verdicts

1. **Batching draws a translucent model out of the order it was submitted in** (`2d610f1c`).
   The model pipeline blends by alpha and writes depth (`ModelNode.cs`, `BlendEnabled: true`),
   and `Gather` puts each draw into the batch its mesh and set first opened. A program that draws
   its walls and then a glass cube of the same mesh as an earlier crate, as raylib programs do
   with a tint whose alpha is below 255, has the glass drawn with the crates, before the walls
   behind it, which the glass's depth then hides. Before this commit the order was the
   program's. Draws whose color has alpha below 1 are to stay out of the opaque batches and be
   drawn after them, in the order submitted for the flat API and from far to near for mesh
   entities, batched only while consecutive draws share a mesh and set. Verified by a pixel test
   of a half-clear quad submitted last, in front of a quad of another mesh and sharing its mesh
   with an earlier opaque draw, showing the blend of the two. TODO.md's entry on the alpha mode
   a file gives its material says what is left.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push. CLAUDE.md and COMMITS.md say since `b2b7fccb` that commits are
   pushed, and they are to say that commits are never pushed once the owner confirms it in the
   working session.

## Replies

# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `fffc5060`. The frame profile, the two stress examples and RENDERING.md §6
(`fffc5060`) were read and are settled. The numbers are what the items below are ordered by.

## Now

In this order, each ending with the same run repeated and RENDERING.md §6 holding the numbers
before and after.

1. **An animated mesh keeps its vertex buffer.** A buffer made, allocated and mapped each frame
   costs about 2 ms a mesh, so eight arms take the whole frame. Each animated mesh keeps one
   buffer a frame in flight, mapped for its life, and a frame writes into the one the GPU is not
   reading. Skinning on the GPU, with the bone matrices in a buffer and the weights in the
   vertex, removes the upload as well and goes into TODO.md as the step after, to be taken when
   the numbers after this fix say the CPU skinning is what is left.
2. **Mesh entities that share a mesh and its maps are one instanced draw.** Recording a draw each
   costs 10.3 ms for 8,004 cubes that the GPU draws in 1.8 ms. `MeshEntityDraws` groups by mesh
   and the five map views, writes each group's transforms and factors into a buffer the vertex
   stage reads by instance, and the model pass and the shadow pass each draw a group once. A
   custom shader's draws stay as they are. Verified by the stress count rising severalfold, the
   material pixel tests not moving, and the validation container.
3. **The flat API's getters do not look a resource up each call.** `GetScreenWidth` and its
   kind are called per sprite in ordinary raylib code, so they read a value the frame cached.
   `DrawTexture`'s 77 nanoseconds a sprite is looked at in the same batch, for what it spends
   outside writing its four vertices.
4. **TODO.md's order** from there.

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push. CLAUDE.md and COMMITS.md say since `b2b7fccb` that commits are
   pushed, and they are to say that commits are never pushed once the owner confirms it in the
   working session.

## Replies

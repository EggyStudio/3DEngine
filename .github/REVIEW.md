# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `2ce4e7e8`. The light payloads (`a6e3023a`) and the scene file's keys (`2ce4e7e8`)
were read and are settled, apart from decision 2.

## Now

In this order.

1. **Specular and tonemapping in the model pass** (RENDERING.md §3 and §4), each with a pixel
   assertion in `OffscreenRenderTests`, such as a highlight brighter than the same surface lit
   diffusely, and a sum of lights past one that keeps its hue instead of clamping to white.
2. **Shadows**, for the directional light first.
3. **Decision 2**, in the batch that next touches `SceneFile.cs`.

More of raylib's breadth waits behind these, unless the owner asks for a function by name.

## Verdicts

None open.

## Decisions

1. **Commits are pushed.** The owner said on 2026-10-03 that the commits made so far are fine as
   they are and that the working session may push `main` along with committing. COMMITS.md and
   CLAUDE.md say commits are never pushed, and both are to say what holds, in the next batch.
2. **An engine component keeps its short name in a scene file.** After `2ce4e7e8` the key a
   component is written under depends on what else is registered when the file is saved. A level
   saved with the engine's `Light` under `Light` loses its lights, with a warning, once the game
   adds a `Light` of its own, because `Find` refuses a short name two types share. A file is to
   read the same whatever the game registers later, so a short name that one of the engine's own
   types holds always means that type, on writing and on reading, and the other type takes its
   full name. Two types of a game that share a short name stay as `2ce4e7e8` has them. The
   alternative of writing every key as a full name was rejected, because the file is read and
   edited by a person. Recorded in ARCHITECTURE.md where the format is described.

## Replies


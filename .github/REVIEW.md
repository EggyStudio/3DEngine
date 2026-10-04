# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `9aa94324`. An entity playing a model's clips (`fa229ef4`), a character's heights
from its component (`52579d98`), storage buffers read by drawing shaders (`23d033a7`), a scene
placed inside another (`0502362d`), a collider from the meshes shown (`454e9276`) and friction
and bounce a body (`9aa94324`) were taken on their descriptions and raised nothing. The three
offered for the ledger are in [SHARED.md](SHARED.md).

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

- **Shared:** each body has a friction and a bounce of its own (`PhysicsMaterial`, a scene
  component, and `SetPhysicsBodyMaterial`, `9aa94324`), frictions mixed as the square root of their
  product and the larger bounce taken, as Box2D does. Bepu has no bounce, so a pair that starts
  touching at half a unit a second or faster is pushed apart at its bounce times its closing
  speed, which brings a ball dropped 2.5 back within a tenth of bounce squared of it. Bepu shares a
  convex pair's friction among its contacts, so a box on its face slid as if a quarter as rough,
  and the coefficient is scaled by the contact count, after which a friction of 1 stops a slide at
  the weight times 1. Two bodies a joint holds no longer collide, as most engines have it
  (`ed0f3aa6`).

- **The build with warnings as errors failed from `987b96cc` to `f2ab42ae`.** The FLAC reader
  or-ed a sign-extended value (CS0675), which a build without `-warnaserror` passes, and the
  run before each commit was read as passing. `abaf9b48` fixes it, and each batch is now
  committed only after that build reports no errors.
- **The two Entities items in TODO.md are design choices rather than faults, and are asked
  about here before any work.** Bare ids from queries are the frame's own by DESIGN.md's choice,
  and handing handles out instead changes every query and system. Change detection that misses a
  write through a store's raw array is the cost of the array being public for generated code, a
  system's first run seeing every earlier stamp is Bevy's rule, and removals kept 60 frames bound
  the memory. Each could be closed as decided, or be given a form to take, such as query rows that
  carry a handle beside the id.

- **Shared:** a joint is described in a scene file as an entity of its own with a `Joint`, which
  names the two bodies' entities and, by its own place and up direction, the point and the axis,
  so a level hangs a door where it stands. It is made once both bodies are, destroyed with its
  entity, and one that cannot be made is marked and not tried again (`e46058fc`).

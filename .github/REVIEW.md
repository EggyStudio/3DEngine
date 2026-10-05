# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `abd09df5`. Windows and macOS drawing in the workflow (`d82c3a1e`, `04c00e4c`) is settled as
written, and neither job has run, so its first result comes through the owner. The public
surface from 536 types to 244 (`82b1feb4`, `a3428422`, `abd09df5`) was read in `PublicApi.txt`
by its type names and is right in the large, and the verdict below is what is left of it. The
surface made smaller breaks a program built on an earlier 5.0 package, which decision 5 answers.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **The verdict below**, while the listing is fresh.
3. **A seventh game, of a kind not yet made.** A turn-based or real-time strategy board seen
   from above: units picked and ordered with the mouse through rays, paths found round
   obstacles on a grid, many units selected and listed in ImGui panels, fog over what is not
   seen, a match saved to a file and taken up again with whatever the engine offers for that,
   and an opponent that plays. From the package, with what it turns up fixed when small and
   entered in TODO.md when not, played, soaked and stormed by CI.
4. **What that game turned up**, in the order it hurt.
5. **TODO.md's order** for everything else, with a crowd's controller rays among it, and
   another game when it runs short.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

1. **The surface still says some things twice.** Reading the 244 type names left in
   `3DEngine/PublicApi.txt`, these pairs and groups look like one thing under more than one
   name, which the item asked to be gone. Each is either made one, or kept with a sentence in
   DESIGN.md §11 saying what tells them apart.
   - `TextureWrap` and `TextureWrapMode`.
   - `Texture` and `Texture2D`.
   - `RaycastHit` and `RayCollision`, after `a3428422` gave the physics rays raylib's shape.
   - `Material`, `ModelMaterial`, `MaterialDescription` and `MaterialSettings`, four types for
     what a surface looks like.
   - `PhysicsBody` and `RigidBody`, and `PhysicsJoint` and `Joint`, where one of each pair is
     the flat API's handle and the other the component, which the names do not say.
   And these look like the engine's own workings still public: `MeshStore` and
   `TextureStore` with their `Upload` types, `MaterialLibrary`, `ScheduleDiagnostics`,
   `WindowCommand` and `WindowData`, and the three generator classes, which are public only if
   the compiler has to find them.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push, as AGENTS.md and COMMITS.md say.
2. **The package is `3DEngine` on nuget.org, the owner's own, numbered from 5.0.** The owner
   sets the major and minor in `build/version.txt`, the patch counts the commits since, and the
   `pack` workflow run by hand makes the package to download or push.

3. **AGENTS.md's bullet and table row on SHARED.md are the owner's.** They approved them on
   2026-10-04, and they are committed like any other change.

4. **TODO.md's two Entities entries are closed as decided**, which answers the question asked
   under Replies. Ids from a query are the frame's own, and a program that keeps an entity
   across frames takes its handle with `ecs.Handle(id)`, which is one call where it matters and
   costs nothing where it does not. Query rows carrying a handle beside the id were considered
   and rejected, since every query would pay for what few keep. A write through a store's raw
   array going unseen is the price of generated code reaching the array, a system's first run
   seeing every earlier stamp is Bevy's rule and is kept so the two engines agree, and removals
   kept 60 frames bound the memory. DESIGN.md says each of these where it describes the ECS, and
   the two entries leave TODO.md.
5. **A commit that takes something out of the public surface is the owner's to number.** The
   patch counts commits and says nothing of what broke. When `PublicApi.txt` loses or reshapes a
   line, the working session says so under Replies, and the owner raises the minor or the major
   in `build/version.txt` before the next package. After `82b1feb4` and `abd09df5` that is due.

## Replies


**Verdict on names said twice and workings left public.** `MeshStore`, `TextureStore` and their
upload records, `MaterialLibrary`, `MaterialDescription`, `MaterialSettings`, `MaterialTextureRef`,
`TextureWrapMode`, `ScheduleDiagnostics`, `WindowCommand`, `WindowData` and the three generator
classes are internal, `MaterialHandle` kept public as the opaque handle a `Material` holds, and
`Config`'s window members that took them internal beside them. The asset is `TextureAsset` now,
beside `SceneAsset`, so `Texture2D` is the one thing called a texture, and the physics raycast's
result is `PhysicsRayCollision`, beside `RayCollision`. `RigidBody`, `Joint` and `Collider` keep
their names, since scene files write them, and DESIGN.md §11 says what tells each pair apart:
`Physics` for a handle or result of the world and the plain noun for a component, `Texture2D` and
`TextureAsset`, `RayCollision` and `PhysicsRayCollision`, `Material` and `ModelMaterial`. 228
public types are left. Taken out of the surface by this commit and the three before it: 308 types
(`82b1feb4`, this one), 227 members (`abd09df5`), and the raycast's and the asset's names
(`a3428422`, this one), which by Decision 5 is the owner's to raise the version for.

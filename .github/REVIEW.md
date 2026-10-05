# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `c9d40019`. Exposure that follows the scene (`c9d40019`) was taken on its description.
Swarm and what it turned up are in the working tree and are read once committed, with the
reply that describes them.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **The captures are WebP at one size**, which the owner asked for on 2026-10-05, BevyCSharp
   having done it (`29ebd78` and the batch before it in its checkout). The 49 captures in
   `.github/assets/examples` are PNG, 3.2 MB together, at four sizes (800 by 450, 960 by 540,
   800 by 448 and 1280 by 720). A small batch, taken before the game goes on.
   - An example is drawn at raylib's window, 800 by 450, and stored at the size drawn. A game
     keeps its own window's size. The one at 800 by 448 is found and put right.
   - A capture of a lit 3D scene is WebP at quality 85, and one of flat colors, 2D shapes or
     text is lossless WebP. Measured on BevyCSharp's captures, a 3D scene is 16 to 38 KB that
     way where it was 138 KB as a PNG of every color, a palette bands a glow into rings, and a
     dithered palette is larger than either.
   - `build/capture-example.sh`, the README's gallery, the guide's pictures and the workflow
     follow the `.webp` name, with `cwebp` where ImageMagick has no WebP, as BevyCSharp's
     script does.
   - The references the render tests compare with stay PNG, since a test compares pixels and a
     lossy picture would move them.
   - Verified by every example's capture being 800 by 450, the folder being smaller than it
     was, the link check passing and the README walk passing.
3. **A fourth game, driven by the ECS.** Pusher, Hopper and Summit are written in the flat
   API's loop and hold almost no behaviors, so the half of the engine under the flat API has
   been used by tests and never by a game. A small arena or tower defense under `games/`, from
   the package: hundreds of entities at once, each moved by `[Behavior]` structs taking their
   components as parameters, waves spawned and despawned by states with `DespawnOnExit` and
   `OnTransition`, projectiles as triggers with contacts that deal damage by their speed,
   `Changed`, `Added` and `Removed` filters doing real work, a prefab an enemy, a HUD in ImGui,
   many short sounds at once, and a behavior script changed while the game runs. What had to be
   worked around or looked up in the source is fixed when small and entered in TODO.md when
   not, CI plays it from the package, and `e3d command profile` says what a frame of it costs.
4. **What that game turned up**, in the order it hurt.
5. **Particles.** Smoke, sparks and dust are in most games and nothing here draws them. An
   emitter as a component and as a few flat functions, its particles simulated in a compute
   shader and drawn as instanced billboards lit or unlit, with rate, life, velocity, gravity,
   size and color over life, an example, a pixel test and a reference scene.
6. **Depth of field and motion blur**, the two effects over the frame TODO.md names as left.
7. **TODO.md's order** for everything else (physics, scenes and input each have entries), and
   when TODO.md runs short, another game of a kind not yet made, since each one has found what
   nothing else did.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

None open.

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

## Replies


**Now 2, the fourth game.** `games/Swarm` is an arena held against waves, written in behaviors with
a loop that only opens and closes frames. Up to 290 creatures walk at once, each placed from one of
two prefabs, and states with a sub-state, `DespawnOnExit`, `OnTransition`, `Changed`, `Added` and
`Removed<T>()` each do work in it. Its shots are triggers that harm by the speed of the contact. Its
HUD is ImGui and its sounds overlap through aliases. A script of its numbers is compiled again when
saved while it runs. CI plays it from the package and keeps `e3d command profile` beside the
captures. What it turned up and this batch fixes:

- static behavior methods ran side by side on worker threads while writing resources, calling ImGui
  and playing sounds, and now run alone on the main thread, with `[MainThread]` for a method of the
  instance that needs it;
- a script could not name the game's own types, and the game watched the copy of its scripts in
  `bin`, so a saved script did nothing until a build. It now references the program, and a program
  run from a project's build folder watches the project's scripts;
- each placed copy of a prefab parsed the whole file again, about 2 ms each, and uploaded its mesh
  again. One version of a file is now parsed once and its meshes shared, about 0.06 ms a copy and
  one instanced draw;
- `GetMeshComponent` turns a generated mesh into a `Mesh` component, where every example wrote its
  own cube, and `EcsCommands.DespawnRecursive` takes a placed prefab with what it holds;
- `./e3d`'s `--timeout` did not reach the program, which gave up after 30 seconds, so a long
  `frames.wait` on lavapipe failed. The request now carries it.

Left in TODO.md (Cost): 290 characters take 2.6 to 3.2 ms of physics a frame.

Shared: static behavior methods on the main thread and `[MainThread]` for instance methods, and
scripts that see the game's own assembly and are watched in the project's folder, both in
`3DEngine.Generator/Behaviors/BehaviorGenerator.cs` and `3DEngine/Behaviors/`.

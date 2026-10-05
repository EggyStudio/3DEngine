# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `4afd2b63`. A probe captured again when a light that reaches it changes (`063c47d0`) and
particles sorted within an emitter (`4afd2b63`) are settled on the replies, which were read. The
probe's first test found a capture lit by the probe's own stale map, a lamp put out still
lighting its room through its reflection, which is fixed. Sorting in blocks of shared memory at
0.02 ms where a dispatch a step took 0.47 is the measurement doing its work again.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **A first game, told from an empty folder.** The guide explains each area and nothing walks
   a newcomer through making one thing whole. `docs/first-game.md`: from `dotnet new 3dengine`
   to a small finished game in a dozen steps, each a few lines added and a picture of what the
   window shows after them, a player that moves, something to collect, a score, a sound, a
   level in a scene file, a win and a restart, ending with how it is published as one file. The
   steps' code is a project under `games/` or the template's own, built by the workflow at each
   step so no step can stop compiling, and the README's Guide lists it first.
3. **TODO.md's Physics and Scenes sections**, entry by entry in the order a game meets them,
   each with its test and its line in the guide.
4. **A probe filtered on the GPU** (TODO.md, Probes capture once and on the CPU), so a capture
   costs a frame's worth of GPU and no readback, which recapturing on a light's change made
   worth having.
5. **C# typed at a running program** (TODO.md, The command line has no evaluator), which this
   engine's own list names: an `e3d eval` that compiles a line or a file against the running
   world through the script compiler already there, for looking at and changing a game while it
   runs.
6. **TODO.md's order** for everything else, and another game only when it is of a kind that
   uses what none of the seven has.

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
5. **A commit that takes something out of the public surface is the owner's to number.** The
   patch counts commits and says nothing of what broke. When `PublicApi.txt` loses or reshapes a
   line, the working session says so under Replies, and the owner raises the minor or the major
   in `build/version.txt` before the next package. After `82b1feb4` and `abd09df5` that is due.

## Replies


**A first game, told from an empty folder.** `docs/first-game.md`, first in the README's Guide,
makes Coins from `dotnet new 3dengine` in twelve steps: a field under a sun with a player, walking,
a camera that follows, coins, taking them, the score, a chime made from its samples so the reader
needs no file, the level in a scene file with `Coin` and `Crate` components the game declares,
crates in the way, a win with R to start again, and a native publish. Each step is a whole program
under `games/FirstGame/steps`, the last being `Program.cs`. `build/first-game.sh` builds each
against the package and runs it offscreen, and with `--shots` walks it through `./e3d` and writes
its picture into `.github/assets/first-game`. The workflow runs it after the README walk.
`FirstGameTests` holds every block the page marks with a step to that step's program line for line,
and the page's level to the game's file, and changing one number in the page fails it. The publish
was tried, an 11 MB executable beside SDL3's, cimgui's and Assimp's libraries, the resources and the
compiled shaders, which the step says rather than calling it one file.

**Physics and Scenes, first entry.** The two sections are rewritten as what there is and, below
it, an entry for each thing missing in the order a game meets it: a fast body through a thin wall,
a sliding joint and motors past the hinge, a contact's impulse, a scene spawned again keeping its
textures, and migration, which stays to consider by the owner's decision. Before those came
collision layers, which none of the sections named and a game meets first: `SetPhysicsBodyLayer`,
`GetPhysicsBodyLayer` and `SetPhysicsLayersCollide` over 32 layers, checked in the narrow phase, a
`Collider`'s `Layer` in scene files, and the character's ground rays, the broad phase shortcut,
wheels and `GetRayCollisionPhysicsEx` seeing only what the layer of the body they look past
collides with. Tests find balls and a box passing through what their layers do not collide with
and reporting no contact, a trigger reporting only the player's layer, a character falling through
a platform to the floor, and a layer through a scene file, with a section in docs/physics.md.
`PublicApi.txt` gains three functions and a field. The other entries follow in order.

**A fast body through a thin wall.** Measured first: every body's contacts reached a tenth of a
unit ahead, so a ball of 20 units a second crossed a wall a fifth of a unit thick.
`SetPhysicsBodyContinuous`, and a `RigidBody`'s `Continuous` in scene files, sweep a body with
Bepu's continuous mode and its margin unbounded, which stops it at a wall of any thickness at 50
units a second, a fifth of a unit's at 100 and half a unit's at 300. Past that a 30 Hz contact
spring, the stiffest a sixtieth's step solves, stops it over a step rather than at once, which the
documents say, pointing a bullet at a ball cast. It stays a choice per body rather than every
body's, since it costs a sweep test a pair, and the games' bodies keep what they were tuned with.
A test finds a ball at 40 crossing a thin wall plain and stopping swept.

**A slider.** `CreatePhysicsSliderJoint`, `SetPhysicsSliderLimits`, `SetPhysicsSliderMotor` and
`GetPhysicsSliderPosition`, and a scene `Joint` of kind `Slider` along the joint entity's up with
its limits and motor in the existing fields: Bepu's point-on-line servo keeps the second body on a
line through where it started, an angular servo keeps it from turning, and a linear axis limit and
motor are added and replaced as the hinge's are. A test drives a lift's car to its upper limit,
pushed and twisted on the way and staying on its line unturned, then back to its lower limit, and a
scene file's slider turned to run along X reaches its limit there. With it the entry on motors
goes, since a hinge and a slider are the joints a game drives. `PublicApi.txt` gains four
functions and an enum member.

**A scene spawned again by hot reload.** The copy `SceneHotReloadSystem` spawns in place of a model
written while it runs now loads its textures through the spawn that collects them, and
`AssetRelease` holds them by its entities, as the first copy's were held, so the old copy's are
given back after the grace and a level edited over and over no longer grows. Writing the test
found a second fault behind it: the new copy was spawned with no parent, so it lost the place of
the entity that put the model there and outlived that entity. It is hung under the old copy's
owner now. The test reports the model modified, sees the copy spawned again under its owner, and
finds the texture let go once the owner is despawned, which fails without the change. What is left
of the entry is a program's own `SceneSpawner.Spawn` with an asset server, which TODO.md says.

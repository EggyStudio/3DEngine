# Shared

What BevyCSharp and 3DEngine have in common, and which of the two has solved what. The engines
are built differently, one over Bevy through a bridge and one on its own Vulkan renderer, and they
answer many of the same questions: how a behavior is written, what the ECS offers it, how states,
physics, scenes and saves work, how a running app is driven from a terminal, and how the project
is tested, packed and versioned. An answer one of them finds is offered to the other as an idea,
and each implements it in its own way.

This file is the same in both repositories and has one writer, the session that writes
[REVIEW.md](REVIEW.md). The session working on a repository offers something for it with a line
under Replies in REVIEW.md that begins `Shared:`, and may read the other repository for the model
it names, without editing it.

## What counts

An idea is shared when it changes what somebody using the engine writes or can rely on. How it is
built underneath is each engine's own and is not recorded here.

Shared areas:

- behaviors and the generators behind them, with their attributes and diagnostics
- the ECS a behavior reaches: queries, filters, change detection, the hierarchy, commands
- states and their transitions
- physics over BepuPhysics: bodies, colliders, joints, contacts, characters
- scene files, saves, data in files, and files that outlive a renamed type
- input, including what a script injects
- the command-line tool that drives a running app (`bcs`, `e3d`)
- tests, the games built from the package, measurement, CI, packing and the version
- the rules for prose and commits

Not shared:

- rendering, which is Bevy's in BevyCSharp and the engine's own in 3DEngine
- the editor, which BevyCSharp has and 3DEngine has decided against
- the flat raylib-style API, which is 3DEngine's alone

## The ledger

One row an idea. The state of the engine that lacks it is `to take`, `taken at` a commit, `to
consider` for an idea that is not scheduled and is taken only if it comes to suit that engine,
`does not apply` with the reason, or `waits on the owner`. A row stays once both have it, since the
table also answers whether the two agree.

### Behaviors and generators

| Idea | BevyCSharp | 3DEngine |
|---|---|---|
| A behavior method names its entity's other components as parameters, `Tick(BehaviorContext ctx, ref Transform transform, in Velocity velocity)`, and is handed them with no lookup of its own | has (`49ea5eb`) | taken at `91f0f793`, with E3D008 for a parameter it cannot hand over |
| One table of the attributes a generator accepts, and a test that compiles and runs a use of each | has (`deb1b79`) | has (`9bfd44e3`) |
| Diagnostics for a behavior or a command written wrongly | has (`BevyCSharp.Generator/BehaviorDiagnostics.cs`) | has (E3D001 to E3D006) |
| Fixes for those diagnostics offered in an editor | to take | has (`3DEngine.CodeFixes`, `c6b529d4`) |

### The ECS

| Idea | BevyCSharp | 3DEngine |
|---|---|---|
| An order among systems in one stage, one before or after another or a chain | taken at `bb1ae57`, as `After`, `Before` and `App.Chain` | to consider, where a system runs after every earlier one it conflicts with (`c6619ab5`) and nothing orders two by name |
| An observer told when a component is added or removed or an event of a game's own is sent to an entity | taken at `26c0a16` | to consider |
| `Changed` and `Added` filters that a system not run every frame can trust | has, from Bevy | has (`e612ac63`, `58924752`) |
| The entities that lost a component since a system last ran | to take | has (`Removed`, `ab052859`) |
| A read that does not mark its component changed, beside one that does | has, from Bevy | has (`GetReadOnly`, `QueryReadOnly`) |

### Animation

| Idea | BevyCSharp | 3DEngine |
|---|---|---|
| An entity plays a model file's clips by name, blending from one to the next | has (`f79472b`) | has (`AnimatedModel`, `fa229ef4`) |

### States

| Idea | BevyCSharp | 3DEngine |
|---|---|---|
| Sub-states and computed states, declared on the enum | has (`16c4c1e`) | has (`eca05448`) |
| A system run on a move from one value to a particular other | taken at `66a5b4d` | has (`OnTransition`, `3ca94c66`) |
| An entity that lives as long as a state holds a value | has (`DespawnOnExit`) | taken at `8a96917c` |
| A script compiled again while a game runs keeps the state the game was in | has (`16c4c1e`) | to take |

### Physics

| Idea | BevyCSharp | 3DEngine |
|---|---|---|
| A body and a collider are components a scene file holds | has (`99ec076`) | has (`606cb3bf`) |
| A character that walls stop, that slides, steps and holds slopes | taken at `f2ac0cd` | has (`b9f280b2`) |
| Contacts with their point and normal, and triggers | has | has (`5fb77861`) |
| A character crouches and stands from its component's height, and its step height is set there | to take | has (`52579d98`) |
| A collider that is the shape of the meshes an entity and those under it show, made once they are loaded | to take, where triangles are given by hand | has (`Collider.Mesh`, `454e9276`) |
| Friction and bounce for each body, mixed for a pair | has | has (`9aa94324`) |
| A joint described in a scene file as an entity naming its two bodies, at its own place and axis | to take | has (`Joint`, `e46058fc`) |
| Two bodies a joint holds do not collide with each other | has (`1f10323`) | has (`ed0f3aa6`) |
| A contact says how hard its pair hit, as the speed they closed at | to take | has (`ContactStarted.Speed`, `c5227118`) |
| A ball joint kept within a cone it swings and twists in, and a distance joint whose range changes after it is made | to take | has (`c5227118`) |
| The sync reads only bodies that changed and writes only bodies that moved | has (`ba5cff4`) | has (`e612ac63` and after) |

### Scenes, saves and files

| Idea | BevyCSharp | 3DEngine |
|---|---|---|
| A scene file placed inside another, its entities left out of the outer file's save | has (`SceneInstances`) | has (`SceneRef`, `0502362d`) |
| A scene file holds arrays, so a mesh made in code is saved with its level | has | has (`8567bea6`) |
| A placed scene file written while the level runs is spawned again in place of its copies | to check against `SceneInstances` | has (`6059b57a`) |
| A model file with animation clips placed in a level plays, where its meshes would stand at rest | to check | has (`ba328b18`) |
| A renamed or reshaped type still reads its old files | has (`FormerName`, `DataVersion`) | to consider |
| A saved game laid over the scenes it started from | has (`SaveGame`, `Persistent<T>`) | to consider |
| Data in files of its own, referred to by an id that survives a rename | has (`[DataAsset]`, `DataRef<T>`) | to consider |
| A message after a load, so a game builds once what a file does not hold | has (`3ab5b22`) | to consider, with saves |

### Input and the command line

| Idea | BevyCSharp | 3DEngine |
|---|---|---|
| A key held for an exact number of frames by one command | has (`324f919`) | has (`input.key`) |
| Gamepads | taken at `7f87a47`, with a pretended pad a script drives | has |
| A gamepad's gyro, accelerometer, touchpad and light | to take | has (`73ce6326`) |
| A pointer dragged a step a frame by one command, so a swipe or a window drag registers | taken at `ce27e73` | has (`input.drag`, `048c072c`) |
| The listing of running sessions taken twice and joined, since one taken while a session file is replaced can leave it out | has (`CliSession.cs`) | has (`048c072c`) |
| A field holding an array written from the terminal, its items split by semicolons | to check against `entity.set` | has (`3cab9d9d`) |
| Files dropped on the window reach the program, and a command pretends a drop | has the messages, the command to check | has (`input.drop`, `eca234f9`) |
| A command's parameter with a default may be left off, shown in brackets in its usage | to check against the command generator | has (`a3d56597`) |
| C# typed at a running app | has in the editor (`eval`) | to consider |
| The frame's cost by part, from one command | has (`frame.profile`, `d6a03d2`) | has (`profile`, `fffc5060`) |

### Tests, CI and packaging

| Idea | BevyCSharp | 3DEngine |
|---|---|---|
| A test that cannot run is reported as skipped with its reason | has (`e857326`) | has (`939ba258`) |
| Examples picked by name as an argument, each with a capture CI takes | taken at `661682e`, measured against Bevy's own 421 examples in EXAMPLES.md | has (`3DEngine.Examples`, `048c072c`) |
| Behaviors registered by a module initializer the generator writes, so a game published trimmed or as native AOT keeps them | has | has (`425ffc31`), with Pusher published native |
| A game published as native AOT and run by CI | to take | has (`458cd909`) |
| Whole pictures compared with checked-in references, a small share of pixels allowed to differ between devices | to take, for the examples' captures | has (`771f10e9`, `fd5bcc84`) |
| A small game built from the packed package and played by CI | has (`f147adc`) | has (`377576c4`) |
| The README's first program followed in a clean container by CI | has (`8ff919c`) | has (`e98e93a1`) |
| A version whose patch counts commits since the owner last set the major and minor | taken at `88954d5` | has (`609bd859`) |
| A package made by a workflow run by hand, after tests on Linux and Windows | taken at `88954d5` | has (`22c766be`) |
| Graphics run under a validation layer in CI, an error failing the run | does not apply, since wgpu validates for Bevy | has (`a2e19d7c`) |

### Documents

| Idea | BevyCSharp | 3DEngine |
|---|---|---|
| The cheatsheet written by a tool from each call's documentation, so the two cannot differ | has (`build/cheatsheet`, `4de242d`) | to consider, where it is written by hand and checked by name |
| Documents in three places by reader: a README of about 200 lines for somebody deciding, `docs/` with a page an area for somebody using the engine, the cheatsheet at the root beside the README, `.github/` for somebody working on it | taken at `a0b1fa3`, 24 pages | has (`df8f633e`, 15 pages), the README being the model |
| The README's table of contents is the guide's, a line a page, with full URLs since it is the package's page too | taken at `a0b1fa3` | has (`c8cce1ce`) |
| A cheatsheet of the whole public surface, a line a call, held to the API by a test | taken at `4de242d`, at the root | has (`a2336d0e`), at the root since `695b5ca6` |
| Every link in the README and the guide followed by a check in the workflow | taken at `a0b1fa3` | has (`DocumentLinkTests`, `07c15314`) |
| The instructions for coding agents are `AGENTS.md` at the root, the name every such tool reads | renamed on 2026-10-04 | renamed on 2026-10-04 |
| A page comparing the engine with the one it follows: what is the same, what it adds, what it costs, and what was measured | taken at `e98b3b0`, with Bevy | has (`docs/compared-with-raylib.md`, `b0d719cc`), with raylib built in C and measured beside it |
| A picture of an example opens the live demo of it on the site of the engine it follows | taken at `29ebd78`, 276 of Bevy's examples | taken at `ac529628`, the 17 examples raylib's site has |
| Captures stored as WebP at the size of the window the followed engine uses, lossy for a lit scene and lossless for flat color | has (`29ebd78`), at Bevy's 1280 by 720 | to take, at raylib's 800 by 450 |

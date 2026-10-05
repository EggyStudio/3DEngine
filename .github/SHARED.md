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
| A behavior method that touches what only one thread may (resources, the interface, sounds) runs on the main thread, by rule for a static method and by an attribute for an instance one | to check | has (`[MainThread]`, `3c9c7ac8`) |
| A script compiled while the game runs names the game's own types, and the scripts watched are the project's, not a copy in the build folder | to check | has (`3c9c7ac8`) |
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
| A ray passes through a trigger, so a sensor never holds up a wheel or a character's ground check | to check | has (`b5eb3642`) |
| A raycast vehicle made by one call beside the character controller, tuned by one record | to consider | has (`CreatePhysicsVehicle`, `ee641437`) |
| The physics step on several workers past a count of awake bodies, repeating to the bit on every machine | to check | has (`319832dc`) |
| A contact says how hard its pair hit, as the speed they closed at | to take | has (`ContactStarted.Speed`, `c5227118`) |
| A ball joint kept within a cone it swings and twists in, and a distance joint whose range changes after it is made | to take | has (`c5227118`) |
| The sync reads only bodies that changed and writes only bodies that moved | has (`ba5cff4`) | has (`e612ac63` and after) |
| What rests on a kinematic body that a transform moves keeps the mover's pace at any frame rate, the body moving at the mover's speed through every step and not a frame's distance in one | to take, measured first, where a body gets a frame's distance over one step and a turn gives it no spin | taken at `15fa305a`, a crate at 2.00 within a hundredth at seven frame rates and uneven frames, a parent's placing said with `MarkPlaced` or past a set distance (`7ae91e7c`) |
| A frame's time and the fixed steps that spend it under one clamp, so what a program moved by frame time and what was simulated agree | has, as Bevy's clock and fixed schedule do | taken at `ee3b47dd`, the frame's clamp of a quarter second the one kept |
| Bodies on collision layers whose pairs collide or not, which contacts, triggers, characters and rays follow, a sleeping body woken when its layer or trigger changes | to take | has (`8520dbe1`, `ac897afa`) |
| A body a game knows is fast swept over each step, so it does not cross a thin wall within one, chosen for each body | to take | has (`SetPhysicsBodyContinuous`, `799a9d56`) |
| A slider joint, one body along an axis against another without turning, with limits, a motor and its position, from code and from a scene file | to take | has (`979c97be`) |
| A game asks how hard two touching bodies press, answered while they sleep too | to take, as the push alone | has (`GetPhysicsContactImpulse`, `53cd565f`), the push alone since `c774a379` |

### Scenes, saves and files

| Idea | BevyCSharp | 3DEngine |
|---|---|---|
| A scene file placed inside another, its entities left out of the outer file's save | has (`SceneInstances`) | has (`SceneRef`, `0502362d`) |
| A scene file holds arrays, so a mesh made in code is saved with its level | has | has (`8567bea6`) |
| A placed scene file written while the level runs is spawned again in place of its copies, under the entity that placed it and giving back what the old copy held | to check against `SceneInstances` | has (`6059b57a`, `5b2234d2`) |
| A model's sibling files, an OBJ's `.mtl` and a glTF's `.bin`, come from the reader the model came from, whatever reader that is, and no native code opens a file | has for glTF, the loader being Bevy's | has (`AssimpFiles`, `abc24192`), read from the model's own stream with no copy and a reader's exception answered as the load's (`1fac9eff`) |
| A model file with animation clips placed in a level plays, where its meshes would stand at rest | to check | has (`ba328b18`) |
| What a level loaded through its references is let go once nothing uses it | has, Bevy counting its handles | has (`4e765797`) |
| A renamed or reshaped type still reads its old files | has (`FormerName`, `DataVersion`) | to consider |
| A saved game laid over the scenes it started from | has (`SaveGame`, `Persistent<T>`) | to consider |
| Data in files of its own, referred to by an id that survives a rename | has (`[DataAsset]`, `DataRef<T>`) | to consider |
| A message after a load, so a game builds once what a file does not hold | has (`3ab5b22`) | to consider, with saves |

### Input and the command line

| Idea | BevyCSharp | 3DEngine |
|---|---|---|
| A key held for an exact number of frames by one command | has (`324f919`) | has (`input.key`) |
| Every menu played with a gamepad alone, settings and key bindings kept in a file | to check, with Courtyard | has (`games/Manor`, `4161a8c5`) |
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
| A game published as native AOT and run by CI | to take | has (`build/play-native.sh`, `f2abc4f0`), Pusher drawn for 300 frames under the validation layer |
| Whole pictures compared with checked-in references, a small share of pixels allowed to differ between devices | to take, for the examples' captures | has (`771f10e9`, `fd5bcc84`) |
| Seven games of different kinds built from the package, the later ones finding nothing new | has one, Courtyard | has (`games/`, to `7a8360ae`) |
| A game written in behaviors alone, with hundreds of entities, played by CI and profiled | to take | has (`games/Swarm`, `3c9c7ac8`) |
| A game played for minutes by a script while memory, GPU objects and entity ids are read, a count that keeps climbing failing the run | to take | has (`build/soak.sh`, `044d2396`) |
| An app made and closed a hundred times in one test holds no more than it held after ten, read before any collection, the rule a soak keeps for a game kept for an app's whole life | to check, with `ScriptHost.References()` reading every loaded assembly at each compilation, the growth 3DEngine found | taken at `c06ec659`, `AppLeakTests`, the growth being the script compiler's references read at every app's start |
| Every loader given a missing, an empty, a cut short and a random file, answering with a message and no exception, as one table in a test | to take | has (`BadFileTests`, `3442e2cd`) |
| A window resized, minimized and moved by commands while it draws, as a storm the workflow runs | to consider, the window being Bevy's | has (`build/storm.sh`, `b0d835c4`) |
| The public surface listed in a checked-in file a tool writes from the built assembly, a test failing when they differ | to take | has (`PublicApi.txt`, `fc5aef49`) |
| Every public member documented, an undocumented one failing the build, the documentation carried in the package | to check | has (`a4b2785c`) |
| A template package, so `dotnet new` starts a game | to take | has (`3DEngine.Templates`, `ec7e6c3c`) |
| A test that opens the packed package and finds everything it should hold, natives for each system among it | to take | has (`PackageContentsTests`, `760b8206`) |
| The package's release notes written from the commits since the version was last set | to take | has (`fc5aef49`) |
| A small game built from the packed package and played by CI | has (`f147adc`) | has (`377576c4`) |
| The README's first program followed in a clean container by CI | has (`8ff919c`) | has (`e98e93a1`) |
| A version whose patch counts commits since the owner last set the major and minor | taken at `88954d5` | has (`609bd859`) |
| A package made by a workflow run by hand, after tests on Linux and Windows | taken at `88954d5` | has (`22c766be`) |
| Graphics run under a validation layer in CI, an error failing the run | does not apply, since wgpu validates for Bevy | has (`a2e19d7c`) |
| A clock stepped by a set amount a frame, for a test and for a run with no window, so motion is measured in frames and is the same on every machine | to take, through Bevy's `TimeUpdateStrategy::ManualDuration` | taken at `966c2c88`, `Time.FrameSeconds` and `--frame-time` |
| A loader lets go of its file when a load returns, checked on Linux as well as Windows, and a test's folder that cannot be removed says which process holds it | to check | taken at `abc24192`, `FileHandleTests` over eleven loaders and `TestFolder` |
| Every example of the engine it follows is a row of a table a script makes from that engine's own list, each written, written in part, able to be written, missing or not applying | has (`.github/EXAMPLES.md`, 225 written of the 363 that apply), the workflow not yet holding the table to its script | taken at `c05bd485`, 41 written of the 204 that apply at `14c8b4a1`, the workflow holding the table to its script |
| An example compiles on the package alone, what the examples share to say a thing in one word being the package's own calls | to take, where 208 of 231 examples call helpers of the examples project | has (`build/examples-on-package.sh`, `a61308b0`), every example built on the packed package |
| The followed engine's own files that its examples load are fetched at a pinned commit and not kept in the repository | has (`bevy-assets.txt`) | taken at `7b9b2f2e` (`raylib-resources.txt`, `build/fetch-raylib-resources.sh`) |
| An example written from the followed engine's says so at its head, with that engine's copyright line and license | taken at `00c3db9`, all 218 written | has (`7b9b2f2e`), each port naming raylib's example, its authors and the zlib license |
| The package carries the notices of everything in it that is another's, and a test holds the notices to the dependencies | taken at `00c3db9`, every crate of the bridge's lock named, 569 of them, by a script the pack workflow checks | has (`THIRD-PARTY-NOTICES.md` in the package, `PackageContentsTests`) |
| A script that more than one system runs is read by a test for the forms only GNU's tools or a later bash read | to take, one line found | has (`ScriptTests`, `fd7b17f3`) |
| Every method native code calls catches every exception, a test finding one that does not by how it is handed over | to take | has (`NormTests.N_2_10`, `48fbb663`), six of twelve mended |
| A run that fails writes a page of at most 200 lines, the failures by cause with message, frames and tests, as the end of the log, the job's summary and its annotations, one script running the tests in the workflow and for a working session | to take, from 3DEngine's script | to take (REVIEW.md, Verdict 15) |
| A test process held to a time and a memory, the suite run again in parts when one is lost, so a crash, a hang or a leak is named and the runner stays up to say so | to take, from 3DEngine's script | to take (REVIEW.md, Verdict 15) |
| A test in which the engine logs an error fails unless it says it expects that error | to take | to take (REVIEW.md, item 2) |
| A system that throws in every frame is logged in full once and counted after | to check | to take (REVIEW.md, Verdict 14) |

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
| A picture of an example opens that example's source in the repository, the owner's choice on 2026-10-05 over the live demo on the followed engine's site, so nothing is cached from another project | taken at `57fc7e9`, all 198 pictures | taken at `bf1a559c`, all 52 pictures |
| A first game told from an empty folder a step at a time, each step a whole program the workflow builds and runs and the page is held to | to take, where `docs/making-a-game.md` describes a finished one | has (`docs/first-game.md`, `d5d2578d`) |
| The rules both engines keep are numbered in one file, each with its reason and a check named for it, and a list of what does not yet keep a rule that only gets shorter | taken at `e7d788a`, `NormTests` over 13 rules with 8 lists | taken at `9decca1d`, `NormTests` over 12 rules with 9 lists |
| Captures stored as WebP at the size of the window the followed engine uses, lossy for a lit scene and lossless for flat color | has (`29ebd78`), at Bevy's 1280 by 720 | taken at `e673197a`, at raylib's 800 by 450 |

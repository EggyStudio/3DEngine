# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `979b7587`. The physics step on four workers past 500 awake bodies, repeating to the
bit (`319832dc`), a raycast vehicle with Rally's car made one (`ee641437`) and a probe captured a
face a frame (`979b7587`) are settled on the replies, which were read. Turning threads on only
where the measurement showed a gain, and finding that contacts had to be sorted for a run to
repeat, is the way to do it. The guide's items are settled.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **The engine draws on Windows and macOS in the workflow.** DESIGN.md says it runs on Linux,
   Windows and macOS, the workflow's Windows job runs only the tests that need no device, and
   nothing has drawn a frame on either system. A software Vulkan for the Windows runner (Mesa's
   lavapipe is built for Windows) so the render tests, the reference comparisons and one game
   run there with the validation layer, and a macOS job through MoltenVK doing the same as far
   as the runner's GPU allows. Each fault found is fixed, and what cannot be made to run is said
   in BUILDING.md with the reason, since a platform nothing has drawn on is a claim.
3. **The public surface is read whole and made smaller and even.** `3DEngine/PublicApi.txt`
   lists 536 public types, far more than a program on the flat API or the ECS needs, and every
   one is a promise from 5.0 on. Read against these: a type or member no program outside the
   engine has reason to call becomes internal (render graph nodes, device wrappers, stores,
   packers, the generator's support types that only generated code calls being marked as such);
   the flat API's names and parameter orders agree with raylib's where raylib has the function
   and with each other where it has not (`Get`, `Set`, `Is`, `Load`, `Unload`, `Begin`, `End`);
   one concept has one name across the flat API, the components and the console commands; and
   nothing is public twice under two spellings. The listing's diff is the record, the games and
   examples still build from the package, and TODO.md says what was left public on purpose
   and why.
4. **A seventh game, of a kind not yet made.** A turn-based or real-time strategy board seen
   from above: units picked and ordered with the mouse through rays, paths found round
   obstacles on a grid, many units selected and listed in ImGui panels, fog over what is not
   seen, a match saved to a file and taken up again with whatever the engine offers for that,
   and an opponent that plays. From the package, with what it turns up fixed when small and
   entered in TODO.md when not, played, soaked and stormed by CI.
5. **What that game turned up**, in the order it hurt.
6. **TODO.md's order** for everything else, with a crowd's controller rays among it, and
   another game when it runs short.

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


**Now 2, drawing on Windows and macOS.** `test.yml` has a macOS job and its Windows job draws now.
Windows takes the loader from LunarG's runtime, the validation layer from the SDK and lavapipe
from Mesa's Windows build, pointed at by the loader's variables, and macOS takes MoltenVK, the
loader and the layer from Homebrew. Each runs the whole suite, render tests and reference frames
among them, with `E3D_REQUIRE_VALIDATION` and a new `E3D_REQUIRE_VULKAN`, which makes a device
that does not start fail the render tests rather than skip them to a green run, and
`build/play-game.sh Pusher` builds the game from the package and draws 300 frames offscreen,
failing on a layer error. The engine asks for portability devices where the loader offers them
and enables the portability subset where a device has it, which MoltenVK needs to be listed at all.
The script passes on lavapipe in the container with the layer, and the render tests with both
variables set, but neither new job has run, so the first run on GitHub (Now 1) will be what says
whether the installs are right.

**Now 3, the public surface, first pass.** Every public type no example, game, guide, the
cheatsheet or the generators' code reaches, directly or through the signature of something that is
reached, is internal now: the renderer, its nodes and pipelines, the device and its abstractions,
the platform's backends, the loaders and decoders, the plugins beside `DefaultPlugins`, the CLI's
server and the diagnostics' internals. A few no program names are kept as the ECS's own
(`DefaultPlugins`, events, `FixedTime`, `GlobalTransform`, asset events, `SceneAsset` and its spawn
request). 536 public types are 244, the listing 2,200 lines shorter, every example and game builds
from the package, Swarm's script still compiles as it runs, and the tests and `e3d` see the rest
through `InternalsVisibleTo`. DESIGN.md §11 says the rule. Names and parameter orders against
raylib's and each other follow next.

**Now 3, names.** The flat API's parameter names were read by type across all 634 functions, and
agree but for two places. `ImageDraw`, `ImageDrawText` and `ImageDrawTextEx` named their image
`destination` where raylib and the other image functions say `dst`, `src`, `srcRec` and `dstRec`,
and the physics raycasts answered a flag with the hit in an out parameter where raylib's
`GetRayCollision` functions give back the collision, so they return a `RaycastHit` whose new `Hit`
says whether it met a body.

**Now 3, members.** On the engine's service classes, `PhysicsWorld`, `EcsWorld`, `Input`, `App`,
`World`, `Schedule`, the asset and audio servers, the texture, mesh and material stores and a few
more, every public member whose name no example, game, guide or generator writes is internal, 227
in all, those implementing an interface kept public. The flat API is untouched, as are the
components' fields and the settings records a program fills in. The listing now holds 244 types and
2,575 members, from 536 types and about 5,000 lines this morning. What is left public that no
program calls is mostly in records set through initializers, which the name search cannot tell from
unused, and is the next pass if it is wanted.

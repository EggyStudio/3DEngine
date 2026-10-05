# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `7a8360ae`. Tactics, the seventh game (`7a8360ae`), is settled, played, soaked and stormed
by CI. It turned up nothing in the engine, every helper it needed being there, which says the
games have found what games of this size find, and the list turns to what a stranger meets when
they take the package.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run on GitHub says**, when the owner brings one back. A red job or an annotation
   comes before anything else.
2. **Every public member says what it does.** 228 public types are what a program sees in its
   editor, and a member with no summary shows nothing there. The compiler's warning for a public
   member with no XML documentation (CS1591) becomes an error in the engine's project, each gap
   is written to STYLE.md, and the package carries the documentation file so a game's editor
   shows it. A generated member or one that only generated code calls is marked so and left out.
3. **A template that starts a game.** `dotnet new` with a template package beside the engine's:
   one command makes a project that references the package, with a window, a loop, a first
   shape, a folder for resources and scripts, and a README of three lines, in a flat form and
   an ECS form. `build/pack.sh` packs it, the README's "A program of your own" becomes that one
   command with the hand steps after it, and the README walk follows it in the container.
4. **What is in the package is checked.** A test opens the packed `.nupkg` and finds the
   engine, the generator as an analyzer, the documentation file, the compiled shader cache, the
   README, the license and the notices of the libraries it carries or depends on, the release
   notes, and a native library for each system the package says it runs on. The pack workflow
   runs it before the package is offered. A file of third-party notices is written if there is
   none.
5. **TODO.md's order** for everything else, with a crowd's controller rays among it. Another
   game is written only when it is of a kind that uses what none of the seven has.

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


**Every public member says what it does.** CS1591 is an error in the engine's project and
silenced still for the tests, examples and tools. It found 188 gaps: 183 members of `Key`, the
store's two boxed accessors, `PhysicsWorld`'s second constructor, and the generated
`BehaviorRegistration`. Each `Key` member says where its key is, since a scan code is a place on
the keyboard, and the enum's remarks say `W` types Z on a French layout. The generator writes
`BehaviorRegistration` internal now, since a program never calls it and a script's is found with
non-public binding, so `PublicApi.txt` loses its two lines, which by Decision 5 is the owner's to
number. `GeneratedBehaviors` and `GeneratedBehaviorRegistrationAttribute`, public only because
generated code in a game's assembly reaches them, are hidden from completion with
`EditorBrowsable(Never)`, and the attribute's summary, which described something else, is
corrected. STYLE.md has a Documentation section with the rule and the three kinds of gap. The
package already carried `lib/net10.0/3DEngine.xml`, and Now 4's test will hold it to that.

**A template that starts a game.** `templates/` is the `3DEngine.Templates` package, which
`build/pack.sh` packs beside the engine at the same version and the pack workflow pushes with it.
`dotnet new 3dengine` makes a project with a window, a loop and a cube, and `dotnet new
3dengine-ecs` one whose cubes are behaviors turned at a speed a script in `source/behaviors` sets,
each with `resources/`, `source/behaviors/` and a README of three lines. Each asks for the engine
version packed with it, and `--package-folder` writes a `nuget.config` for a local package folder.
The README's "A program of your own" is those three commands, with the hand steps after them, and
BUILDING.md's local section starts with the template. `build/readme-walk.sh` follows the README's
commands with both templates installed from the folder into a list of its own, then the hand
steps, and passed here and in the Ubuntu 24.04 container on lavapipe. Two things are the owner's:
whether `3DEngine.Templates` is free on nuget.org before the first push, and a `templates/` row
in AGENTS.md's table of where things are, which this session leaves to them.

**What is in the package is checked.** `PackageContentsTests` opens the newest engine package in
`build/package` and finds the library, the generator and its fixes as analyzers, the
documentation file with the flat API in it, a compiled `.spv` for every entry point of every
built-in shader, the README, the license, the notices and release notes, and, for each system in
BUILDING.md's table of platforms on x64 and arm64, SDL3, cimgui and Assimp in the dependencies the
nuspec names at the versions it names. A package missing the license and one shader's entry
point fails two of the four, naming both. The tests skip where no package was made, and the pack
workflow sets `E3D_REQUIRE_PACKAGE` and runs them after `build/pack.sh` and before the package is
uploaded or pushed, as `build.yml` does after its pack. `THIRD-PARTY-NOTICES.md` is new, naming
every dependency with its license, which the test holds to the nuspec, and the package now
carries it and `LICENSE`. Release notes are never empty, a version raised by the last commit saying it is the
first package of that version.

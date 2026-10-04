# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `e2bdea1e`. The shadow pass's instances written once a frame (`aec68393`) and the
flat API's further raylib functions (`e2bdea1e`) were taken on their descriptions and raised
nothing.

## Now

The first runs on GitHub were green on Linux and Windows, and the `pack` run made 3DEngine 5.0.0
as its artifact. They carried 33 warnings and 2 notices, which are item 1.

1. **The runs are quiet.** Three kinds of annotation, each on all three jobs.
   - Ten faults in XML documentation: a `cref` that fits two overloads at
     `3DEngine/Ecs/EcsWorld.API.cs:170` (`GetRef{T}`) and `:310` (`GetReadOnly{T}`), which name
     the overload meant; `id` and `down` with no `param` on `Input.SetTouch`
     (`3DEngine/Core/Input/Input.cs:270`); a `param` for a parameter that is gone at
     `3DEngine/Components/Material.cs:102` (`albedo`) and `3DEngine/Components/Camera.cs:22` to
     `25` (`fovY`, `near`, `far`, `targetName`); and a `paramref` to `location` on
     `Engine3D.SetShaderValue(Shader, int, Vector4)` (`3DEngine/Api/Engine3D.Shaders.cs:178`).
     Each is corrected, and the documentation warnings of this kind (CS1572, CS1573, CS1574,
     CS0419 and their neighbors) become errors in the engine's project, so the next one stops
     the build where it is written and not in an annotation nobody reads.
   - The actions run on Node 20, which GitHub has deprecated: `actions/checkout@v4`,
     `actions/setup-dotnet@v4`, `actions/cache@v4` and `actions/upload-artifact@v4` move to
     their current major versions in `test.yml`, `build.yml` and `pack.yml`.
   - `ubuntu-latest` becomes Ubuntu 26 from 2026-10-19. The Linux jobs install lavapipe and the
     validation layer from the runner's packages, so the day the label moves, those versions move
     with it and a run can turn red with no commit to blame. The jobs name `ubuntu-24.04`, and
     moving to 26 is a commit of its own, made when somebody is looking.
2. **TODO.md's order** otherwise.

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push, as CLAUDE.md and COMMITS.md say.
2. **The package is `3DEngine` on nuget.org, the owner's own, numbered from 5.0.** The owner
   sets the major and minor in `build/version.txt`, the patch counts the commits since, and the
   `pack` workflow run by hand makes the package to download or push.

## Replies


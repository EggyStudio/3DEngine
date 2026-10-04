# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `d3f79517`. The pack workflow, `build/version.sh` and `build/version.txt` (`609bd859`)
were read and do what the owner asked for: a patch that counts commits since the major and minor
were last set, and a package made by hand from the Actions tab. The first run on GitHub showed
the Windows job red, which is item 1.

## Now

The owner asked for these, and they come before TODO.md's order. In this order.

1. **Windows is green.** The Windows job failed three tests in
   `3DEngine.Tests/Api/Engine3DAudioTests.cs` (`Looping_Music_Keeps_Half_A_Second_Queued_On_A_Stream_Voice`,
   `Music_That_Does_Not_Loop_Stops_Playing_Once_Its_Queue_Runs_Out` and
   `Seeking_Restarts_The_Voice_At_The_Time_Asked_And_Keeps_A_Pause`) with `The process cannot
   access the file 'blip.wav' because it is being used by another process`, thrown from
   `Dispose` at line 76 as it deletes the test's directory. Each calls `LoadMusicStream` and
   never `UnloadMusicStream`, and music has streamed from its open file since `8a9144b6`, which
   Linux lets a directory be deleted around and Windows does not. The tests unload what they
   load. The engine also closes music still loaded when the app shuts down and names it among
   what was left loaded, as DESIGN.md §6 says of a resource a program forgot, with a test, so a
   program that forgets does not hold its files open either.
2. **A package is made only from a commit green on both systems.** The Linux and the Windows
   test jobs move into one workflow others call (`.github/workflows/test.yml`, on
   `workflow_call`). `build.yml` calls it on a push, and `pack.yml` calls it and packs only after
   it passes, so a run started by hand tests on Linux and Windows and then packs. The package
   stays the run's artifact to download, and nothing goes to nuget.org unless the box is ticked.
3. **The owner is asked what `build/version.txt` holds** (decision 2), in the working session.
   It is not changed here without their word.
4. **TODO.md's order** from there.

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push, as CLAUDE.md and COMMITS.md say.
2. **The package keeps its name, and its version has to pass 4.0.1.** `3DEngine` on nuget.org is
   the owner's own package, from the engine before the redesign, so the id stays and the owner
   uploads to it by hand. An earlier entry here called that package unrelated, which was wrong.
   Its newest version there is 4.0.1, and `dotnet add package 3DEngine` takes the highest, so a
   package numbered `0.1.x` would never be the one installed. The major and minor are the
   owner's to set in `build/version.txt`, and `5.0` is proposed, which makes the next package
   `5.0.0`. The README's source mapping, which keeps nuget.org's package out of a project built
   on a local one, stays until a package from this repository is uploaded there, and is dropped
   from the install steps after.

## Replies

- **Decision 2.** The owner said on 2026-10-04 that the `3DEngine` package on nuget.org is theirs,
  so the id stays and the engine is published under it. The `pack` workflow, run from the Actions
  tab, builds, tests and packs a version from `build/version.txt` and the commits since it changed,
  and pushes it to nuget.org when asked. The README leads with `dotnet add package 3DEngine`, and
  the local pack with its source mapping is the route for a build from a checkout.
- **Decision 1.** The owner confirmed in the working session that they push `main` and the
  session commits locally, and CLAUDE.md and COMMITS.md say so.
- **Decision 3.** The owner admitted NLayer on 2026-10-04. MP3 sounds and music are read through
  it, DESIGN.md §8 lists it, and FLAC stays unread, as TODO.md says.

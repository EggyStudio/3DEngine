# Norm

The rules BevyCSharp and 3DEngine keep, each with a number, a reason and what checks it. It is the
same file in both repositories and is written by the session that writes [REVIEW.md](REVIEW.md).
[SHARED.md](SHARED.md) says what the two engines have, and this says what they keep.

## Scope

The norm covers how each engine is laid out and built: its library, generators, tests, examples,
games, documents, build scripts and workflows, and how work on them is done. What an engine does
is its own design, which the annexes point to.

## Terms

| Term | Meaning |
|---|---|
| the library | The project a game references, `BevyCSharp/` or `3DEngine/` |
| the package | The library as packed for NuGet, with its natives |
| the followed engine | The engine whose way of working a library follows, Bevy for BevyCSharp and raylib for 3DEngine |
| an example | A program in the examples project, opened by name |
| a capture | The picture the workflow takes of an example or a game |
| a game | A project under `games/`, built from the package |
| the workflow | What GitHub runs on a push |
| a batch | One finished piece of work, which is one commit |
| the working session | The session that writes an engine |
| the reviewing session | The session that reads both engines and writes REVIEW.md, SHARED.md and this file |
| the owner | The person both sessions work for |
| a rule's list | The file `build/norm/<number>.txt`, the places that do not yet keep a rule |

## How a rule is kept

A rule is one sentence in bold with its number, then why it is a rule, then what checks it. A
number is never given to another rule, and a rule withdrawn keeps its number and says so.

A check is a test in the class `NormTests` of the engine's test project, named for the rule, as
`N_3_3` is for N 3.3, with a message that begins with the rule's number. It runs with every other
test. Where a build setting or a step of the workflow is the check, the rule names that.

Code that does not keep a rule on the day the rule is written goes on the rule's list, a place a
line, with a few words of why where that helps. The test fails for a place that is not listed and
for a line whose place keeps the rule or is gone, so a list only gets shorter. A place on a list
is mended when a batch next touches it, and a mending that only moves code is a commit of its
own, apart from the batch whose work touched the file.

A rule may leave a kind of place out, and says which. Those places are on its list too, each with
its reason after a tab, and they stay there. A line with no reason is a place to mend.

One test holds this file and the tests to each other. It reads its engine's column of the table
under Conformance. A cell that begins `listed`, or begins `checked` and names `NormTests`, has its
test there, every test in `NormTests` is named for a rule that is here, and every list is the list
of a test.

A rule no machine can check says by review, and the reviewing session holds it when it reads a
commit.

## 1 Layout

**N 1.1 The library is one project with a folder an area, and its namespaces are the ones its
annex lists.** A game finds what it calls behind one `using`, and a second namespace is a decision
somebody made and wrote down. A test reads the built library's public types and fails for a
namespace the annex does not have.

**N 1.2 A public type that is not nested is declared in a file named for it, `Type.cs`, or
`Type.Part.cs` for a part of it.** A type is found by its name with no search, and two batches
meet in one file less often. A test reads the library's sources, with a list.

**N 1.3 A source file has at most 800 lines, generated files apart.** A file is read whole before
it is changed, by a person or by a session, and COMMITS.md has a section for the batches that
meet in a large one. A test counts the lines of the library, the tests, the examples and, in
BevyCSharp, the bridge, with a list.

**N 1.4 A test class is in the folder of the area it tests, the test project laid out as the
library is, and what the tests share is at its root.** A test is found from the code it holds, and
the code from its test. The norm's own class is at the root too. A test of what is no area of the
library, such as the documents or the package, is in a folder named for what it tests and is left
out. A test compares the two trees, with a list.

**N 1.5 Every top folder of the repository, the hidden ones apart, and every top folder of the
library is named in the table of areas in AGENTS.md, by a row of its own or by a row for a folder
within it.** A session reads that table to find where a thing belongs, and a folder it does not
name is found by search or not at all. A project is in a top folder, so the folder's row is the
project's. A test compares the table with the folders.

## 2 The public surface

**N 2.1 A type is public only when a game reaches it, and what is public is a checked-in listing
that a tool writes from the built library.** Each public type is a promise a game may come to rely
on, and a change to the listing is read as a change to that promise. A test fails when the listing
and the library differ.

**N 2.2 Every public member is documented, and one that is not fails the build.** The
documentation is what a game's author sees at the call, and it is carried in the package. The
build treats the compiler's warning for a missing comment, CS1591, as an error in the library.

**N 2.3 Every public call has its line in CHEATSHEET.md.** The cheatsheet is the API on one page,
and it is true only while something holds it. A test compares the names.

**N 2.4 A commit that removes or reshapes a line of the public listing says so under Replies, and
the owner sets the version before the next package.** The patch number counts commits and says
nothing of what broke. By review.

**N 2.5 The library reflects on nothing at runtime that a generator can register, and a game
published native runs in the workflow.** A game published trimmed or native keeps only what the
compiler can see is used. The workflow publishes a game native and plays it.

**N 2.6 A file that is missing, empty, cut short or not what its name says is answered with a
message that names it, and no exception or panic leaves the engine for it.** A game reads files
its players and its artists made. One test gives every loader each kind of bad file, as a table.

**N 2.7 An example compiles on the package alone.** A picture in the README opens an example as
the way to do a thing, so what it calls has to be what a game can call. The workflow builds
examples in a project of their own on the packed package.

**N 2.8 Every package the library references is in the engine's list of packages with what it is
used for, and adding one is the owner's.** A dependency is surface the engine answers for. A test
compares the project file with the list its annex names.

**N 2.9 A loader keeps no file open once a load returns.** A file the engine still holds cannot
be written by the tool that made it, and on Windows cannot be removed, which ended a run of
3DEngine's when a child process had inherited the handle. One test loads through every loader and
finds its file let go, on Linux among the entries of `/proc/self/fd` and on Windows by opening it
for writing with no sharing.

**N 2.10 No exception leaves a callback that native code calls.** An exception that unwinds into
native code ends the process with no message, as a reader's exception inside Assimp ended
3DEngine's test host. Every method handed to native code catches everything and answers the
native side in its own terms, the first exception kept for whoever asked. A test finds a method
handed over that does not.

## 3 Tests

**N 3.1 A fault that is mended has a test that fails without the mend.** A fault with no test
comes back. By review, the commit's description saying what the test caught.

**N 3.2 A test that cannot run on a machine is reported as skipped, with its reason.** A test
that returns early passes, and a run's summary then counts more than ran. By review.

**N 3.3 A test of motion steps time by a set amount and counts frames.** A test that waits on the
machine's clock measures the machine, which 3DEngine's Windows run of `db942962` showed. A test
finds the clocks and the waits in the test project, with a list of those that are about something
outside the frame, each with its reason.

**N 3.4 A test keeps its files in a folder from the one helper, which removes it and says which
process holds what it cannot remove.** A delete in a `finally` puts its own failure in place of
the test's, and names nobody. A test finds temporary folders made or removed anywhere else, with a
list.

**N 3.5 A tolerance or a wait is changed only with the measurement that calls for it, in the
commit's description.** A tolerance widened to pass hides the fault the test found. By review.

**N 3.6 A number in a document names the command that measured it.** A number nobody can measure
again is not kept true. By review.

**N 3.7 A test in which the engine logs an error fails, unless the test says it expects that
error.** A system that throws is logged and the app goes on, so an error logged in every frame
of every headless test stood for thirteen hours in 3DEngine with the suite passing over it. The
test project hears the engine's log and fails the test, with a list of the tests that log an
error on the day the rule is taken.

## 4 Documents

**N 4.1 Prose follows STYLE.md, in comments, messages, documentation and Markdown.** Many
sessions write these files, and they read as one. A test counts the dashes STYLE.md forbids, which
is the part a machine can count, and the rest is by review with STYLE.md's own searches. A file
that names those dashes, or carries the followed engine's own words, is left out.

**N 4.2 A document is in the place of its reader.** The README is for somebody deciding and has at
most 320 lines, `docs/` has a page an area for somebody using the engine, each listed in the
README, the cheatsheet is at the root, and `.github/` is for somebody working on the engine. A
test counts the README's lines and finds every page of `docs/` among its links.

**N 4.3 Every link in the README, the cheatsheet and `docs/` leads somewhere, and a link in the
README or the cheatsheet is a full address.** The README is the package's page too, where a
relative link goes nowhere. A check follows each link, one into the repository against the
checkout with no request made.

**N 4.4 Code a document tells a reader to write is built by the workflow.** A tutorial stops
compiling the day the API moves unless something builds it. The workflow follows the README's
first program in a new project, and a page of steps is held to programs the workflow builds.

**N 4.5 A capture is a WebP file at the size of the followed engine's window, and a picture of an
example in the README opens that example's source.** One size down the gallery, small files, and
a picture that leads to the code that drew it. An example that asks for a window of its own, as
the followed engine's example does, and a game, whose window is its own, are left out of the size.
A test reads each capture's size and each picture's link.

**N 4.6 A rule is stated once, here, and a document that needs it cites its number.** A rule
written in two places comes to say two things. By review.

## 5 Examples and games

**N 5.1 An example has the followed engine's name for it where that engine has one, is opened by
name, and has a capture the workflow takes.** The name says which of the followed engine's
examples it answers, and the capture shows it still draws. The workflow captures every example.

**N 5.2 Every example of the followed engine is a row of a table that a script writes from that
engine's own list.** How much of the followed engine a library carries is counted and not
guessed. The workflow runs the script and fails when what it writes differs from the table
checked in.

**N 5.3 A game is built from the packed package and played by the workflow.** A game built on the
project itself does not find what the package leaves out. The workflow packs, builds a game on the
package and plays it with no display.

## 6 Build and package

**N 6.1 A build has no warnings, and a warning fails the workflow.** A warning left standing hides
the next one. The workflow builds with warnings as errors.

**N 6.2 The tests run on Linux and Windows on every push, and what draws runs under a validation
layer where the device has one.** Path separators, file locking and the names of native libraries
differ between the two, and a validation layer finds what a forgiving driver lets pass. The
workflow.

**N 6.3 A package is made by a workflow run by hand, from a commit whose tests passed, numbered by
`build/version.txt` and the count of commits since it changed.** The owner sets the major and the
minor, and no commit message decides a release. The pack workflow and `build/version.sh`.

**N 6.4 A test opens the packed package and finds what it should hold, the natives for each system
among it.** A package without a native library fails on somebody else's machine. A test.

**N 6.5 The package carries the notices of everything in it that is another's.** The licenses of
what a package is built from ask that their notices go with a copy, a compiled one too.
`THIRD-PARTY-NOTICES.md` is in the package, written by a script where the dependencies are many,
and a test holds it to them.

**N 6.6 A script that more than one system runs uses only what each system's tools read.**
macOS has BSD's tools and bash 3.2, and a script written on Linux fails there on a form only
GNU's tools read, as 3DEngine's pack did on `sed -i` until `d42a5c95`. A test reads the scripts
the jobs on Windows and macOS run, and those they call, for such forms.

**N 6.7 A run that fails says what failed in a page, on each system.** A log of 60,000 lines
names nothing, an annotation saying that a process ended with code 1 names nothing, and such a
log pasted into a session on 2026-10-05 ended the session. The page is at most 200 lines. It
has the failures by cause, each with its message, its first frames and some of its tests, and
the lines the output repeated most. It ends the step's log and is the job's summary and its
annotations, and the step's log is a line for each process and the page, with what the tests
print kept in a file. One script runs the tests and writes the page, in the workflow and for a
working session, and its own tests hold the page to its limits.

**N 6.8 A test process is held to a time and a memory, and one that is lost is found by running
the suite again in parts.** A process that takes the runner's memory takes the runner, which
left three of 3DEngine's runs on Linux with no results. A test that hangs runs for the six
hours a job is given, and a crash ends every test after it. The suite runs whole, since tests
of different areas in one process find faults neither finds alone, as 3DEngine's `c06ec659`
did, and in parts, each a process of its own, only after a process is lost. The workflow's jobs
have a time limit, and the script's tests run it against a process that hangs, one that grows
and one that dies.

## 7 Working

**N 7.1 Commits stay local, and the owner pushes.** The owner decides what is published and when.
By review.

**N 7.2 A commit is one finished batch, and its message has the form COMMITS.md gives.** The
history is read by its descriptions, and release notes are written from them. A test reads the
messages back, from the commit that adds the test on. A commit that changes `build/version.txt`
alone is the owner's setting of the version and is left out, whatever its message, and any other
commit of the owner's is left out on the rule's list with that reason.

**N 7.3 REVIEW.md, SHARED.md and this file have one writer, the reviewing session, and the working
session writes under Replies.** Two sessions editing one file write over each other. By review.

**N 7.4 AGENTS.md changes on the owner's word, apart from a row of its table of areas that N 1.5
asks for.** It is what every session reads first. By review.

## Conformance

`checked` names what checks the rule. `listed` is checked with that many places to mend on the
rule's list, what the rule leaves out being counted apart. `to take` has no check yet, whether or
not the code keeps the rule. `by review` is held by the reviewing session.

| Rule | 3DEngine | BevyCSharp |
|---|---|---|
| N 1.1 | checked, `NormTests` | checked, `NormTests` |
| N 1.2 | listed 116, `NormTests` | listed 347, `NormTests` |
| N 1.3 | listed 6, `NormTests` | listed 32, `NormTests`, 18 of them in the bridge |
| N 1.4 | checked, `NormTests`, 7 left out | listed 113, `NormTests`, 20 left out |
| N 1.5 | listed 3, `NormTests`, their rows waiting for the owner | checked, `NormTests` |
| N 2.1 | checked, `PublicSurfaceTests` and `PublicApi.txt` | to take |
| N 2.2 | checked, CS1591 an error in `3DEngine.csproj` | to take |
| N 2.3 | checked, `CheatsheetTests` | checked, `CheatsheetTests` |
| N 2.4 | by review | by review |
| N 2.5 | checked, `build/play-native.sh` in the workflow | to take |
| N 2.6 | checked, `BadFileTests` | to take |
| N 2.7 | checked, `build/examples-on-package.sh` in the workflow | to take |
| N 2.8 | checked, `NormTests` | to take |
| N 2.9 | checked, `FileHandleTests` | to take |
| N 2.10 | checked, `NormTests` | to take |
| N 3.1 | by review | by review |
| N 3.2 | by review | by review |
| N 3.3 | checked, `NormTests`, 10 left out | checked, `NormTests`, 4 left out |
| N 3.4 | checked, `NormTests` | listed 55, `NormTests` |
| N 3.5 | by review | by review |
| N 3.6 | by review | by review |
| N 3.7 | to take | to take |
| N 4.1 | checked, `NormTests`, 2 left out | checked, `NormTests`, 4 left out |
| N 4.2 | checked, `NormTests` | checked, `NormTests` |
| N 4.3 | checked, `DocumentLinkTests` | checked, `build/check-docs.py` in the workflow |
| N 4.4 | checked, `build/readme-walk.sh` and `FirstGameTests` | checked, `build/readme-walk.sh` |
| N 4.5 | checked, `NormTests`, 8 left out | checked, `NormTests`, 5 left out |
| N 4.6 | by review | by review |
| N 5.1 | checked, the workflow's capture of every example | checked, the workflow's capture of every example |
| N 5.2 | checked, `build/examples-table.py --check` in the workflow | to take |
| N 5.3 | checked, the workflow's games | checked, the workflow's Courtyard |
| N 6.1 | checked, `-warnaserror` in the workflow | to take |
| N 6.2 | checked, `test.yml` | checked, `package.yml` |
| N 6.3 | checked, `pack.yml` and `build/version.sh` | checked, `pack.yml` and `build/version.sh` |
| N 6.4 | checked, `PackageContentsTests` | checked, `NormTests` on the packed package |
| N 6.5 | checked, `PackageContentsTests` | checked, by the test of N 6.4, which holds the notices to the lock |
| N 6.6 | checked, `ScriptTests` | to take |
| N 6.7 | to take | to take |
| N 6.8 | to take | to take |
| N 7.1 | by review | by review |
| N 7.2 | checked, `NormTests`, 1 left out | checked, `NormTests` |
| N 7.3 | by review | by review |
| N 7.4 | by review | by review |
| B 1 | | checked, `NativeLoader` at load |
| B 2 | | by review |
| B 3 | | listed 2, `NormTests`, 15 left out |
| B 4 | | by review |

## How the norm changes

The owner decided this on 2026-10-05. The reviewing session adds a rule alone when the rule comes
with its check and no existing code breaks it. A rule that existing code breaks, or that touches
what is the owner's (dependencies, versions, AGENTS.md, what is published), waits for the owner's
word. A rule that existing code breaks comes with its list, and no code is rearranged to meet a
rule on the day it is written.

A working session that finds a rule wrong, or a fault of a kind no rule names, says so with a line
under Replies in REVIEW.md beginning `Rule:`.

## Annex A, 3DEngine

The followed engine is raylib, whose window is 800 by 450.

The engine's own design is [DESIGN.md](DESIGN.md), whose sections are cited as D 1 to D 11 and
are rules of this engine with their reasons there: one flat API (D 1), the frame (D 2), immediate
drawing (D 3), ImGui inside the frame (D 4), the ECS underneath (D 5), resources the program owns
(D 6), no editor (D 7), dependencies (D 8), one project (D 9), the cheatsheet (D 10) and what is
public (D 11).

Its namespaces are `Engine`, for everything a program calls, and `Engine.Files.Compiler`, which is
internal and compiles behaviors while a program runs. Its list of packages is the table in D 8.

## Annex B, BevyCSharp

The followed engine is Bevy 0.19.1, whose window is 1280 by 720.

Its namespaces are `Bevy`, for what a game calls, `Bevy.Reflected`, for the wrappers the generator
writes over Bevy's own components, `Bevy.Interop`, for the bridge's entry points and what crosses
it, and `Bevy.Physics`, for the physics, which is this library's own and not Bevy's. Its list of
packages is to be written, in BUILDING.md, with the library's packages and the bridge's crates and
what each is used for.

**B 1 The managed half and the bridge carry the same ABI number, and a bridge with another is
refused at load with a message that says so.** A structure read with the wrong layout is memory
read wrong. `NativeLoader` compares the two as the library loads.

**B 2 After a change under `native/`, all three profiles compile, headless, render and editor.**
Most of the bridge is behind a feature, and the smallest profile compiles paths the others leave
out. By review, the workflow testing the headless and the render profile.

**B 3 No panic crosses the C ABI.** A panic that unwinds into managed code ends the process with
no message. Every entry point runs under the guard in `interop.rs`, and a test finds an
`extern "C"` function that does not. One that only answers a constant, a flag or a count is left
out.

**B 4 An example keeps on an entity what Bevy's example keeps on an entity, in a behavior.** The
README's first program is a behavior, and a picture opens an example as the way to do a thing. A
static field is for what Bevy keeps in a resource or a `Local`. By review, EXAMPLES.md counting
the examples that do.

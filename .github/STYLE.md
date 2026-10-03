# Style

Conventions for prose in this repository: code comments, XML documentation, log and exception
messages, MSBuild and YAML comments, shader comments, and Markdown. They exist so that the codebase
reads consistently regardless of who wrote a given file.

## Punctuation

Em dashes and en dashes are not used. Neither `—` nor `–`, in any file.

The spaced hyphen ` - ` is not sentence punctuation. Replace it according to the relationship
between the clauses:

| instead of | write |
|---|---|
| `the world is borrowed - only on the main thread` | `the world is borrowed, only on the main thread` |
| `it is two thoughts - here is the second` | `it is two thoughts. Here is the second` |
| `not uploaded yet - only meshes are - so this fails` | `not uploaded yet (only meshes are), so this fails` |
| `driven from elsewhere - a menu, a test` | `driven from elsewhere: a menu, a test` |

A comma continues a clause, a full stop separates two statements, parentheses enclose an aside,
and a colon introduces a list.

Hyphens within words (`backend-agnostic`), arithmetic operators (`count - 1`) and the names of
characters (`the minus key`) are unaffected.

The colon introduces a list or labels what follows. It is not a joint between two clauses, and a
statement followed by its reason is two sentences or one sentence with `because` in it. Used as a
joint it becomes a tic, because every paragraph then acquires the same shape and the reader stops
reading the mark as anything at all.

The test is the text after the mark. The colon is for a list, or a phrase naming the thing just
mentioned. A clause that could stand on its own and explains what precedes it is a joint, whatever
it is about, so `a tag is cheap: it costs nothing to store` is wrong where
`a tag is cheap, because it costs nothing to store` is right.

| instead of | write |
|---|---|
| `the wheel never moves the panel: rolling over a list hands it to whatever holds it` | `the wheel never moves the panel, because rolling over a list hands it to whatever holds it` |
| `a click is the engine's to answer: it raycasts the scene` | `a click is the engine's to answer. It raycasts the scene` |
| `three files, because they are edited by different hands: the first is` | `three files, because they are edited by different hands. The first is` |
| `it offers three: a menu, a button, a key` | unchanged, because a list follows |
| `**What is not here.** Despawning is not recorded` | unchanged, because the mark labels rather than joins |

## Section banners

A banner names its section and ends:

```csharp
// -- Hierarchy
```

Trailing runs of dashes are not used. They carry no information, and their length is not
reproducible across edits.

## Comments

A comment that restates the code is a maintenance liability, because it has to be kept true as
the code changes. Remove it.

```csharp
// Incorrect: restates the line below it.
// Adds the time plugin so Time is updated every frame.
app.AddPlugin(new TimePlugin());

// Correct: no comment.
app.AddPlugin(new TimePlugin());
```

A comment is warranted when the reader would otherwise ask why, and the answer is not visible in
the surrounding code:

- a constraint that is not apparent locally
- a decision, together with the alternative that was rejected
- a hazard
- a safety argument for an `unsafe` block or a Vulkan synchronization choice

Where a comment would be needed to explain what the code does, renaming is usually the better
correction.

## Tone

Prose is factual and plain.

Headings and rule names are descriptive rather than metaphorical or aphoristic. Use "Comments",
not "Comments earn their place".

Marketing register is not used. This includes the rhetorical triple:

> No registration call, no partial-class list, no startup boilerplate. Add the package, write
> behaviors, compile, run.

State what the software does instead:

> Behaviors are discovered at compile time, so a consuming project needs no registration code.

The words `simply`, `just`, `powerful` and `blazing` are not used, nor are exclamation marks.
`easy` is not used to describe working with the software. It is acceptable in a warning, as in
"easy to get subtly wrong", where it tells the reader something they need.

Antithesis is not used for emphasis. A claim paired with the negation of something nobody
proposed reads as a slogan, and the negated half carries nothing:

> Discovery is generated, not scanned.

State the claim on its own:

> Registration happens at assembly load.

The construction is warranted where a reader would otherwise assume the negated alternative, and
correcting that assumption is the point of the sentence, as in "`DrawCube` records a cube for the
frame, not a mesh that outlives it".

Limitations are stated directly. "Not implemented yet" is preferable to "coming soon".

### Saying what things are for

A sentence names its subject and says what it does. The cleft forms `X is what Y needs`,
`which is what Y wants`, `that is what Y is for` and `this is what makes it work` route a plain
statement through a relative clause and read as a mannerism once they recur, which they do, because
every explanation can be forced into that shape. Code and features do not want things either, so
`a swapchain wants a new size` is written as the requirement it is.

| instead of | write |
|---|---|
| `a depth buffer is what an outline is made of` | `an outline is drawn from the depth buffer` |
| `which is what the shadow pass needs` | `which the shadow pass needs` |
| `that is what the timestamp queries are for` | `the timestamp queries measure that` |
| `this is what lets a reload keep its values` | `so a reload keeps its values` |
| `a scene of many cubes wants instancing` | `a scene of many cubes benefits from instancing` |
| `what the caller wanted` | unchanged, because a person wants things |

The same applies to `what X does is Y`, `the point is that`, and `the reason is that`, which delay
the statement they introduce.

## Self-reference

Prose states what is true. It does not describe its own history, and it does not narrate the work
that produced it.

| instead of | write |
|---|---|
| `Scene loading is not blocked, contrary to what this file said before` | `Scene loading works` |
| `The question the old entry raised is settled: parts are addressed individually` | `Parts are addressed individually` |
| `Recorded here so the approach is settled when it comes up` | delete the sentence |
| `Cameras take their common parameters now` | `Cameras take their common parameters` |
| `The bug this was written to explain is fixed now` | delete the sentence |

A reader has no access to the previous version of a file, so a correction phrased against it says
nothing. Rewrite the passage as a plain statement and let the diff carry the change.

The words `now`, `already` and `no longer` are the usual signals. Each is fine where it
distinguishes two states the reader can see, as in "the handle reports `Loading` until the file
has been read", and wrong where it only means "since the last edit".

The same applies to `TODO.md`. An item is a description of outstanding work, so completed work is
removed from it or the entry is rewritten around what remains. It is not annotated as done, struck
through, or kept for the record.

A list of gaps is a claim about the software in the same way the rest of the prose is, so closing
a gap means rewriting the entry around what is left of it. `TODO.md` and the README's status
section describe the same software and are kept consistent with each other.

## References

Prose refers only to what a reader of the repository can open. An ignored directory, a path that
exists on one machine, a private branch or an internal ticket tells them nothing.

`.ref/` is ignored by git, so it is not cited. Where something in it informed a decision, state
the decision and the reasoning, which is the part worth keeping:

| instead of | write |
|---|---|
| `.ref/raylib has a version to follow. Its shape:` | `One flat function per operation, so that no handle has to be set up first:` |
| `as the reference engine does it` | describe the approach |

Public sources are citable: a package on nuget.org, a type in a dependency, an upstream issue, the
raylib cheatsheet on raylib.com. So is anything checked in, by repository-relative path.

## Spelling

American spelling, everywhere, in prose as well as in code: `behavior`, `color`, `center`,
`gray`, `meter`, `neighbor`, `initialize`, `serialize`, `canceled`, `modeled`, `toward`. The public
API is spelled that way, and so are .NET, Vulkan, SDL and raylib, and a word spelled one way in a
type name and another in the sentence describing it reads as two words.

A name that comes from outside is kept as it is, such as .NET's `StringMarshalling`, a file name in
a third-party icon set, or the text of a license.

## Scope

- `//` and `///` in C#, and `//` in Slang shaders
- exception messages, log output and `GITHUB_STEP_SUMMARY` content
- MSBuild `<!-- -->` comments and analyzer `messageFormat` strings
- YAML comments under `.github/workflows`
- `README.md`, `CLAUDE.md`, every Markdown file under `.github/`, the skills under
  `.claude/skills/` and this file

## Checks

```bash
# Em and en dashes.
grep -rn "[—–]" --include=*.cs --include=*.slang --include=*.md --include=*.yml . | grep -v "^\./\.ref/"

# Spaced hyphens in comments and strings.
grep -rn "^\s*\(///\|//\) .* - \|\"[^\"]* - [^\"]*\"" --include=*.cs --include=*.slang . | grep -v "^\./\.ref/"

# Colons joining two clauses in a comment. A list or a label after the mark is correct, so the
# hits are read rather than counted.
grep -rn "^\s*\(///\|//\) .*[a-z]: [a-z]" --include=*.cs --include=*.slang . | grep -v "^\./\.ref/"

# Padded section banners.
grep -rn "// -- .*--" --include=*.cs --include=*.slang . | grep -v "^\./\.ref/"

# Cleft sentences and things that want. People want things, so each hit is read.
grep -rniE "\b(is|are|was|were) what\b|which is what|that is what|this is what|\bwants?\b" --include=*.cs --include=*.slang --include=*.md . | grep -v "^\./\.ref/"

# Prose about earlier revisions.
grep -rniE "contrary to what|this (file|document|entry) (said|used to)|the (old|previous) (entry|version)|at the time .* was written" --include=*.md . | grep -v "^\./\.ref/"

# References to paths that are not checked in.
grep -rn "\.ref/" --include=*.cs --include=*.md . | grep -v "^\./\.ref/" | grep -v "^\./\.github/STYLE.md"
```

Arithmetic such as `counts[i - 1]` is the expected false positive in the second command.

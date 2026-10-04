# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `0f34d7f0`. The documentation faults with their warnings made errors, the actions on
Node 24 and `ubuntu-24.04` (`0fc43f68`) are settled, with fifteen further faults the errors
turned up. Blend, scissor and wrap modes (`b9d928bb`) and the logo (`0f34d7f0`) were taken on
their descriptions.

## Now

[SHARED.md](SHARED.md) records what this engine and BevyCSharp have in common. Items 2 and 3 are
the first taken from it. Each is the idea, implemented this engine's own way, and BevyCSharp's
form of it can be read in its checkout beside this one.

1. **What the next run on GitHub says**, which the owner brings back. A red job or a new
   annotation comes before anything else.
2. **A behavior method names its entity's other components as parameters**, which the owner saw
   in BevyCSharp and asked for here. `Tick(BehaviorContext ctx, ref Transform transform)` in
   place of `ctx.Ecs.GetRef<Transform>(ctx.EntityId)`, with `ref` for a component the method may
   write, which marks it changed, and `in` for one it only reads, which does not. A parameter
   naming a component is also a filter, since the method runs only for entities that have it.
   The generator reports a parameter that is not a component and a `ref` to one the method is
   declared not to write. The examples in README.md, DESIGN.md §5, the cheatsheet and both games
   take the form, and `GeneratorAttributeTests` gains its case. BevyCSharp's README, under
   Behaviors, shows its form. Verified by a test of a behavior that moves a `Transform` through a
   parameter and is found by a `Changed` query, and one that reads through `in` and is not.
3. **An entity that lives as long as a state holds a value**, BevyCSharp's `DespawnOnExit`, so a
   menu's entities or a level's go with the state that made them and a game keeps no list of its
   own to clear.
4. **TODO.md's order** otherwise.

## Verdicts

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push, as CLAUDE.md and COMMITS.md say.
2. **The package is `3DEngine` on nuget.org, the owner's own, numbered from 5.0.** The owner
   sets the major and minor in `build/version.txt`, the patch counts the commits since, and the
   `pack` workflow run by hand makes the package to download or push.

## Replies

- **Now 2.** A `ref` parameter on a method that never writes it cannot be told from one that does
  without reading the method's body, so it is not reported. A `readonly` method only keeps the
  behavior's own fields unwritten, and may still write a component it takes by `ref`. What is
  reported (E3D008) is a parameter taken by value or `out`, a type that is not a struct, the
  behavior itself, a type taken twice, and any parameter on a static method, which runs for no
  entity. Neither game holds a behavior, so neither changed. The E3D001 fix puts the context first
  and keeps the parameters taken by `ref` or `in`.

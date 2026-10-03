# The editor

What the editor is for, how it is built, and the order it is built in. It is a plan. The editor of
an earlier revision ran a Blazor server inside the engine and drew it through an embedded browser,
and it is kept on the `legacy-modules` branch. This one is Dear ImGui, drawn by the engine's own
ImGui pass, and nothing else.

## What it is for

Looking at a running world and changing it: listing the entities, editing their components,
placing models, saving the result as a scene, and pressing Play. A game that never opens the editor
does not pay for it, because the editor is a separate project that references the engine.

## What it is

**The editor is an ordinary 3DEngine app.** It opens a window with `InitWindow`, adds `EditorPlugin`
to `Engine3D.App`, and runs the same loop any game runs. Everything it draws is ImGui between
`BeginDrawing` and `EndDrawing`, and everything it changes it changes through `EcsWorld`, so a
feature the editor needs from the engine is a feature every game gets.

**A panel is a function.** It runs every frame and draws what is true then. There is no widget tree
to build and nothing to invalidate, so a panel cannot be out of step with the world it shows.

## The shape of it

`EditorShell` owns the arrangement and nothing else: whether the side panel is docked, how wide it
is and which bottom tab is open. Every rectangle follows from those numbers each frame, and the
parts read them and draw.

| part | what it draws |
|---|---|
| `WorldPanel` | the entities, as a tree by parent, with search and selection |
| `DetailsPanel` | the components of what is selected, through the inspector |
| `ConsoleTab` | the log, and a line to type commands into |
| `AssetsTab` | the files under the asset root, as a grid that can be dragged into the scene |
| `SettingsTab` | the editor's and the game's settings |
| `SceneView` | the camera's picture, the gizmo for the selection, and the toolbar over it |

## The inspector

A component is drawn from a schema the source generator emits for every `[Behavior]` struct and
every component struct it sees. Each field carries a closure that reads it and one that writes it,
so the inspector reflects nothing at runtime and survives trimming. What a field is drawn as follows
from its type and from attributes on it: `[Range]`, `[Label]`, `[Tooltip]`, `[ReadOnly]`, `[Hidden]`,
`[Header]`, `[Color]` and `[Button]` on a parameterless method. A write goes through `EcsWorld.Set`
so change detection sees it.

## History

`EditorHistory` keeps two stacks of edits, each a name and a pair of closures that undo and redo
it. Consecutive edits with the same key coalesce, so dragging a number is one step. Only what can be
reversed exactly is recorded, so despawning is not, until the scene format can hold the entity it
removed.

## Theme

`EditorTheme` is a record of every color and metric the editor uses, applied to `ImGui.GetStyle()`
and written to `editor/theme.txt`. Hierarchy is shown by fill rather than borders, with the ground, the
panel, a card and a field each a step lighter than what they lie on, and one accent color marks
what is selected.

## Tables a game adds to

Everything the editor offers is a row in a table, and each table is one line a game writes:

```csharp
EditorMenu.Command("Spawn/Enemy", world => Spawn(world, "Enemy"), order: 40, keys: "Ctrl+E");
EditorShell.Tabs.Add(new EditorTab("Profiler", ProfilerTab.Draw));
EditorSettings.Number("My game", "Enemy speed", () => Speed, value => Speed = value);
```

## What the engine has to grow

- **Entity names and a hierarchy** in `EcsWorld`, so the world panel has something to list.
- **Component schemas** from the generator, for the inspector.
- **Scenes as JSON**, written and read without reflection, for Save, Load and Play.
- **A viewport rectangle** for the main camera, so a docked panel shrinks the picture instead of
  covering it.
- **Picking** by ray against mesh bounds, then against triangles.
- **`TakeScreenshot`**, so a change to the editor's look can be checked by comparing two pictures.

## Order

1. `EditorPlugin`, the shell and the world panel over entity names.
2. The details panel with schemas for the built-in components, then for behaviors.
3. History, then the console with commands.
4. The scene view with a free camera, picking and a translate gizmo.
5. Scenes as JSON, then Play, which runs a copy of the world and throws it away on Stop.
6. The assets tab, then the theme and settings.

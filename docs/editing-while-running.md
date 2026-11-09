# Editing while it runs

A program in development loads again, in place, the files it loaded by name as they are saved, so a
shader, a texture, a model, a level or a behavior is changed in its editor and seen in the running
program at the next frame, with no restart. The engine's own code and shaders are not reloaded, and
a change to them needs the program run again.

## Where it holds

A program is live where it runs from its own project's build folder,
`bin/<configuration>/<framework>` under the project that builds it, as `dotnet run` and an editor's
run start it, and not where it runs from anywhere else, as a shipped game does. Its config and the
command line say otherwise:

```csharp
InitWindow(800, 450, "editing");          // live under dotnet run, not once shipped

var config = Config.Default with { Live = false };   // never, for an app made by hand
```

`--live` and `--no-live`, or `E3D_LIVE` set on or off, say it from the command line over the config.

A program run from its build folder watches the project's copy of each file where the project has
one at the same place, since that is the copy an editor saves and the build copies only at the next
build, and the file it read otherwise.

## What is loaded again

| Loaded by | Saved, it | And |
|---|---|---|
| `LoadShader` | is compiled again by slangc and drawn with from the next frame, under the same handle | a save that does not compile keeps the shader as it was, slangc's reason in the log |
| `LoadTexture` | is read again and drawn from the next frame, under the same handle, at its new size where it has one | the `Texture2D` the program holds keeps the size it was given, so a drawing that takes its size from it draws the old one |
| `LoadModel` | is read again into the `Model` the program holds, its meshes, materials and bones the file's as it is now | a shader the program set on a material stays on it, and a texture the file names is loaded again on its own as one `LoadTexture` read is |
| `LoadScene` | is spawned again, and the entities it spawned before are despawned with their children once it has | a save that cannot be read keeps the level, and the handles the program was given before are refused from then on |
| a behavior in `source/behaviors` | is compiled again into the running world ([Behaviors and the ECS](behaviors-and-the-ecs.md)) | |

A file is read once it has been quiet a moment, since an editor saves in more than one write, and
each reload is said in the log under `Engine.Live`. An asset server's handles are reloaded too,
with the scenes spawned from them.

## Seeing it from the command line

`e3d`'s `live` command says whether the program is live and lists each file it watches, a line each
with its kind, its id, the file and its last reload, and `live on` or `live off` turns it for the
run:

```bash
./e3d command live
./e3d command live off
```

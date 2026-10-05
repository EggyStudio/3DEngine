# 3DEngine templates

Templates for a game on [3DEngine](https://github.com/EggyStudio/3DEngine), a C# engine used the
way raylib is.

```bash
dotnet new install 3DEngine.Templates
dotnet new 3dengine -n MyGame && cd MyGame
dotnet run
```

`dotnet new 3dengine` makes a program of plain calls, a window, a loop and a cube, and
`dotnet new 3dengine-ecs` makes one whose state is in behaviors, with a script the running game
compiles again when it is saved. Each asks for the version of the engine packed with these
templates, and `--package-folder <folder>` writes a `nuget.config` that takes the engine from a
folder of packages built from a checkout.

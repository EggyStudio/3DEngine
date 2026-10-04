# Shipping a game

A game is a program of its own outside this repository, built against the engine's package and
shipped as a folder a player runs. This page follows `games/Summit`, which is built that way.

## A program on the package

A game references the package as any NuGet package, and carries its art beside it:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="3DEngine" Version="0.1.0-*" />
    </ItemGroup>
    <ItemGroup>
        <Content Include="resources\**\*" CopyToOutputDirectory="PreserveNewest" />
    </ItemGroup>
</Project>
```

Files under `resources` are found beside the program, by `LoadModel`, `LoadSound`, `LoadScene` and
a scene's `ModelRef` and `SceneRef` alike. `games/Summit` builds against a package made in this
repository by `build/pack.sh`, through a `nuget.config` naming that folder, as
[BUILDING.md](../.github/BUILDING.md#a-program-on-a-local-package) shows.

## One native executable

`dotnet publish` makes the folder a player runs, with the .NET runtime in it, or one native
executable through native AOT, which starts at once and needs nothing installed:

```bash
dotnet publish -c Release -r linux-x64 -p:PublishAot=true -o publish    # or win-x64, osx-arm64
```

Summit's executable is 11 MB, beside the native libraries SDL3, Assimp and Dear ImGui bring, its
`resources` and the engine's shaders compiled ahead in `source`. Its level from scene files, its
physics, its console commands and its music all run native, since behaviors, scene components and
commands register through code the generator writes, with nothing found by reflection a trimmer
could break. Behavior scripts compiled while the game runs are left out of a native build, which
runs the behaviors compiled into it.

A shader of the game's own is compiled ahead into the same `source/.slang-cache` with
`e3d shaders <folder> <cache>`, so a player needs no `slangc`.

## Before shipping

`./e3d open` runs the published executable as it runs an example, so a game is driven and captured
from the terminal the same way, and CI plays each game from a freshly made package with the
validation layer on, as `build.yml` shows.

## See also

- The game [`games/Summit`](../games/Summit/Program.cs), and the smaller
  [`games/Pusher`](../games/Pusher/Program.cs) and [`games/Hopper`](../games/Hopper/Program.cs)
- [BUILDING.md](../.github/BUILDING.md#shipping-a-game), on the package, publishing and what a
  native build leaves out
- Previous: [Driving a program with e3d](driving-with-e3d.md)

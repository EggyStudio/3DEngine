# Third-party notices

3DEngine is under the Mozilla Public License 2.0 (`LICENSE`). The `3DEngine` package carries the
engine's own code, its generator, the shaders compiled from its own sources and three of Bevy's
tonemapping tables, below, and it depends on the packages below, which a restore fetches with their own license files. They are named here
with their licenses, so a game shipped on the engine can say what it carries.

| Library | Used for | License | Source |
|---|---|---|---|
| SDL3-CS and SDL3-CS.Native | The window, input, gamepads and audio, over SDL3 | zlib | https://github.com/edwardgushchin/SDL3-CS |
| SDL3, inside SDL3-CS.Native | The same, natively | zlib | https://github.com/libsdl-org/SDL |
| Vortice.Vulkan | Vulkan's bindings | MIT | https://github.com/amerkoleci/Vortice.Vulkan |
| Twizzle.ImGui-Bundle.NET | Dear ImGui's bindings and its native build, cimgui | MIT | https://github.com/JoeTwizzle/ImGui.NET |
| Dear ImGui, inside it | The interface, and the default font, ProggyClean | MIT | https://github.com/ocornut/imgui |
| AssimpNetter | Models read from files | MIT | https://github.com/Saalvage/AssimpNetter |
| Assimp, inside AssimpNetter | The same, natively | BSD 3-Clause | https://github.com/assimp/assimp |
| BepuPhysics and BepuUtilities | Rigid bodies, characters and vehicles | Apache 2.0 | https://github.com/bepu/bepuphysics2 |
| StbImageSharp | Images read from files | Public domain or MIT | https://github.com/StbSharp/StbImageSharp |
| NVorbis | Ogg Vorbis sound read from files | MIT | https://github.com/NVorbis/NVorbis |
| NLayer | MP3 sound read from files | MIT | https://github.com/naudio/NLayer |
| Microsoft.CodeAnalysis.CSharp | Behavior scripts compiled while a game runs | MIT | https://github.com/dotnet/roslyn |

The shaders in the package were compiled with Slang (Apache 2.0 with LLVM exceptions,
https://github.com/shader-slang/slang), which the package does not carry. The Vulkan loader is
the system's, or MoltenVK's on macOS, and is not carried either.

## Bevy's tonemapping

The package carries three tables of Bevy's (https://github.com/bevyengine/bevy), from its crate
`bevy_core_pipeline` 0.19.1, under Bevy's MIT or Apache 2.0, whose two texts are beside them in
`source/shaders/tonemapping`, and `composite.slang` works out four of Bevy's curves as that crate's
`tonemapping_shared.wgsl` does, under the same licenses. The tables are carried as Bevy has them,
their supercompression swapped from Zstandard to zlib by `build/bevy-luts.py`, with Bevy's
`info.txt`, which says how each was made, and Bevy credits them and the curves to these authors:

| Table or curve | By | Source |
|---|---|---|
| AgX's table, `agx.ktx2`, for `Tonemap.AgX` | Troy Sobotka, as MrLixm's AgXc gives it | https://github.com/sobotka/AgX, https://github.com/MrLixm/AgXc |
| Tony McMapface's table, `tony_mc_mapface.ktx2` | Tomasz Stachowiak | https://github.com/h3r2tic/tony-mc-mapface |
| Blender's filmic table, `blender_filmic.ktx2` | Blender's Filmic view transform, sampled in Blender | https://www.blender.org |
| The ACES fit, `Tonemap.AcesFitted` | Stephen Hill, from Matt Pettineo's BakingLab | https://github.com/TheRealMJP/BakingLab |
| The somewhat boring display transform, `Tonemap.SomewhatBoring` | Tomasz Stachowiak | https://github.com/bevyengine/bevy |

The examples in `3DEngine.Examples` that carry raylib's names are raylib's examples
(https://github.com/raysan5/raylib/tree/master/examples, zlib) written again for the flat API, each
naming its authors at its head, and are not in the package. The files of raylib's they load are
fetched by `build/fetch-raylib-resources.sh` under the licenses its examples' `resources/LICENSE.md`
files give, and are not kept in this repository.

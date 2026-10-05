# Third-party notices

3DEngine is under the Mozilla Public License 2.0 (`LICENSE`). The `3DEngine` package carries only
the engine's own code, its generator and the shaders compiled from its own sources, and it depends
on the packages below, which a restore fetches with their own license files. They are named here
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

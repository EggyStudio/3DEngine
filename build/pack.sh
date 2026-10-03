#!/usr/bin/env bash
# Builds the engine in Release, compiles its shaders into a cache with `e3d shaders`, and packs
# the library, the shaders and that cache into build/package. A game built from the package then
# loads the built-in shaders with no slangc of its own.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet build 3DEngine.slnx -c Release
rm -rf build/shader-cache
./e3d shaders 3DEngine/Shaders build/shader-cache
dotnet pack 3DEngine/3DEngine.csproj -c Release --no-build -o build/package

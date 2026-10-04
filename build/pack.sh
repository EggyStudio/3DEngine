#!/usr/bin/env bash
# Builds the engine in Release, compiles its shaders into a cache with `e3d shaders`, and packs
# the library, the shaders and that cache into build/package. A game built from the package then
# loads the built-in shaders with no slangc of its own.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet build 3DEngine.slnx -c Release
rm -rf build/shader-cache
./e3d shaders 3DEngine/Shaders build/shader-cache
# Each pack is a version of its own, stamped with the time, since NuGet keeps the first package of
# a version it sees and never reads it again. A game asks for the newest with a floating version
# such as 0.1.0-*.
version="0.1.0-preview.$(date -u +%Y%m%d%H%M%S)"
dotnet pack 3DEngine/3DEngine.csproj -c Release --no-build -o build/package -p:Version="$version"
echo "packed 3DEngine $version into build/package"

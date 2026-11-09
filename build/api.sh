#!/usr/bin/env bash
# Writes 3DEngine/PublicApi.txt again from the built engine, every public type and member a game
# can reach, once a change to them is meant, and 3DEngine/Graphics/DeviceSurface.txt, what the rest
# of the engine reaches on the Vulkan device. The suite fails while a listing and the assembly
# differ, so a commit that changes a surface carries the change to its file, where it is read.
set -euo pipefail
cd "$(dirname "$0")/.."

ENGINE_WRITE_API=1 dotnet test 3DEngine.Tests --filter "FullyQualifiedName~PublicSurfaceTests.The_Public_Surface|FullyQualifiedName~DeviceSurfaceTests.The_Device_Surface" -v q
git --no-pager diff --stat -- 3DEngine/PublicApi.txt 3DEngine/Graphics/DeviceSurface.txt

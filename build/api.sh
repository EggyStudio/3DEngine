#!/usr/bin/env bash
# Writes 3DEngine/PublicApi.txt again from the built engine, every public type and member a game
# can reach, once a change to them is meant. The suite fails while the listing and the assembly
# differ, so a commit that changes the surface carries the change to this file, where it is read.
set -euo pipefail
cd "$(dirname "$0")/.."

ENGINE_WRITE_API=1 dotnet test 3DEngine.Tests --filter "FullyQualifiedName~PublicSurfaceTests.The_Public_Surface" -v q
git --no-pager diff --stat -- 3DEngine/PublicApi.txt

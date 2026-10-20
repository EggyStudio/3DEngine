#!/usr/bin/env bash
# Writes the lists of CHEATSHEET.md again from the flat API's source, each function a line of its
# declaration and the first sentence of its summary, the page's other text kept as it is. It runs
# the test that holds the page to the source with ENGINE_WRITE_CHEATSHEET set, as build/api.sh
# writes the surface listing, so a function added, changed or documented anew reaches its line.
set -euo pipefail
cd "$(dirname "$0")/.."

ENGINE_WRITE_CHEATSHEET=1 dotnet test 3DEngine.Tests --filter "FullyQualifiedName~CheatsheetTests.The_Lists_Are_What_The_Script_Writes" -v q
git --no-pager diff --stat -- CHEATSHEET.md

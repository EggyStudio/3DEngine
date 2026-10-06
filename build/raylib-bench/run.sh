#!/usr/bin/env bash
# Builds raylib from source with its SDL3 backend, against the SDL3 library the engine already
# brings, and runs its bunnymark and a cube count beside this engine's textures_bunnymark and
# models_stress, each searching for the count that holds 60 frames a second in a hidden window.
# raylib is built here for the measurement only and is not a dependency. The comparison and the
# numbers are in docs/compared-with-raylib.md.
#
#   build/raylib-bench/run.sh [work directory]
set -euo pipefail
cd "$(dirname "$0")/../.."
here="$PWD/build/raylib-bench"
work="${1:-$(mktemp -d)}"
mkdir -p "$work"

# The raylib and SDL the numbers were taken with.
raylib_commit=30fa673ef2fa137588cf8b5732c65d76d86c1570
sdl_release=release-3.4.2
[ -d "$work/raylib" ] || { git clone -q https://github.com/raysan5/raylib.git "$work/raylib"; git -C "$work/raylib" checkout -q "$raylib_commit"; }
[ -d "$work/sdl" ] || { git clone -q --depth 1 --filter=blob:none --sparse --branch "$sdl_release" https://github.com/libsdl-org/SDL.git "$work/sdl"; git -C "$work/sdl" sparse-checkout set include; }

sdl_lib=$(dirname "$(find ~/.nuget/packages/sdl3-cs.native -path '*linux-x64/native/libSDL3.so' | sort | tail -1)")
make -C "$work/raylib/src" -j"$(nproc)" PLATFORM=PLATFORM_DESKTOP_SDL SDL_INCLUDE_PATH="$work/sdl/include" \
    CUSTOM_CFLAGS="-DUSING_SDL3_PROJECT -O2" >/dev/null
for program in bunnymark models; do
  gcc -O2 "$here/$program.c" -I "$work/raylib/src" "$work/raylib/src/libraylib.a" -L"$sdl_lib" -l:libSDL3.so \
      -lm -ldl -lpthread -Wl,-rpath,"$sdl_lib" -o "$work/$program"
done

echo "raylib $raylib_commit"
"$work/bunnymark" 3DEngine.Examples/resources/logo.png 2>/dev/null | grep '^limit' | sed 's/^/  bunnymark: /'
"$work/models" 2>/dev/null | grep '^limit' | sed 's/^/  cubes: /'

echo "3DEngine $(git rev-parse --short HEAD)"
dotnet build -c Release 3DEngine.Examples -v q >/dev/null
for example in textures_bunnymark models_stress; do
  # The bunnymark is raylib's own program unless asked for its benchmark.
  stress=""
  [ "$example" != textures_bunnymark ] || stress="--stress"
  ./e3d open 3DEngine.Examples/bin/Release/net10.0/3DEngine.Examples "$example" $stress --hidden --quiet
  until ./e3d command profile 2>/dev/null | grep -qE '^limit [1-9]'; do sleep 2; done
  echo "  $example: $(./e3d command profile | grep '^limit')"
  ./e3d stop --quiet
done

echo "raylib.h carried"
"$here/coverage.py" "$work/raylib/src/raylib.h" | head -1 | sed 's/^/  /'

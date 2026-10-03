#!/usr/bin/env bash
#
# Downloads the Slang compiler this checkout builds its shaders with, into build/tools/slang.
#
# Every shader the engine draws with is Slang, compiled to SPIR-V by slangc when it is loaded, so a
# checkout needs slangc before a shader can be edited or an example can start. The engine looks for
# it at build/tools/slang/bin/slangc, walking up from the running program and from the working
# directory, so nothing has to be put on the PATH. ENGINE_SLANGC still wins when it is set.
#
# A shipped game does not need it. Every successful compile is cached beside the assets, in
# .slang-cache, and read back where there is no compiler.
#
# The version is pinned so every checkout compiles the same SPIR-V. Pass --force to download it
# again over what is there.
#
# Usage:
#   build/fetch-slang.sh
#   build/fetch-slang.sh --force
#
set -euo pipefail

VERSION="2026.18.2"

BUILD_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS_DIR="$BUILD_DIR/tools"
SLANG_DIR="$TOOLS_DIR/slang"
STAMP="$SLANG_DIR/.version"

if [[ "${1:-}" != "--force" && -f "$STAMP" && "$(cat "$STAMP")" == "$VERSION" ]]; then
    echo "==> slangc $VERSION is already at $SLANG_DIR"
    exit 0
fi

case "$(uname -s)" in
    Linux)  os="linux" ;;
    Darwin) os="macos" ;;
    MINGW*|MSYS*|CYGWIN*) os="windows" ;;
    *) echo "No Slang release for $(uname -s). Install slangc and set ENGINE_SLANGC." >&2; exit 1 ;;
esac

case "$(uname -m)" in
    x86_64|amd64) arch="x86_64" ;;
    arm64|aarch64) arch="aarch64" ;;
    *) echo "No Slang release for $(uname -m). Install slangc and set ENGINE_SLANGC." >&2; exit 1 ;;
esac

# The glibc 2.28 build on Linux, which is the oldest floor Slang publishes for both architectures.
if [[ "$os" == "linux" ]]; then
    archive="slang-$VERSION-linux-$arch-glibc-2.28.tar.gz"
elif [[ "$os" == "windows" ]]; then
    archive="slang-$VERSION-windows-$arch.zip"
else
    archive="slang-$VERSION-macos-$arch.tar.gz"
fi

url="https://github.com/shader-slang/slang/releases/download/v$VERSION/$archive"
download="$TOOLS_DIR/$archive"

mkdir -p "$TOOLS_DIR"
echo "==> downloading $url"
curl -fsSL --retry 3 -o "$download" "$url"

rm -rf "$SLANG_DIR"
mkdir -p "$SLANG_DIR"

if [[ "$archive" == *.zip ]]; then
    unzip -q "$download" -d "$SLANG_DIR"
else
    tar -xzf "$download" -C "$SLANG_DIR"
fi

rm -f "$download"
echo "$VERSION" > "$STAMP"

"$SLANG_DIR/bin/slangc" -v >/dev/null
echo "==> slangc $VERSION is at $SLANG_DIR/bin"

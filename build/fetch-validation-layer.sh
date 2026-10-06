#!/usr/bin/env bash
#
# Downloads Khronos' Vulkan validation layer for Linux from LunarG's SDK into
# build/tools/vulkan-layer, for the workflows to name in VK_LAYER_PATH.
#
# Ubuntu 24.04's own layer, 1.3.275, predates VK_KHR_line_rasterization, which lavapipe offers and
# the engine draws its lines with, so it warned at every device the suite opened and left those
# lines unvalidated. Of the SDK's 370 MB only the layer and its manifest are kept, the layer
# stripped of its symbols to some 33 MB, which the workflows cache by this file.
#
# The version is pinned so a change of layer comes in a commit, as Slang's does. Pass --force to
# download it again over what is there.
#
# Usage:
#   build/fetch-validation-layer.sh
#   build/fetch-validation-layer.sh --force
#
set -euo pipefail

VERSION="1.4.363.0"

BUILD_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LAYER_DIR="$BUILD_DIR/tools/vulkan-layer"
STAMP="$LAYER_DIR/.version"

if [[ "${1:-}" != "--force" && -f "$STAMP" && "$(cat "$STAMP")" == "$VERSION" ]]; then
    echo "==> the validation layer $VERSION is already at $LAYER_DIR"
    exit 0
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "==> downloading LunarG's Vulkan SDK $VERSION for its validation layer"
curl -fsSL --retry 3 "https://sdk.lunarg.com/sdk/download/$VERSION/linux/vulkansdk-linux-x86_64-$VERSION.tar.xz" \
    | tar -xJ -C "$WORK" --wildcards \
        '*/x86_64/lib/libVkLayer_khronos_validation.so' \
        '*/x86_64/share/vulkan/explicit_layer.d/VkLayer_khronos_validation.json'

# The manifest names the library by a path relative to itself, which this layout keeps.
rm -rf "$LAYER_DIR"
mkdir -p "$LAYER_DIR"
cp -r "$WORK/$VERSION/x86_64/." "$LAYER_DIR/"
strip --strip-unneeded "$LAYER_DIR/lib/libVkLayer_khronos_validation.so"
echo "$VERSION" > "$STAMP"
echo "==> the validation layer is at $LAYER_DIR/share/vulkan/explicit_layer.d"

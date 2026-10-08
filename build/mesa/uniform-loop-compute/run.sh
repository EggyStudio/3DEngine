#!/usr/bin/env bash
# Builds comp.c and runs comp.glsl with it on lavapipe. Run in Ubuntu 24.04, as
#
#   podman run --rm -v "$PWD":/repro:ro,Z ubuntu:24.04 /repro/run.sh
#
# from this folder, which installs Mesa's Vulkan drivers, a compiler and glslang first. On lavapipe
# it ends with exit 139, a segmentation fault, where a driver that runs it prints "dispatched" and
# ends with 0.
set -u
apt-get update -qq > /dev/null && apt-get install -y -qq mesa-vulkan-drivers libvulkan-dev glslang-tools gcc > /dev/null 2>&1
work=$(mktemp -d) && cp "$(dirname "$0")"/{comp.c,comp.glsl} "$work" && cd "$work" || exit 1
cc comp.c -o comp -lvulkan || exit 1
glslangValidator -V --target-env vulkan1.3 -S comp comp.glsl -o comp.spv > /dev/null || exit 1
export VK_ICD_FILENAMES=${VK_ICD_FILENAMES:-/usr/share/vulkan/icd.d/lvp_icd.json}
./comp
echo "exit $?"

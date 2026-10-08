#!/usr/bin/env bash
# Builds the reproduction and draws each fragment shader with it on lavapipe: frag.glsl, and
# frag.spvasm, the module spirv-reduce cut 3DEngine's model pass down to. Run in Ubuntu 24.04, as
#
#   podman run --rm -v "$PWD":/repro:ro,Z ubuntu:24.04 /repro/run.sh
#
# from this folder, which installs Mesa's Vulkan drivers, a compiler, glslang and SPIRV-Tools
# first. On lavapipe each ends with exit 139, a segmentation fault, where a driver that draws it
# ends with 0 and prints that the middle pixel is red.
set -u
apt-get update -qq > /dev/null && apt-get install -y -qq mesa-vulkan-drivers libvulkan-dev glslang-tools spirv-tools gcc > /dev/null 2>&1
work=$(mktemp -d) && cp "$(dirname "$0")"/{repro.c,vert.glsl,frag.glsl,frag.spvasm} "$work" && cd "$work" || exit 1
cc repro.c -o repro -lvulkan || exit 1
glslangValidator -V --target-env vulkan1.3 -S vert vert.glsl -o vert.spv > /dev/null || exit 1
export VK_ICD_FILENAMES=${VK_ICD_FILENAMES:-/usr/share/vulkan/icd.d/lvp_icd.json}
glslangValidator -V --target-env vulkan1.3 -S frag frag.glsl -o frag.spv > /dev/null || exit 1
echo "frag.glsl:"
./repro 1 28
echo "exit $?"
spirv-as --target-env vulkan1.2 frag.spvasm -o frag.spv && spirv-val --target-env vulkan1.2 frag.spv || exit 1
echo "frag.spvasm:"
./repro 1 28
echo "exit $?"

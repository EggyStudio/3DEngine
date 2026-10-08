#!/usr/bin/env bash
# Assembles crash.spvasm and validates it with the SPIRV-Tools on the PATH, and reduces it with the
# spirv-reduce given, that on the PATH otherwise, and a test that takes only the module the
# reduction starts from, so spirv-reduce takes no step and its pass that merges blocks looks at the
# module as it is.
#
#   build/spirv-tools/merge-blocks/run.sh [spirv-reduce]
set -u
reduce="${1:-spirv-reduce}"
here="$(cd "$(dirname "$0")" && pwd)"
work=$(mktemp -d)
spirv-as --target-env vulkan1.2 "$here/crash.spvasm" -o "$work/crash.spv" || exit 1
spirv-val --target-env vulkan1.2 "$work/crash.spv" && echo "spirv-val takes it"
printf '#!/bin/sh\ncmp -s "$1" %s/crash.spv\n' "$work" > "$work/only-start.sh"
chmod +x "$work/only-start.sh"
cd "$work"
"$reduce" --version | head -n 1
"$reduce" crash.spv -o reduced.spv -- "$work/only-start.sh" > log.txt 2>&1
code=$?
grep "Trying pass" log.txt | tail -n 1
echo "spirv-reduce exited with $code"
rm -rf "$work"

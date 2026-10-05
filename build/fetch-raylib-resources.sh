#!/usr/bin/env bash
# Downloads the files of raylib's examples that the examples here load, at the raylib commit
# build/raylib-bench/run.sh pins, into 3DEngine.Examples/raylib-resources, which a build of the
# examples copies beside the program under resources/, at the path raylib's own example loads it
# by. A file already there is kept, so this is cheap to run again. They are raylib's, under the
# licenses its examples' resources/LICENSE.md files give, and are fetched rather than kept here.
#
#   build/fetch-raylib-resources.sh
#
# A line of 3DEngine.Examples/raylib-resources.txt is a file's path under raylib's examples/, as
# models/resources/models/obj/castle.obj, which lands at resources/models/obj/castle.obj.
set -euo pipefail
cd "$(dirname "$0")/.."

commit=$(sed -n 's/^raylib_commit=\([0-9a-f]\{40\}\)$/\1/p' build/raylib-bench/run.sh)
[ -n "$commit" ] || { echo "build/raylib-bench/run.sh pins no raylib commit" >&2; exit 1; }

into=3DEngine.Examples/raylib-resources
while read -r line; do
    case "$line" in ''|'#'*) continue ;; esac
    path="${line#*/resources/}"
    [ -f "$into/$path" ] && continue
    mkdir -p "$into/$(dirname "$path")"
    curl -fsSL --retry 3 -o "$into/$path" "https://raw.githubusercontent.com/raysan5/raylib/$commit/examples/$line"
    echo "fetched $path"
done < 3DEngine.Examples/raylib-resources.txt

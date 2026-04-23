#!/usr/bin/env bash
# Remove a submodule cleanly.
# Usage: ./remove-module.sh 3DEngine.Foo.Bar
set -euo pipefail
name="$1"
path="Modules/$name"

if [ ! -d "$path" ]; then
    echo "Submodule '$path' does not exist." >&2
    exit 1
fi

git submodule deinit -f -- "$path"
git rm -f "$path"
rm -rf ".git/modules/$path"
git commit -m "Remove $name submodule"
git push


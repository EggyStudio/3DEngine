#!/usr/bin/env bash
# Asks raylib's site which of the examples here it runs in the browser, and writes their names to
# build/raylib-examples.txt, which the README's gallery links by. Run by hand when an example is
# added, since the suite and CI run with no network.
#
#   build/raylib-examples.sh
#
# An example's page is raylib.com/examples/<module>/<example>.html, the module being its name up to
# the first underscore. The loader page the README links to answers for any name, so the page itself
# is asked for, which answers 404 for an example raylib does not have.
set -euo pipefail
cd "$(dirname "$0")/.."

out=build/raylib-examples.txt
names=$(grep -oE '^\s*\["[a-z0-9_]+"\]' 3DEngine.Examples/Program.cs | grep -oE '[a-z0-9_]+' | sort)
{
    echo "# The examples raylib's site runs in the browser, written by build/raylib-examples.sh."
    for name in $names; do
        status=$(curl -s -o /dev/null -w '%{http_code}' "https://www.raylib.com/examples/${name%%_*}/$name.html")
        if [ "$status" = 200 ]; then echo "$name"; fi
    done
} > "$out.tmp"
mv "$out.tmp" "$out"
echo "$(grep -vc '^#' "$out") of $(echo "$names" | wc -w) examples have a page on raylib's site."

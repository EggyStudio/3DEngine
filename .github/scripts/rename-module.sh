#!/usr/bin/env bash
# Rename a module repo on GitHub AND update every reference locally.
#
# Usage: ./rename-module.sh <old-name> <new-name>
# Example: ./rename-module.sh 3DEngine.Foo 3DEngine.Bar
#
# Owner defaults to the authenticated gh user; override with:
#   OWNER=EggyStudio ./rename-module.sh <old> <new>
#
# Steps performed:
#   1. Rename the GitHub repo via `gh repo rename`.
#   2. Rename the submodule directory Modules/<old> -> Modules/<new>.
#   3. Update .gitmodules (path, url, section name).
#   4. Sync submodule config (git submodule sync).
#   5. Rename any <old>.csproj / <old>.sln-style files inside the module.
#   6. Replace every occurrence of <old> with <new> in tracked text files
#      across the parent repo (.sln, .csproj, Directory.Build.*, *.cs, *.md,
#      *.json, *.props, *.targets, *.yml, *.yaml, *.sh).
#   7. Stage & commit the change in the parent repo.
set -euo pipefail

if [ "$#" -ne 2 ] || [ -z "${1:-}" ] || [ -z "${2:-}" ]; then
    echo "Usage: $0 <old-name> <new-name>" >&2
    exit 1
fi

old="$1"
new="$2"

if ! command -v gh >/dev/null 2>&1; then
    echo "✗ GitHub CLI (gh) is not installed." >&2
    exit 1
fi
if ! gh auth status >/dev/null 2>&1; then
    echo "✗ gh is not authenticated. Run: gh auth login" >&2
    exit 1
fi

repo_root=$(git rev-parse --show-toplevel)
cd "$repo_root"

owner="${OWNER:-$(gh api user --jq .login)}"

if [ ! -d "Modules/$old" ]; then
    echo "error: Modules/$old does not exist" >&2
    exit 1
fi
if [ -e "Modules/$new" ]; then
    echo "error: Modules/$new already exists" >&2
    exit 1
fi

echo ">> Renaming GitHub repo $owner/$old -> $new"
gh repo rename "$new" --repo "$owner/$old" --yes

echo ">> Moving submodule directory"
git mv "Modules/$old" "Modules/$new"

echo ">> Updating .gitmodules"
# Section header rename
sed -i "s|\[submodule \"$old\"\]|[submodule \"$new\"]|g" .gitmodules
# Path + URL
sed -i "s|Modules/$old|Modules/$new|g" .gitmodules
sed -i "s|/$old\([\"/[:space:]]\\|$\)|/$new\1|g" .gitmodules

echo ">> Syncing submodule config"
git submodule sync -- "Modules/$new"

# Update remote URL inside the submodule's own config too
( cd "Modules/$new" && git remote set-url origin "https://github.com/$owner/$new" )

echo ">> Renaming files inside the module that contain the old name"
while IFS= read -r -d '' f; do
    dir=$(dirname "$f")
    base=$(basename "$f")
    newbase="${base//$old/$new}"
    if [ "$base" != "$newbase" ]; then
        git -C "$repo_root" mv "$f" "$dir/$newbase" 2>/dev/null \
            || mv "$f" "$dir/$newbase"
    fi
done < <(find "Modules/$new" -depth -name "*$old*" -print0)

echo ">> Replacing textual references across the workspace"
# Limit to relevant text file extensions to avoid touching binaries.
grep -rIl --null \
    --exclude-dir=.git \
    --exclude-dir=bin \
    --exclude-dir=obj \
    --include='*.sln' --include='*.csproj' --include='*.props' \
    --include='*.targets' --include='*.cs' --include='*.md' \
    --include='*.json' --include='*.xml' --include='*.yml' \
    --include='*.yaml' --include='*.sh' --include='*.txt' \
    --include='.gitmodules' \
    "$old" . 2>/dev/null \
| xargs -0 -r sed -i "s|$old|$new|g"

echo ">> Staging & committing"
git add -A
git commit -m "Rename $old -> $new"

echo ">> Done. Review with: git show --stat HEAD"
echo "   Push with:        git push"

#!/usr/bin/env bash
# Remove a submodule cleanly. Optionally also delete the GitHub repo.
#
# Usage:
#   ./remove-module.sh <ModuleName>                 # local removal only
#   ./remove-module.sh <ModuleName> --delete-remote # also delete the GitHub repo
#
# Owner for --delete-remote defaults to the authenticated gh user; override
# with: OWNER=EggyStudio ./remove-module.sh ... --delete-remote
set -euo pipefail

if [ "$#" -lt 1 ] || [ -z "${1:-}" ]; then
    echo "Usage: $0 <ModuleName> [--delete-remote]" >&2
    exit 1
fi

name="$1"
delete_remote=0
if [ "${2:-}" = "--delete-remote" ]; then
    delete_remote=1
fi

cd "$(git rev-parse --show-toplevel)"

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

if [ "$delete_remote" -eq 1 ]; then
    if ! command -v gh >/dev/null 2>&1; then
        echo "✗ gh not installed; skipping remote deletion." >&2
        exit 1
    fi
    if ! gh auth status >/dev/null 2>&1; then
        echo "✗ gh not authenticated; skipping remote deletion." >&2
        exit 1
    fi
    owner="${OWNER:-$(gh api user --jq .login)}"
    echo "• Deleting GitHub repo $owner/$name (requires delete_repo scope)..."
    gh repo delete "$owner/$name" --yes
    echo "  ✓ remote repo deleted."
fi


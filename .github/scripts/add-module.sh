#!/usr/bin/env bash
# Usage: ./add-module.sh <ModuleName> ["optional description"]
# Example:
#   ./add-module.sh Engine.Common.App "App + Config + plugin contract"
#
# What it does:
#   1. Verifies `gh` is installed and authenticated.
#   2. Creates the repo on GitHub (public, MPL-2.0, README initialized) under
#      the authenticated gh user, OR under $OWNER if set. Skips if it exists.
#   3. Adds it as a git submodule at <repo-root>/Modules/<ModuleName>
#      (tracking the remote default branch).
#   4. Commits & pushes the submodule registration in the parent repo.
#
# Requires: gh (https://cli.github.com), git.

set -euo pipefail

# --- args -----------------------------------------------------------------
if [ "$#" -lt 1 ] || [ -z "${1:-}" ]; then
    echo "Usage: $0 <ModuleName> [\"description\"]" >&2
    echo "Example: $0 Engine.Common.App \"App + Config + plugin contract\"" >&2
    exit 1
fi

name="$1"
description="${2:-}"

# --- prerequisites --------------------------------------------------------
if ! command -v gh >/dev/null 2>&1; then
    echo "✗ GitHub CLI (gh) is not installed." >&2
    echo "  Install it from https://cli.github.com and run: gh auth login" >&2
    exit 1
fi

if ! gh auth status >/dev/null 2>&1; then
    echo "✗ gh is not authenticated. Run: gh auth login" >&2
    exit 1
fi

# Always operate from the parent repository root.
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

# --- resolve owner --------------------------------------------------------
# Defaults to the authenticated gh user; override with: OWNER=EggyStudio ./add-module.sh ...
owner="${OWNER:-$(gh api user --jq .login)}"
full="$owner/$name"
url="https://github.com/$owner/$name.git"

echo "→ Module:      $name"
echo "→ Owner:       $owner"
[ -n "$description" ] && echo "→ Description: $description"
echo "→ Submodule:   Modules/$name"
echo

# --- step 1: create remote repo if missing --------------------------------
if gh repo view "$full" >/dev/null 2>&1; then
    echo "• Repo $full already exists on GitHub - skipping create."
    if [ -n "$description" ]; then
        gh repo edit "$full" -d "$description" >/dev/null && \
            echo "  description synced."
    fi
else
    echo "• Creating $full on GitHub..."
    gh repo create "$full" \
        --public \
        --license mpl-2.0 \
        --add-readme \
        ${description:+--description "$description"} \
        >/dev/null
    echo "  ✓ created."
fi

# Discover the remote default branch (gh defaults to "main").
default_branch="$(gh repo view "$full" --json defaultBranchRef --jq .defaultBranchRef.name 2>/dev/null || true)"
[ -z "$default_branch" ] && default_branch="main"

# --- step 2: register submodule ------------------------------------------
if [ -e "Modules/$name" ] || git config -f .gitmodules --get "submodule.Modules/$name.url" >/dev/null 2>&1; then
    echo "• Modules/$name already registered - skipping submodule add."
else
    echo "• Adding submodule Modules/$name (branch: $default_branch)..."
    git submodule add -b "$default_branch" "$url" "Modules/$name"
    git submodule update --init --recursive "Modules/$name"
    echo "  ✓ added."
fi

# --- step 3: commit & push parent ----------------------------------------
git add .gitmodules "Modules/$name" 2>/dev/null || true
if ! git diff --cached --quiet; then
    git commit -m "Add $name submodule"
    git push
    echo "  ✓ parent repo updated and pushed."
else
    echo "• Nothing to commit in parent repo."
fi

echo
echo "✓ Done. $name is live at https://github.com/$owner/$name and wired in at Modules/$name."

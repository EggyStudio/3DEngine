#!/usr/bin/env bash
# Commit & push pending changes inside each submodule, then bump the parent.
# Usage: ./push-modules.sh "commit message"
set -euo pipefail

msg="${1:-Update submodule}"

cd "$(git rev-parse --show-toplevel)"

git submodule foreach --recursive "
    if [ -n \"\$(git status --porcelain)\" ]; then
        branch=\$(git rev-parse --abbrev-ref HEAD)
        if [ \"\$branch\" = \"HEAD\" ]; then
            echo \"Skipping \$name (detached HEAD)\"
        else
            git add -A
            git commit -m \"$msg\"
            git push origin \"\$branch\"
        fi
    fi
"

if ! git diff --quiet -- Modules; then
    git add Modules
    git commit -m "$msg"
    git push
fi

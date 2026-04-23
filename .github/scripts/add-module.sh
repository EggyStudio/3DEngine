#!/usr/bin/env bash
# Usage: ./add-module.sh 3DEngine.Foo.Bar
set -euo pipefail
name="$1"
git submodule add -b main "https://github.com/EggyStudio/$name" "Modules/$name"
git add .gitmodules "Modules/$name"
git commit -m "Add $name submodule"
git push

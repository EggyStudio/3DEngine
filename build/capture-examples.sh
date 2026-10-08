#!/usr/bin/env bash
# Captures every example offscreen into the folder given (build/capture-example.sh), as WebP or as
# PNG by the second argument, and checks each one's session log for validation errors, which counts
# the teardown too. Every example is captured before the script fails. A capture that fails says
# why in its error, as the test page names its causes (N 6.7): the script's exit code and its own
# last line, and the last lines the example logged at a warning or worse, since an annotation is
# all a reader not signed in sees. The build workflow's examples job runs it for the README's
# gallery, and the test workflow's macOS job for pictures kept as its artifact and compared with
# nothing. It runs in the bash macOS ships too. Examples named after the format are captured alone.
#
#   build/capture-examples.sh <folder> [webp|png] [example...]
set -u

out="$1"
ext="${2:-webp}"
shift; [ $# -gt 0 ] && shift
cd "$(dirname "$0")/.."
mkdir -p "$out"
# The system by the name a reader knows it by, where Git's bash on Windows calls itself MINGW64.
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) system=Windows ;;
  Darwin) system=macOS ;;
  *) system=$(uname -s) ;;
esac

failed=0
examples="$*"
[ -n "$examples" ] || examples=$(sed -n 's/^[[:space:]]*\["\([a-z0-9_]*\)".*/\1/p' 3DEngine.Examples/Program.cs)
for example in $examples; do
  code=0
  said=$(build/capture-example.sh "$example" "$out/$example.$ext" 2>&1) || code=$?
  echo "$said"
  if [ "$code" -eq 0 ] && [ ! -s "$out/$example.$ext" ]; then
    code=1
    said="$said
it wrote no picture"
  fi
  if [ "$code" -ne 0 ]; then
    logged=$(grep -a -E '\[(WARN |ERROR|FATAL)\]' "build/sessions/$example.log" 2>/dev/null | tail -n 3 | sed 's/^\[[^]]*\] //' | cut -c1-300)
    why="exit code $code: $(printf '%s' "$said" | tail -n 1 | cut -c1-300)"
    [ -n "$logged" ] && why="$why%0AIts last lines at a warning or worse:%0A${logged//$'\n'/%0A}"
    echo "::error title=$example on $system::$example: the capture failed, $why"
    failed=1
    ./e3d stop --quiet || true
  fi
  if grep -F '[Vulkan.Validation]' "build/sessions/$example.log" 2>/dev/null | grep -F 'Validation Error'; then
    echo "::error title=$example on $system::$example: the validation layer reported an error"
    failed=1
  fi
done
exit $failed

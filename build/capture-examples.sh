#!/usr/bin/env bash
# Captures every example offscreen into the folder given (build/capture-example.sh), as WebP or as
# PNG by the second argument, and checks each one's session log for validation errors, which counts
# the teardown too. Every example is captured before the script fails. A capture that fails says
# why in its error, as the test page names its causes (N 6.7): the script's exit code and its own
# last line, and the last lines the example logged at a warning or worse, since an annotation is
# all a reader not signed in sees. The build workflow's examples job runs it for the README's
# gallery, and the test workflow's macOS captures job for pictures kept as its artifact and compared
# with nothing. It runs in the bash macOS ships too. Examples named after the format are captured
# alone.
#
# An example whose capture takes more than CAPTURE_SECONDS, 300 unless set, is stopped and fails
# alone, and the rest are captured. Where CAPTURE_MINUTES is set, no example is started once that
# many minutes have gone, and an error names the last one reached and how many were left. At the
# end the script says how many drew, in how long, and the slowest, in a notice the page shows, and
# writes each example's seconds and result to times.tsv in the folder.
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
limit="${CAPTURE_SECONDS:-300}"
budget="${CAPTURE_MINUTES:-}"
total=$(echo $examples | wc -w | tr -d ' ')
began=$(date +%s)
reached=0
drew=0
last=""
# What each capture said, in a file rather than through $(...), which would wait for every
# process holding its output open, the example's own among them.
said_file=$(mktemp)
times="$out/times.tsv"
printf 'example\tseconds\tresult\n' > "$times"
for example in $examples; do
  if [ -n "$budget" ] && [ $(( $(date +%s) - began )) -ge $(( budget * 60 )) ]; then
    echo "::error title=Captures on $system::The captures' budget of $budget minutes ended after $reached of $total examples, the last reached ${last:-none}, $(( total - reached )) left uncaptured"
    failed=1
    break
  fi
  reached=$(( reached + 1 ))
  last="$example"
  start=$(date +%s)
  code=0
  stopped=""
  build/capture-example.sh "$example" "$out/$example.$ext" > "$said_file" 2>&1 &
  pid="$!"
  while kill -0 "$pid" 2>/dev/null; do
    if [ $(( $(date +%s) - start )) -ge "$limit" ]; then
      kill "$pid" 2>/dev/null
      stopped=1
      break
    fi
    sleep 0.2
  done
  wait "$pid" || code=$?
  seconds=$(( $(date +%s) - start ))
  said=$(cat "$said_file")
  echo "$said"
  if [ -n "$stopped" ]; then
    code=124
    said="$said
it took past its $limit seconds and was stopped"
  elif [ "$code" -eq 0 ] && [ ! -s "$out/$example.$ext" ]; then
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
    printf '%s\t%s\tfailed\n' "$example" "$seconds" >> "$times"
  else
    drew=$(( drew + 1 ))
    printf '%s\t%s\tdrew\n' "$example" "$seconds" >> "$times"
  fi
  if grep -F '[Vulkan.Validation]' "build/sessions/$example.log" 2>/dev/null | grep -F 'Validation Error'; then
    echo "::error title=$example on $system::$example: the validation layer reported an error"
    failed=1
  fi
done
rm -f "$said_file"

# How many drew and how long they took, the mean and the five slowest, from times.tsv.
spent=$(( $(date +%s) - began ))
slowest=$(tail -n +2 "$times" | sort -t "$(printf '\t')" -k2,2nr | head -n 5 | awk -F '\t' '{printf "%s%s %s s", (NR > 1 ? ", " : ""), $1, $2}')
mean=0
[ "$reached" -gt 0 ] && mean=$(( spent / reached ))
[ -n "$slowest" ] && slowest=" The slowest were $slowest."
echo "::notice title=Captures on $system::$drew of $total examples drew in $spent seconds, about $mean a capture, of $reached reached.$slowest"
exit $failed

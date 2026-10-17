#!/usr/bin/env bash
# Opens a program on the engine as ./e3d open does on Linux and macOS, from the checkout, serving,
# with its output in build/sessions/<name>.log, and returns once it answers a command. A shell
# starts it and waits on it, since e3d open's shell is replaced by the program and e3d ends once it
# serves, which leaves no process to read how it ended, and the shell writes that once it does,
# its exit code or 128 and the number of the signal that ended it. On Linux every opening prints
# the processor's model and flags, so a run says which processors a death came on, the program may
# dump core, and where it dies leaving a core where the kernel's core_pattern names a file, the
# shell reads the faulting thread's native frames and the instruction at its program counter from
# it with gdb.
#
#   build/open-game.sh <program> [--offscreen|--hidden|...]
#   build/open-game.sh --ended <program or its name>
#
# Beside the log, under build/sessions, it writes these.
#   - <name>.pid, the program's process id, once it is started.
#   - <name>.ended, once it ends, a line saying how, then what gdb read where a core was left.
#     <name>.reading stands while gdb reads.
# Given --ended, it prints how the program last opened here ended, once its shell has written it
# and gdb has read any core, and nothing while the program still runs, which build/drive-game.sh,
# build/storm.sh and build/step.py ask where a game fails.
set -euo pipefail

if [ "${1:-}" = --ended ]; then
  cd "$(dirname "$0")/.."
  name=$(basename "$2")
  pid=$(cat "build/sessions/$name.pid" 2> /dev/null || true)
  [ -n "$pid" ] || exit 0
  # A process killed and not yet reaped is a zombie, which ps marks Z.
  case "$(ps -o stat= -p "$pid" 2> /dev/null || true)" in Z*|"") ;; *) exit 0 ;; esac
  for _ in 1 2 3 4 5 6 7 8 9 10; do grep -q "^its process $pid " "build/sessions/$name.ended" 2> /dev/null && break; sleep 0.5; done
  for _ in $(seq 1 240); do [ -e "build/sessions/$name.reading" ] || break; sleep 0.5; done
  if grep -q "^its process $pid " "build/sessions/$name.ended" 2> /dev/null; then cat "build/sessions/$name.ended"; fi
  exit 0
fi

program="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
shift
cd "$(dirname "$0")/.."
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) echo "On Windows ./e3d open starts a program, and build/drive-game.sh watches it." >&2; exit 2 ;;
esac
linux=no
[ "$(uname -s)" = Linux ] && linux=yes
name=$(basename "$program")
sessions=build/sessions
mkdir -p "$sessions"
log="$sessions/$name.log"
pidfile="$sessions/$name.pid"
ended="$sessions/$name.ended"
reading="$sessions/$name.reading"
rm -f "$pidfile" "$ended" "$reading"

processor=""
if [ "$linux" = yes ]; then
  processor=$(LC_ALL=C lscpu | sed -n 's/^Model name: *//p' | head -n 1)
  echo "$name opens on $processor, whose flags are $(LC_ALL=C lscpu | sed -n 's/^Flags: *//p' | head -n 1)"
fi

# How the program ended, from its wait status, with the processor on Linux.
said() {
  local what=""
  case "$2" in
    0) what=", of its own" ;;
    132) what=", SIGILL, an illegal instruction, one the processor lacks or a trap a compiler wrote" ;;
    134) what=", SIGABRT, as .NET's abort on a fatal error" ;;
    135) what=", SIGBUS, a fault of memory" ;;
    136) what=", SIGFPE, an arithmetic fault" ;;
    137) what=", SIGKILL, as the kernel's out-of-memory killer or a kill sends" ;;
    139) what=", SIGSEGV, a fault in native code" ;;
    143) what=", SIGTERM" ;;
    129|13[0-9]|14[0-9]|15[0-9]) what=", signal $(( $2 - 128 ))" ;;
  esac
  echo "its process $1 ended with exit code $2$what${processor:+, on $processor}"
}

# The core a process left, where the kernel's core_pattern names a file and not a program it pipes
# cores to, as Ubuntu's apport is, which the job replaces with a file under captures. A pattern
# names the process id with %p, and any other field it holds is matched by a wildcard. A relative
# pattern is the process's folder's, which is the checkout.
core() {
  local pattern found
  pattern=$(cat /proc/sys/kernel/core_pattern 2> /dev/null || true)
  case "$pattern" in ""|"|"*) return 0 ;; esac
  for found in $(printf '%s' "$pattern" | sed "s/%p/$1/g; s/%[a-zA-Z]/*/g"); do
    [ -f "$found" ] && { echo "$found"; return 0; }
  done
  return 0
}

# What gdb reads from a core, the faulting thread, its native frames and the instructions from its
# program counter, the lines of each thread it finds left out. gdb asks no server for symbols, since
# a library's debug files are large and libLLVM's own exported names place its frames. The core is
# then deleted, since a .NET process's core holds every page it touched and the job keeps captures.
read_core() {
  if ! command -v gdb > /dev/null; then
    echo "It left a core, which no gdb here could read, and which is deleted."
    rm -f "$1"
    return 0
  fi
  echo "gdb read the core it left, the faulting thread's frames and the instructions at its program counter."
  gdb -batch -nx -iex "set debuginfod enabled off" -ex "thread" -ex "bt 30" -ex 'x/4i $pc' "$program" "$1" 2>&1 \
    | grep -v -E '^\[New LWP|^\[Thread debugging|^warning: |^Downloading' | head -n 60 || true
  rm -f "$1"
}

# The shell that starts the program and waits on it holds none of the caller's output, which a
# workflow's step reads until every process holding it has ended. It writes how the program ended
# only while the pid file still names it, so the line of a program opened before, which ended as
# this one began, is not taken for this one's, and every line names its process for the same
# reason. The marker that gdb is reading is made before the line is written, so a reader that finds
# the line finds the marker with it.
(
  if [ "$linux" = yes ]; then ulimit -c unlimited 2> /dev/null || true; fi
  "$program" "$@" --serve < /dev/null > "$log" 2>&1 &
  child="$!"
  echo "$child" > "$pidfile"
  status=0
  wait "$child" || status=$?
  [ "$(cat "$pidfile" 2> /dev/null || true)" = "$child" ] || exit 0
  left=""
  if [ "$linux" = yes ] && [ "$status" != 0 ]; then left=$(core "$child"); fi
  if [ -n "$left" ]; then : > "$reading"; fi
  said "$child" "$status" > "$ended.part"
  mv "$ended.part" "$ended"
  if [ -n "$left" ]; then
    read_core "$left" >> "$ended"
    rm -f "$reading"
  fi
) < /dev/null > /dev/null 2>&1 3>&- &

for _ in $(seq 1 50); do [ -s "$pidfile" ] && break; sleep 0.1; done
pid=$(cat "$pidfile" 2> /dev/null || true)
if [ -z "$pid" ]; then
  echo "$name was not started." >&2
  exit 1
fi
# It serves once a command is answered, which its first frame does, and is given the minute e3d
# open gives a program.
until=$(( SECONDS + 60 ))
while [ "$SECONDS" -lt "$until" ]; do
  if ./e3d command --session "$pid" frames.wait 0 --quiet --timeout 10 > /dev/null 2>&1; then
    echo "$name is serving ($pid), its log in $log"
    exit 0
  fi
  grep -q "^its process $pid " "$ended" 2> /dev/null && break
  sleep 0.5
done
if grep -q "^its process $pid " "$ended" 2> /dev/null; then
  for _ in $(seq 1 240); do [ -e "$reading" ] || break; sleep 0.5; done
  echo "$name did not open, since $(cat "$ended")" >&2
else
  echo "$name did not serve within a minute of starting, its process $pid still running." >&2
fi
tail -n 5 "$log" >&2 || true
exit 1

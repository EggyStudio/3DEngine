#!/usr/bin/env bash
# Plays a game from the package through ./e3d, offscreen and under the validation layer, as the
# Linux jobs play it, and asserts its walk or its win, so a game's input, its sound and the session
# ./e3d controls are tried on the system it runs on. The build workflow runs it on Windows and macOS
# after build/play-game.sh, which packs the engine, for Slide on a push and for every game in the
# pack run, and once more for one game with window as the second argument, which opens the game in a
# window on the runner's desktop, as a player sees it, so SDL's window and the swapchain are tried
# there.
#
#   build/drive-game.sh <Hopper|Summit|Swarm|Rally|Manor|Tactics|Tempo|Sumo|Wordfall|Slide|Jelly|Wick> [offscreen|window]
#
# What fails is said as an error annotation naming the game and the system, with the last warnings
# of the game's log, so the page says why a game cannot run there. Each game is drawn at 480 by
# 270, since what is asserted is its play and not its picture, and a device drawing on its CPU, as
# lavapipe on a Windows runner is, plays more of it in the time.
set -euo pipefail

game="$1"
mode="${2:-offscreen}"
cd "$(dirname "$0")/.."
mkdir -p captures
name=$(printf '%s' "$game" | tr '[:upper:]' '[:lower:]')
# The system by the name a reader knows it by, where Git's bash on Windows calls itself MINGW64.
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) system=Windows ;;
  Darwin) system=macOS ;;
  *) system=$(uname -s) ;;
esac
# How ./e3d opens the game, and how the page names the run. A window takes no flag, and its run's
# captures are named apart from the offscreen run's. The empty list is expanded where it is used
# in the form macOS's bash 3.2 takes under set -u.
case "$mode" in
  offscreen) shown=(--offscreen); heading="$game on $system"; where=offscreen ;;
  window) shown=(); heading="$game in a window on $system"; where="in a window"; name="$name-window" ;;
  *) echo "the second argument is offscreen or window, not $mode" >&2; exit 2 ;;
esac
log="build/sessions/$game.log"
# Every command and stop goes to the game played here by its name, so a program an earlier game left
# serving neither makes them ambiguous nor takes the stop meant for this one.
export E3D_NAME="$game"
# The script's own output, which an error is written to, so one said inside $(ask ...) reaches the
# page and not the variable.
exec 3>&1

# Each game has a budget of minutes, DRIVE_MINUTES or eight, past which it is stopped and an error
# names how far it got by its last status, so a slow device says which game it is slow at and the
# games after it are still played. The game's last status is kept in a file for that error.
minutes="${DRIVE_MINUTES:-8}"
last="captures/$name-last-status.txt"
expired="captures/$name-expired"
finished="captures/$name-finished"
rm -f "$last" "$expired" "$finished"
restore=""
cleanup() {
  : > "$finished"
  if [ -n "$restore" ]; then eval "$restore"; fi
  ./e3d stop --quiet > /dev/null 2>&1 || true
  # A game started here that never served has no session for the stop to find.
  if [ "$system" != Windows ] && [ -n "$pid" ] && ! ended; then kill "$pid" 2> /dev/null || true; fi
}
trap cleanup EXIT

# Where a game that dies in native code leaves its dump. On Windows it is the system's own, set in
# the job, which names each dump after the program's file. Elsewhere it is the .NET runtime's, asked
# for here, into captures, which each job keeps, since Slide's process ended once in its
# startup on Linux with nothing in its log and no dump to say which thread it died on.
if [ "$system" = Windows ]; then
  dumps="${E3D_DUMPS:-3DEngine.Tests/TestResults/dumps}"
else
  dumps="${E3D_DUMPS:-captures/dumps}"
  mkdir -p "$dumps"
  export DOTNET_DbgEnableMiniDump=1 DOTNET_DbgMiniDumpType=2 DOTNET_DbgMiniDumpName="$PWD/$dumps/%e.%p.dmp"
fi

# The game's process, and how it ended once it has. On Windows e3d opens it through cmd.exe, and a
# watcher holds it and writes its exit code once it ends, since nothing else waits on it. On Linux
# and macOS e3d's shell is replaced by the game and e3d ends once the game serves, which leaves no
# process to read how it ended, so the game is started here instead, by a shell that waits on it and
# writes its wait status, the exit code or 128 and the number of the signal that ended it. Where a
# command then finds no session, the error says whether the process still runs, so it stopped
# serving, or how it ended, which its log's tail does not say of a kill or a crash in native code.
# Each opening has files of its own, so a line the last one's shell writes late is not read as this
# one's.
pid=""
opened="captures/$name-opened.json"
openings=0
died=""
start() {
  openings=$(( openings + 1 ))
  exited="captures/$name-exit-$openings.txt"
  local started="captures/$name-pid-$openings.txt"
  rm -f "$exited" "$started"
  pid=""
  if [ "$system" = Windows ]; then
    # The path without the .exe a Windows build gives it, which ./e3d finds. Its answer goes to a
    # file and is read from there, since the game is started holding every handle e3d holds, a pipe
    # around it among them, and $(...) would wait on that pipe until the game ends.
    ENGINE_VULKAN_VALIDATION=1 ./e3d open "$folder/$game" ${shown[@]+"${shown[@]}"} --json > "$opened" || return 1
    pid=$(perl -MJSON::PP -0777 -ne 'my $answer = eval { decode_json($_) } or exit; print $answer->{data}{pid} // ""' "$opened")
    if [ -n "$pid" ]; then
      powershell.exe -NoProfile -NonInteractive -Command \
        "\$p = Get-Process -Id $pid -ErrorAction Stop; \$null = \$p.Handle; \$p.WaitForExit(); '0x{0:X8}' -f \$p.ExitCode" \
        < /dev/null > "$exited" 2> /dev/null &
    fi
    return 0
  fi
  # Started as e3d open starts it, from the checkout, serving, with its output in its log. The shell
  # holds none of the step's output, which the step reads until every process holding it has ended.
  mkdir -p "$(dirname "$log")"
  (
    ENGINE_VULKAN_VALIDATION=1 "$folder/$game" ${shown[@]+"${shown[@]}"} --serve < /dev/null > "$log" 2>&1 &
    echo $! > "$started"
    status=0
    wait $! || status=$?
    echo "$status" > "$exited"
  ) < /dev/null > /dev/null 2>&1 3>&- &
  for _ in $(seq 1 50); do [ -s "$started" ] && break; sleep 0.1; done
  pid=$(cat "$started" 2> /dev/null || true)
  [ -n "$pid" ] || return 1
  # It serves once a command is answered, which its first frame does, and is given the minute e3d
  # open gives a game.
  local until=$(( SECONDS + 60 ))
  while [ "$SECONDS" -lt "$until" ]; do
    ./e3d command --session "$pid" frames.wait 0 --quiet --timeout 10 > /dev/null 2>&1 && return 0
    [ -s "$exited" ] && return 1
    sleep 0.5
  done
  return 1
}
# What an exit code means, as e3d names it where a game ends before it serves, on Windows its
# NTSTATUS and elsewhere 128 and the signal's number for a game a signal ended.
meaning() {
  case "$1" in
    0|0x00000000) echo ", of its own" ;;
    0xC0000005) echo ", an access violation" ;;
    0xC00000FD) echo ", a stack overflow" ;;
    0xC0000409) echo ", a fail-fast, as .NET's on a fatal error" ;;
    0xC0000374) echo ", a corrupted heap" ;;
    0xE0434352) echo ", a .NET exception no one caught" ;;
    134) echo ", SIGABRT, as .NET's abort on a fatal error" ;;
    137) echo ", SIGKILL, as the kernel's out-of-memory killer or a kill sends" ;;
    139) echo ", SIGSEGV, a fault in native code" ;;
    143) echo ", SIGTERM" ;;
    129|13[0-9]|14[0-9]|15[0-9]) echo ", signal $(( $1 - 128 ))" ;;
  esac
}
# Whether the game's process has ended. Git's bash would turn the Windows tools' arguments that
# begin with a slash into paths, which MSYS2_ARG_CONV_EXCL stops. A process killed and not yet
# reaped is a zombie, which ps marks Z and kill -0 still finds.
ended() {
  if [ "$system" = Windows ]; then
    ! MSYS2_ARG_CONV_EXCL='*' tasklist.exe /FI "PID eq $pid" /NH 2> /dev/null | grep -q " $pid "
  else
    case "$(ps -o stat= -p "$pid" 2> /dev/null)" in Z*|"") return 0 ;; *) return 1 ;; esac
  fi
}
# How the game's process ended, its exit code and, on Windows, the description of the last crash of
# it Windows logged, the faulting module and the exception's code for a native one and the
# exception for a managed one.
how() {
  local code="" logged=""
  # The watcher or the shell writes its line a moment after the process ends.
  for _ in 1 2 3 4 5 6 7 8 9 10; do [ -s "$exited" ] && break; sleep 0.5; done
  [ -s "$exited" ] && code=$(tr -d '\r\n' < "$exited")
  if [ "$system" = Windows ]; then
    logged=$(MSYS2_ARG_CONV_EXCL='*' wevtutil.exe qe Application "/q:*[System[(EventID=1000 or EventID=1026)]]" /c:5 /rd:true /f:text 2> /dev/null \
      | tr -d '\r' | awk -v exe="$(printf '%s' "$game" | tr '[:upper:]' '[:lower:]').exe" '
          /^Event\[/ { if (found) exit; text = ""; inside = 0; next }
          /^ *Description:/ { inside = 1; next }
          inside { line = $0; gsub(/^ +| +$/, "", line); if (line != "") text = text (text == "" ? "" : " ") line; if (index(tolower(line), exe)) found = 1 }
          END { if (found) print substr(text, 1, 400) }' || true)
  fi
  echo "its process $pid ended${code:+ with exit code $code$(meaning "$code")}${logged:+, and Windows logged: $logged}"
}
# Where the session went, whether the game's process still runs and, ended, how.
whereabouts() {
  [ -n "$pid" ] || { echo "its process not known"; return; }
  if ended; then how; else echo "its process $pid still runs, so it stopped serving"; fi
}
# Lines as one workflow command carries them, each percent sign and line's end escaped.
escaped() { printf '%s\n' "$1" | sed 's/%/%25/g' | awk 'NR > 1 { printf "%%0A" } { printf "%s", $0 }'; }

fail() {
  # The budget's error has been said, and the command its stop ended says nothing more.
  [ -e "$expired" ] && exit 1
  # The log's last lines whatever their level, since a game that died in native code logged no
  # warning, and the dumps it left. The game's output and its errors both go to its log.
  local ending="" left="" gone="" kernel="" memory="" more=""
  [ -f "$log" ] && ending=$(tail -n 3 "$log" | tr -s '\r\n' '  ' | sed 's/ *$//; s/\.$//')
  [ -d "$dumps" ] && left=$(ls "$dumps" 2> /dev/null | grep -i "^$game" | tr '\n' ' ' | sed 's/ *$//' || true)
  if grep -qE 'NO_SESSION|SESSION_UNREACHABLE' "$said" 2> /dev/null; then
    gone=$(whereabouts)
    if [ -n "$pid" ] && ended; then died=yes; fi
  fi
  # A game that died on Linux adds the kernel's last lines, which name what the out-of-memory killer
  # chose or a fault the kernel saw, and the processes holding the most memory as it failed, said
  # on the step's output as well. The runner's sudo asks no password, and elsewhere dmesg is tried
  # as the user.
  if [ "$died" = yes ] && [ "$system" = Linux ]; then
    kernel=$( { sudo -n dmesg 2> /dev/null || dmesg 2> /dev/null || true; } | tail -n 30)
    [ -n "$kernel" ] || kernel="(none, since dmesg could not be read)"
    memory=$(ps -eo pid,rss,etime,args --sort=-rss 2> /dev/null | head | cut -c1-200 || true)
    printf "The kernel's last lines:\n%s\nThe processes holding the most memory, in kilobytes:\n%s\n" "$kernel" "$memory" >&3
    more="%0A%0AThe kernel's last lines:%0A$(escaped "$kernel")%0A%0AThe processes holding the most memory, in kilobytes:%0A$(escaped "$memory")"
  fi
  echo "::error title=$heading::$game: $1${gone:+, and $gone}.${ending:+ Its log ends with $ending.}${left:+ It left the dump $left.}$more" >&3
  exit 1
}
# A command e3d refuses fails with its code and sentence, which it writes to standard error, kept in
# a file for the error.
said="captures/$name-said.txt"
cmd() { ./e3d command "$@" --quiet --timeout 600 2> "$said" || fail "./e3d command $* ended with $?, $(head -n 1 "$said")"; }
ask() {
  local answer
  answer=$(./e3d command "$1" --timeout 600 2> "$said") || fail "./e3d command $1 ended with $?, $(head -n 1 "$said")"
  printf '%s\n' "$answer" > "$last"
  printf '%s\n' "$answer"
}
# How far apart two points of two status lines are, each the numbers after a word.
moved() {
  printf '%s\n%s\n' "$1" "$2" | awk -v word="$3" '
    { for (i = 1; i <= NF; i++) if ($i == word) for (j = i + 1; j <= NF && $j ~ /^-?[0-9.]+$/; j++) p[NR, j - i] = $j }
    END { for (k = 1; k <= 3; k++) { d = p[2, k] - p[1, k]; sum += d * d }; printf "%.2f", sqrt(sum) }'
}

dotnet restore "games/$game" --force-evaluate > /dev/null || fail "did not restore"
dotnet build "games/$game" --no-restore > /dev/null || fail "did not build from the package"
# The servers the builds before the game leave running, MSBuild's nodes and the compiler's, are shut
# down before it is played, since on a runner they hold memory the game and lavapipe need, and a game
# the kernel kills for memory says nothing in its log.
dotnet build-server shutdown > /dev/null 2>&1 || true
folder="games/$game/bin/Debug/net10.0"
rm -f "$folder/tempo-best.txt" "$folder/tempo-offset.txt" "$folder/manor-settings.txt" "$folder/tactics-save.json" "$folder/rally-best.txt" \
  "$folder/wordfall-shot.png" "$folder/slide-best.txt" "$folder/jelly-best.rae" "$folder/jelly-best.txt"

# The budget is kept by a watcher that wakes every five seconds, so it ends soon after the game
# does. It starts before the opening, so a game that hangs there is stopped at its budget too.
(
  for (( waited = 0; waited < minutes * 60; waited += 5 )); do
    sleep 5
    [ -e "$finished" ] && exit 0
  done
  : > "$expired"
  echo "::error title=$heading::$game: was not played through in $(( minutes * 60 )) seconds on $system, as far as $(cat "$last" 2> /dev/null || echo "its opening")" >&3
  ./e3d stop --quiet > /dev/null 2>&1 || true
) &

# An opening that fails says why. On Linux and macOS that is how the game's process ended, or that
# it still runs and has not served. On Windows e3d's answer is kept, so an opening it refuses says
# e3d's code and sentence and the last lines of the log it names, read from the path it gives,
# which perl reads from the answer.
if ! start; then
  if [ "$system" != Windows ]; then
    if [ -n "$pid" ] && ended; then died=yes; fail "did not open, since $(how)"; fi
    fail "did not serve within a minute of starting${pid:+, its process $pid still running}"
  fi
  answer=$(cat "$opened")
  said=$(printf '%s' "$answer" | perl -MJSON::PP -0777 -ne '
    my $answer = eval { decode_json($_) } or do { s/\s+/ /g; print length ? "an answer that is not JSON, $_\n" : "nothing\n"; exit };
    my $error = $answer->{errors}[0] || {};
    (my $sentence = $error->{message} // "") =~ s/\.$//;
    print "$error->{code}, $sentence\n", $answer->{data}{log} // "", "\n";')
  named=$(printf '%s\n' "$said" | sed -n 2p)
  # Git's bash reads a Windows path once cygpath has turned it.
  if [ -n "$named" ] && command -v cygpath > /dev/null; then named=$(cygpath -u "$named"); fi
  ending=""
  [ -n "$named" ] && [ -f "$named" ] && ending=$(tail -n 5 "$named" | tr -s '\r\n' '  ' | sed 's/ *$//')
  # The log's lines are said here whole, so fail's from it are left out.
  log=""
  fail "did not open, e3d said $(printf '%s\n' "$said" | head -n 1)${ending:+, and the log ends with $ending}"
fi
cmd window.size 480 270
cmd frames.wait 30

case "$game" in
  Hopper)
    before=$(ask hopper.status)
    cmd input.key Right 40
    cmd input.button 0 RightFaceDown 2
    cmd input.key Right 120
    after=$(ask hopper.status)
    echo "$before / $after"
    distance=$(moved "$before" "$after" at)
    awk -v d="$distance" 'BEGIN { exit !(d > 64) }' || fail "the player did not run along the level, $before then $after"
    ;;
  Summit)
    cmd frames.wait 30
    cmd input.key Enter 2
    before=$(ask summit.where)
    cmd input.key W 120
    after=$(ask summit.where)
    echo "$before / $after"
    distance=$(moved "at $before" "at $after" at)
    awk -v d="$distance" 'BEGIN { exit !(d > 1) }' || fail "the player did not walk, $before then $after"
    ;;
  Swarm)
    cmd swarm.invulnerable true
    cmd input.key Enter 2
    cmd frames.wait 600
    status=$(ask swarm.status)
    echo "$status"
    case "$status" in *"fallen 0"*|"") fail "no creature fell in the first wave, $status" ;; esac
    # The script changed while the game runs is compiled again, with perl, which macOS and Windows'
    # Git bash both have, the change taken back after.
    perl -pi -e 's/tuning\.EnemySpeed = 1;/tuning.EnemySpeed = 0.5f;/' games/Swarm/source/behaviors/Tune.cs
    restore='perl -pi -e "s/tuning\.EnemySpeed = 0\.5f;/tuning.EnemySpeed = 1;/" games/Swarm/source/behaviors/Tune.cs'
    cmd frames.wait 300
    grep -q "Hot-reload behaviors: Compiled" "$log" || fail "the script changed while it ran was not compiled again"
    ;;
  Rally)
    cmd frames.wait 30
    cmd rally.autopilot true
    cmd input.key Enter 2
    status=""
    for i in $(seq 1 40); do
      status=$(ask rally.status)
      case "$status" in *"laps []"*) cmd frames.wait 300 ;; *) break ;; esac
    done
    echo "$status"
    case "$status" in *"laps []"*|"") fail "the autopilot did not finish a lap, $status" ;; esac
    ;;
  Manor)
    cmd frames.wait 30
    # Settings from the title, vertical sync turned off, back, and a walk begun, each press held a
    # frame, as the Linux step presses them.
    pad() { cmd input.button 0 "$1" 1; cmd frames.wait 4; }
    pad LeftFaceDown; pad RightFaceDown; pad LeftFaceDown; pad LeftFaceDown; pad RightFaceDown; pad RightFaceRight; pad LeftFaceUp; pad RightFaceDown
    screen=$(ask manor.status)
    case "$screen" in Play*) ;; *) fail "the pad's way through Settings to a walk ended on another screen, $screen" ;; esac
    cmd manor.autopilot true
    status=""
    for i in $(seq 1 60); do
      status=$(ask manor.status)
      case "$status" in Finished*) break ;; *) cmd frames.wait 300 ;; esac
    done
    echo "$status"
    case "$status" in Finished*) ;; *) fail "the autopilot did not find every lantern, $status" ;; esac
    grep -q "vsync = False" "$folder/manor-settings.txt" || fail "the setting changed with the pad was not kept in the settings file"
    ;;
  Tactics)
    cmd tactics.new 7
    cmd tactics.autopilot true
    status=""
    for i in $(seq 1 60); do
      status=$(ask tactics.status)
      case "$status" in Over*) break ;; *) cmd frames.wait 300 ;; esac
    done
    echo "$status"
    case "$status" in Over*) ;; *) fail "the match the computer plays for both sides did not end, $status" ;; esac
    ;;
  Tempo)
    cmd tempo.autopilot true
    cmd input.key Enter 2
    # The song is a minute of the music's time, which a slow device reaches later.
    status=""
    for i in $(seq 1 60); do
      status=$(ask tempo.status)
      case "$status" in Results*) break ;; *) cmd frames.wait 60 ;; esac
    done
    echo "$status"
    case "$status" in Results*"miss 0 "*) ;; *) fail "the autopilot did not play every note of the song within its windows, $status" ;; esac
    ;;
  Sumo)
    # The second player's pad starts the match and rolls its marble, the first player's key
    # rolls theirs, and both then play themselves until one has won. Each is held 15 frames, which
    # on a device drawing at the slowest frame the game steps whole, a twentieth of a second, rolls
    # a marble less than half the ring, and at sixty frames a second a fifth of a unit.
    cmd input.button 1 RightFaceDown 2
    cmd frames.wait 200
    before=$(ask sumo.status)
    cmd input.key D 15
    cmd input.axis 1 LeftX 1
    cmd frames.wait 15
    cmd input.axis 1 LeftX 0
    after=$(ask sumo.status)
    echo "$before / $after"
    first=$(moved "$before" "$after" p1)
    second=$(moved "$before" "$after" p2)
    awk -v a="$first" -v b="$second" 'BEGIN { exit !(a > 0.1 && b > 0.1) }' || fail "a key and the second pad did not roll both marbles, $before then $after"
    cmd sumo.autopilot true
    status=""
    for i in $(seq 1 60); do
      status=$(ask sumo.status)
      case "$status" in Won*) break ;; *) cmd frames.wait 300 ;; esac
    done
    echo "$status"
    case "$status" in Won*) ;; *) fail "the match the marbles play themselves did not end, $status" ;; esac
    ;;
  Wordfall)
    # A list of words with accents dropped on the window is played with, the word falling lowest
    # is typed through the text input, a character no word starts with is a miss, and the
    # autopilot types until the town is buried, after which C copies the result and reads it back
    # from the clipboard and F12 saves a screenshot beside the game.
    list="$PWD/captures/wordfall-words.txt"
    printf 'caf\xc3\xa9\nna\xc3\xafve\nfianc\xc3\xa9\nd\xc3\xa9j\xc3\xa0\nsm\xc3\xb6rg\xc3\xa5sbord\n' > "$list"
    if command -v cygpath > /dev/null; then list=$(cygpath -m "$list"); fi
    cmd input.drop "$list"
    cmd frames.wait 5
    title=$(ask wordfall.status)
    case "$title" in *"words 5 from wordfall-words.txt"*) ;; *) fail "the dropped list of words was not taken, $title" ;; esac
    cmd input.key Enter 2
    status=""
    for i in $(seq 1 40); do
      status=$(ask wordfall.status)
      case "$status" in *"falling -"*) cmd frames.wait 10 ;; *) break ;; esac
    done
    word=$(printf '%s' "$status" | sed 's/.*falling \([^ ,]*\).*/\1/')
    cmd input.text "$word"
    cmd frames.wait 5
    typed=$(ask wordfall.status)
    echo "$status / typed $word / $typed"
    case "$typed" in *" words 1 "*) ;; *) fail "typing $word did not clear it, $typed" ;; esac
    cmd input.text 9
    cmd frames.wait 5
    missed=$(ask wordfall.status)
    case "$missed" in *" misses 1 "*) ;; *) fail "a character no word starts with was not a miss, $missed" ;; esac
    cmd wordfall.autopilot true
    for i in $(seq 1 60); do
      status=$(ask wordfall.status)
      case "$status" in Over*) break ;; *) cmd frames.wait 300 ;; esac
    done
    cmd input.key C 2
    cmd input.key F12 2
    cmd frames.wait 5
    status=$(ask wordfall.status)
    echo "$status"
    case "$status" in Over*"copied yes shots 1"*) ;; *) fail "the game did not end with its result copied and a screenshot saved, $status" ;; esac
    [ -f "$folder/wordfall-shot.png" ] || fail "F12 wrote no screenshot beside the game"
    ;;
  Slide)
    # A tap in the window's middle starts a game, a quick drag up or down swipes the tiles, a
    # double click takes the move back as a double tap, a finger held a second and a half starts
    # again, and the autopilot plays until no move is left. A gesture is timed on the game's
    # clock, a swipe a drag let go faster than 0.2 of the window a second and a double tap two
    # taps within 0.3 seconds, so each frame is made a sixtieth of a second first, since where a
    # device drew the drag's ten frames in two seconds, the swipe slid nothing.
    cmd time.frame 0.0166667
    cmd input.click 240 135
    cmd frames.wait 10
    cmd input.drag Left 0 -120 10
    cmd frames.wait 10
    swiped=$(ask slide.status)
    case "$swiped" in *" moves 1 "*) ;; *)
      cmd input.drag Left 0 120 10
      cmd frames.wait 10
      swiped=$(ask slide.status) ;;
    esac
    case "$swiped" in Play*" moves 1 "*) ;; *) fail "a swipe up or down did not slide the tiles, $swiped" ;; esac
    cmd input.click 240 135 2
    cmd frames.wait 10
    undone=$(ask slide.status)
    case "$undone" in *" moves 0 undos 1 "*) ;; *) fail "a double tap did not take the move back, $undone" ;; esac
    cmd input.touch 0 240 135 90
    cmd frames.wait 10
    restarted=$(ask slide.status)
    case "$restarted" in Play*" moves 0 undos 0 "*) ;; *) fail "a held finger did not start again, $restarted" ;; esac
    echo "$swiped / $undone / $restarted"
    cmd slide.autopilot true
    status=""
    for i in $(seq 1 60); do
      status=$(ask slide.status)
      case "$status" in Over*) break ;; *) cmd frames.wait 300 ;; esac
    done
    echo "$status"
    case "$status" in Over*) ;; *) fail "the game the autopilot plays did not end, $status" ;; esac
    ;;
  Jelly)
    # A run started and steered by keys until it crashes, then watched again from its recorded
    # input, which ends where it did, and the best run, written to a file, watched from the title
    # of the game opened again, which ends there too.
    cmd input.key Enter 2
    cmd frames.wait 60
    cmd input.key Left 2
    cmd frames.wait 30
    cmd input.key Space 2
    ran=""
    for i in $(seq 1 60); do
      ran=$(ask jelly.status)
      case "$ran" in Crashed*) break ;; *) cmd frames.wait 60 ;; esac
    done
    case "$ran" in Crashed*) ;; *) fail "the run did not end, $ran" ;; esac
    ending="${ran% watched *}"
    cmd input.key R 2
    watched=""
    for i in $(seq 1 60); do
      watched=$(ask jelly.status)
      case "$watched" in *"watched this-run") break ;; *) cmd frames.wait 60 ;; esac
    done
    echo "$ran / $watched"
    [ "${watched% watched *}" = "$ending" ] || fail "the run watched again ended elsewhere, $ending then $watched"
    ./e3d stop --quiet
    start || fail "did not open again${pid:+, and $(whereabouts)}"
    cmd window.size 480 270
    cmd frames.wait 10
    cmd input.key B 2
    best=""
    for i in $(seq 1 60); do
      best=$(ask jelly.status)
      case "$best" in *"watched the-best-run") break ;; *) cmd frames.wait 60 ;; esac
    done
    echo "$best"
    [ "${best% watched *}" = "$ending" ] || fail "the best run watched from its file ended elsewhere, $ending then $best"
    ;;
  Wick)
    # Walked east from the start with the keys, then lit through by the autopilot, which walks
    # round the walls and the pits to each wick and out by the door once every wick burns.
    before=$(ask wick.status)
    cmd input.key Enter 2
    cmd input.key D 60
    after=$(ask wick.status)
    echo "$before / $after"
    distance=$(moved "$before" "$after" at)
    awk -v d="$distance" 'BEGIN { exit !(d > 1) }' || fail "the player did not walk, $before then $after"
    cmd wick.autopilot true
    status=""
    for i in $(seq 1 120); do
      status=$(ask wick.status)
      case "$status" in Won*) break ;; *) cmd frames.wait 120 ;; esac
    done
    echo "$status"
    case "$status" in Won*"lit 4 of 4 falls 0 "*) ;; *) fail "the autopilot did not light every wick and leave by the door, $status" ;; esac
    ;;
  *)
    fail "is not a game this script plays"
    ;;
esac

./e3d shot "captures/$name-driven.png" --quiet || true
./e3d stop --quiet
cp "$log" "captures/$name-driven.log"
grep -q "Validation layers: ENABLED" "$log" || fail "drew without the validation layer"
if grep -F 'Validation Error' "$log"; then fail "the validation layer reported an error"; fi
echo "$game played through ./e3d $where on $system, its play asserted, with no validation error"

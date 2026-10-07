#!/usr/bin/env bash
# Plays a game from the package through ./e3d, offscreen and under the validation layer, as the
# build workflow's examples job plays it on Linux, and asserts its walk or its win, so a game's
# input, its sound and the session ./e3d drives are tried on the system it runs on. The workflow
# runs it for every game on Windows and macOS after build/play-game.sh, which packs the engine.
#
#   build/drive-game.sh <Pusher|Hopper|Summit|Swarm|Rally|Manor|Tactics|Tempo|Sumo>
#
# What fails is said as an error annotation naming the game and the system, with the last warnings
# of the game's log, so the page says why a game cannot run there. Each game is drawn at 480 by
# 270, since what is asserted is its play and not its picture, and a device drawing on its CPU, as
# lavapipe on a Windows runner is, plays more of it in the time.
set -euo pipefail

game="$1"
cd "$(dirname "$0")/.."
mkdir -p captures
name=$(printf '%s' "$game" | tr '[:upper:]' '[:lower:]')
# The system by the name a reader knows it by, where Git's bash on Windows calls itself MINGW64.
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) system=Windows ;;
  Darwin) system=macOS ;;
  *) system=$(uname -s) ;;
esac
log="build/sessions/$game.log"

fail() {
  local warnings=""
  [ -f "$log" ] && warnings=$(grep -E '\[(WARN |ERROR|FATAL)\]' "$log" | tail -n 3 | tr '\n' ' ' || true)
  echo "::error title=$game on $system::$game: $1. ${warnings}"
  ./e3d stop --quiet > /dev/null 2>&1 || true
  exit 1
}
cmd() { ./e3d command "$@" --quiet --timeout 600 || fail "./e3d command $* ended with $?"; }
ask() { ./e3d command "$1" --timeout 600 || fail "./e3d command $1 ended with $?"; }
# How far apart two points of two status lines are, each the numbers after a word.
moved() {
  printf '%s\n%s\n' "$1" "$2" | awk -v word="$3" '
    { for (i = 1; i <= NF; i++) if ($i == word) for (j = i + 1; j <= NF && $j ~ /^-?[0-9.]+$/; j++) p[NR, j - i] = $j }
    END { for (k = 1; k <= 3; k++) { d = p[2, k] - p[1, k]; sum += d * d }; printf "%.2f", sqrt(sum) }'
}

dotnet restore "games/$game" --force-evaluate > /dev/null || fail "did not restore"
dotnet build "games/$game" --no-restore > /dev/null || fail "did not build from the package"
folder="games/$game/bin/Debug/net10.0"
rm -f "$folder/tempo-best.txt" "$folder/tempo-offset.txt" "$folder/manor-settings.txt" "$folder/tactics-save.json" "$folder/rally-best.txt"

# The path without the .exe a Windows build gives it, which ./e3d finds. Its answer is kept, so an
# opening it refuses says e3d's code and sentence and the last lines of the log it names, read
# from the path it gives, which perl reads from the answer on every system the job runs on.
if ! answer=$(ENGINE_VULKAN_VALIDATION=1 ./e3d open "$folder/$game" --offscreen --json); then
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
  # The log's lines are said here whole, so fail's warnings from it are left out.
  log=""
  fail "did not open, e3d said $(printf '%s\n' "$said" | head -n 1)${ending:+, and the log ends with $ending}"
fi
trap './e3d stop --quiet > /dev/null 2>&1 || true' EXIT
cmd window.size 480 270
cmd frames.wait 30

case "$game" in
  Pusher)
    before=$(ask pusher.status)
    cmd input.key Enter 2
    cmd input.key W 90
    after=$(ask pusher.status)
    echo "$before / $after"
    distance=$(moved "$before" "$after" at)
    awk -v d="$distance" 'BEGIN { exit !(d > 1) }' || fail "the player did not walk, $before then $after"
    ;;
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
    trap 'perl -pi -e "s/tuning\.EnemySpeed = 0\.5f;/tuning.EnemySpeed = 1;/" games/Swarm/source/behaviors/Tune.cs; ./e3d stop --quiet > /dev/null 2>&1 || true' EXIT
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
  *)
    fail "is not a game this script plays"
    ;;
esac

./e3d shot "captures/$name-driven.png" --quiet || true
./e3d stop --quiet
cp "$log" "captures/$name-driven.log"
grep -q "Validation layers: ENABLED" "$log" || fail "drew without the validation layer"
if grep -F 'Validation Error' "$log"; then fail "the validation layer reported an error"; fi
echo "$game played through ./e3d on $system, its play asserted, with no validation error"

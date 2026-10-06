# Reads what build/soak.sh wrote and fails when something a program holds climbs without leveling
# off, or when a game could not be played through. The first quarter of the readings, while the
# program loads and fills its caches, is left out, and the rest is cut in two halves: the least a
# value fell to in the second half may pass the least it fell to in the first by a little, the slack
# each kind of value is given, and no more. A leak raises a value's least as well as its most, where
# a level streamed in and let go swings between them, as Manor's buffers go from 521 to 618 and
# back as its walk passes rooms of more cells and fewer, so the least tells the one from the other
# however much of the play a slow device gets through.
#
#   python3 build/soak-check.py build/soak/<name>.csv [...] [--sessions DIR]
#
# Each game that fails is named with what failed and its numbers, and under GitHub Actions in an
# error annotation as well, which a reader not signed in sees (NORM.md, N 6.7): the value that
# climbed, from the least of the first half to the least of the second against the bound, or the
# command that ended its soak with its exit code, which build/soak.sh writes beside the readings as
# <name>.failed, and the last warnings of the game's own log under build/sessions, or the folder
# --sessions gives. The process's resident memory at the first reading kept and at the last is
# said beside them, and not judged, since a driver's caches move it.
import os
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from page import annotate, fit

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
arguments = sys.argv[1:]
SESSIONS = os.path.join(ROOT, "build", "sessions")
if "--sessions" in arguments:
    at = arguments.index("--sessions")
    SESSIONS = arguments[at + 1]
    del arguments[at:at + 2]
LOG_LINES = 3

# How far past the first half's least each value's least in the second may go: a share of it and a
# fixed amount.
SLACK = {
    "heap": (0.15, 2 << 20),
    "usedBytes": (0.10, 1 << 20),
    "blockBytes": (0.0, 64 << 20),
    "memoryBlocks": (0.0, 1),
    "buffers": (0.05, 4),
    "images": (0.05, 2),
    "descriptorSets": (0.05, 4),
    "pipelines": (0.0, 2),
    "entities": (0.10, 10),
    "entityIds": (0.10, 10),
    "assets": (0.10, 4),
}


def read(path):
    rows = []
    for line in open(path, encoding="utf-8"):
        words = line.split()
        if len(words) < 3:
            continue
        values = {words[i]: int(words[i + 1]) for i in range(1, len(words) - 1, 2) if words[i + 1].lstrip("-").isdigit()}
        rows.append(values)
    return rows


def ended(path):
    """The exit code, the program and the command that ended a game's soak, or None where it ran out its time."""
    failed = os.path.splitext(path)[0] + ".failed"
    if not os.path.exists(failed):
        return None
    with open(failed, encoding="utf-8") as text:
        code, program, command = (text.read().strip().split(" ", 2) + ["", ""])[:3]
    return code, program, command


def warnings(program):
    """The last lines at a warning or worse of a program's session log."""
    log = os.path.join(SESSIONS, program + ".log")
    if not program or not os.path.exists(log):
        return []
    with open(log, encoding="utf-8", errors="replace") as text:
        found = [line.rstrip() for line in text if "[WARN ]" in line or "[ERROR]" in line or "[FATAL]" in line]
    return found[-LOG_LINES:]


def megabytes(count):
    return f"{count / (1 << 20):.0f} MB"


failed = False
for path in arguments:
    name = os.path.splitext(os.path.basename(path))[0]
    rows = read(path)
    problems = []
    if (stop := ended(path)) is not None:
        code, program, command = stop
        problems.append(f"{name} could not be played through, `{command}` ending with {code}")
        problems += [f"  {line}" for line in warnings(program)]
    if len(rows) < 6:
        problems.append(f"{name}: {len(rows)} readings, too few to judge")
    else:
        kept = rows[len(rows) // 4:]
        first, second = kept[: len(kept) // 2], kept[len(kept) // 2:]
        for measure, (share, amount) in SLACK.items():
            if measure not in kept[0]:
                continue
            before = min(r[measure] for r in first)
            after = min(r[measure] for r in second)
            bound = before * (1 + share) + amount
            climbs = after > bound
            print(f"{path}: {measure:15} least {before:>12} then {after:>12} (bound {int(bound)}) {'CLIMBS' if climbs else 'ok'}")
            if climbs:
                problems.append(f"{name}: {measure} climbed from {before} to {after} at its least, past its bound of {int(bound)}")
        if "resident" in kept[0] and "resident" in kept[-1]:
            resident = f"{name}: resident memory {megabytes(kept[0]['resident'])} at the first reading kept and {megabytes(kept[-1]['resident'])} at the last"
            print(resident)
            if problems:
                problems.append(resident)
    if problems:
        failed = True
        for line in problems:
            print(line)
        annotate("error", f"Soak, {name}", fit(problems))
sys.exit(1 if failed else 0)

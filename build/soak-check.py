# Reads what build/soak.sh wrote and fails when something a program holds climbs without leveling
# off, or when a game could not be played through. The first quarter of the readings, while the
# program loads and fills its caches, is left out, and the rest is cut in two halves, and the least
# a value fell to in the second half may pass the least it fell to in the first by a little, the
# slack each kind of value is given, and no more. A leak raises a value's least as well as its most,
# where a level streamed in and let go swings between them, as Manor's buffers go from 96 to 158 and
# back as its walk passes from the grounds into the house, so the least tells the one from the other
# where both halves see the level. A game that streams its level gives the most a whole walk through
# it holds of such a value, which bounds the value in place of the first half's least.
#
#   python3 build/soak-check.py build/soak/<name>.csv [...] [--sessions DIR]
#
# Each game that fails is named with what failed and its numbers, and under GitHub Actions in an
# error annotation as well, which a reader not signed in sees (NORM.md, N 6.7), with the value that
# climbed, from the least of the first half to the least of the second against the bound, where a
# game that walks a level stood at each half's least and at each half's first and last reading, or
# the command that ended its soak with its exit code, which build/soak.sh writes beside the readings
# as <name>.failed, how the game ended where build/open-game.sh's shell wrote it, and the last
# warnings of the game's own log under build/sessions, or the folder --sessions gives, with the
# lines the engine writes as a program's main loop is asked to end, which say what asked. The process's
# resident memory at the first reading kept and at the last is said beside them, and not judged,
# since a driver's caches move it.
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

# How far past the first half's least each value's least in the second may go, a share of it and a
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


# The most a game holds of a value over a whole walk through its level or a whole turn of its soak,
# which bounds the second half's least with the same slack in place of the first half's. On a device
# that plays two turns in the soak's two minutes, as the workflow's runner drawing every game at
# once on four cores does, Manor's walk enters rooms the first half never reached and the least
# climbs with the world, as its buffers did from 133 to 149 on lavapipe at a core and its images,
# descriptor sets, entity ids and assets on the pack run of 683e6e9d, with nothing leaking. Its
# buffers are a vertex and an index buffer for each mesh of the models its cells show, 96 in the
# grounds and at most 158 in the house over a whole walk on the development machine's GPU and 162 on
# lavapipe, which the device's census named (`memory.buffers`). The other five are the most two
# whole walks of its autopilot held at 320 by 180 on both devices, read by `memory.collect` every 60
# frames, the second walk holding no more than the first. Its entities are the most alive at once,
# the 539 its entity ids count, which every whole walk reached on both devices at the game's own
# pace and with each frame a tenth or a quarter of a second (`time.frame`), as a runner's slow
# frames count more of the game's time, where the readings found 464 to 533. Bounded by the first
# half's least alone, 374 entities, a walk on the pack run of 80625742 held 422 at the second half's
# least, past the 421 that gave, its halves seeing different parts of the estate. Swarm's soak sets
# wave 3 at each turn, and a turn leaves nothing alive past the next one's reset, 27 entities after
# each of thirty, but a frame on a slower device counts more of the game's time, up to the quarter
# second Time clamps it to, so a turn of 300 frames reaches waves 4 to 6 there and holds more
# entities at once, and their ids, which count the most ever alive at once, climb with it. Thirty
# turns with every frame counting that quarter second (`time.frame 0.25`) held 152 ids at most on
# the GPU and 151 on lavapipe.
WHOLE = {
    "manor": {"buffers": 162, "images": 92, "descriptorSets": 95, "entities": 539, "entityIds": 539, "assets": 96},
    "swarm": {"entityIds": 152},
}


def read(path):
    """Each reading's figures, by name, and where the game stood, after a bar, under "where"."""
    rows = []
    for line in open(path, encoding="utf-8"):
        figures, _, where = line.partition(" | ")
        words = figures.split()
        if len(words) < 3:
            continue
        values = {words[i]: int(words[i + 1]) for i in range(1, len(words) - 1, 2) if words[i + 1].lstrip("-").isdigit()}
        if where.strip():
            values["where"] = where.strip()
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


# What the engine writes as a program's main loop is asked to end, each saying what asked, so a game
# that ended of its own, with an exit code of 0, says why. The pack run of c721746b read Tumble
# ending so in its soak's first seconds, its log not kept and its warnings none (Verdict 91).
CLOSING = ("WindowShouldClose returns true from here on", "The app exits, since", "app.quit was asked by", "frame(s) asked for",
           "closing before it was ready")


def warnings(program):
    """The last lines at a warning or worse of a program's session log, with those saying what ended
    its main loop."""
    log = os.path.join(SESSIONS, program + ".log")
    if not program or not os.path.exists(log):
        return []
    with open(log, encoding="utf-8", errors="replace") as text:
        found = [line.rstrip() for line in text
                 if "[WARN ]" in line or "[ERROR]" in line or "[FATAL]" in line or any(said in line for said in CLOSING)]
    return found[-LOG_LINES:]


def how(program):
    """How a program build/open-game.sh opened ended, its exit code or signal and what gdb read from
    a core it left, as its shell wrote it beside the program's log."""
    path = os.path.join(SESSIONS, program + ".ended")
    if not program or not os.path.exists(path):
        return []
    with open(path, encoding="utf-8", errors="replace") as text:
        return [line.rstrip() for line in text][:40]


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
        problems += [f"  {line}" for line in how(program) + warnings(program)]
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
            if measure in WHOLE.get(name, {}):
                bound = max(bound, WHOLE[name][measure] * (1 + share) + amount)
            climbs = after > bound
            print(f"{path}: {measure:15} least {before:>12} then {after:>12} (bound {int(bound)}) {'CLIMBS' if climbs else 'ok'}")
            if climbs:
                problems.append(f"{name}: {measure} climbed from {before} to {after} at its least, past its bound of {int(bound)}")
                for label, half in (("first", first), ("second", second)):
                    if "where" in half[0]:
                        least = min(half, key=lambda r: r[measure])
                        problems.append(f"  the {label} half's least where it stood at {least.get('where', 'no word')}, the half from "
                                        f"{half[0].get('where', 'no word')} to {half[-1].get('where', 'no word')}")
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

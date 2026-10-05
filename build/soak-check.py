# Reads what build/soak.sh wrote and fails when something a program holds climbs without leveling
# off. The first quarter of the readings, while the program loads and fills its caches, is left
# out, and the rest is cut in two halves: the most a value reached in the second half may pass the
# most it reached in the first by a little, the slack each kind of value is given, and no more.
#
#   python3 build/soak-check.py build/soak/<name>.csv [...]
import sys

# How far past the first half's most each value may go: a share of it and a fixed amount.
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
}

def read(path):
    rows = []
    for line in open(path):
        words = line.split()
        if len(words) < 3:
            continue
        values = {words[i]: int(words[i + 1]) for i in range(1, len(words) - 1, 2) if words[i + 1].lstrip("-").isdigit()}
        rows.append(values)
    return rows

failed = False
for path in sys.argv[1:]:
    rows = read(path)
    if len(rows) < 6:
        print(f"{path}: {len(rows)} readings, too few to judge")
        failed = True
        continue
    kept = rows[len(rows) // 4:]
    first, second = kept[: len(kept) // 2], kept[len(kept) // 2:]
    for name, (share, amount) in SLACK.items():
        if name not in kept[0]:
            continue
        before = max(r[name] for r in first)
        after = max(r[name] for r in second)
        bound = before * (1 + share) + amount
        state = "ok" if after <= bound else "CLIMBS"
        if after > bound:
            failed = True
        print(f"{path}: {name:15} {before:>12} then {after:>12} (bound {int(bound)}) {state}")
sys.exit(1 if failed else 0)

#!/usr/bin/env python3
# Counts how many of raylib.h's functions the flat API carries, by name, against the lines of
# CHEATSHEET.md, which a test keeps equal to Engine3D's public functions, and names the rest by the
# section of raylib.h they are declared in.
#
#   build/raylib-bench/coverage.py [path to raylib.h] [--check]
#
# With no path, raylib.h is read from the checkout build/examples-table.py reads, of the commit
# build/raylib-bench/run.sh pins, cloned when there is none. --check reads
# docs/compared-with-raylib.md, whose two tables answer each function not carried with its C# or
# its reason, and fails where a function is not carried and the page names none of it, where the
# page names one that is carried, or where the page's counts differ from these (NORM.md, N 5.2).
import importlib.util
import re
import sys
from collections import OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PAGE = ROOT / "docs" / "compared-with-raylib.md"

arguments = [a for a in sys.argv[1:] if a != "--check"]
check = "--check" in sys.argv[1:]
if arguments:
    header_path = Path(arguments[0])
else:
    # The table's own way to the pinned checkout, so both read the same raylib, imported without
    # leaving its compiled bytes in build/.
    sys.dont_write_bytecode = True
    spec = importlib.util.spec_from_file_location("examples_table", ROOT / "build" / "examples-table.py")
    table = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(table)
    header_path = Path(table.raylib_source(table.raylib_commit(), None)) / "src" / "raylib.h"

header = header_path.read_text()
cheatsheet = (ROOT / "CHEATSHEET.md").read_text()
carried = set(re.findall(r"^[\w<>\[\],? ]+?\b(\w+)(?:<\w+>)?\(", cheatsheet, re.M))

sections = OrderedDict()
section = "(none)"
for line in header.splitlines():
    # A section is a comment naming functions, as "// Window-related functions" or "// Camera
    # System Functions (Module: rcamera)", and not a NOTE or WARNING beneath one.
    heading = re.match(r"^//\s*([A-Z][\w\s\-:,.]*?functions[\w\s:,.]*?)\s*(\(Module.*)?$", line.strip(), re.I)
    if heading and not re.match(r"(NOTE|WARNING)", heading.group(1)):
        section = heading.group(1).strip()
    match = re.match(r"^RLAPI\s+[\w\s\*]+?\b(\w+)\s*\(", line)
    if match:
        sections.setdefault(section, []).append(match.group(1))

names = [n for group in sections.values() for n in group]
have = [n for n in names if n in carried]
percent = round(100 * len(have) / len(names))
print(f"{len(have)} of {len(names)} functions in raylib.h are carried, {100 * len(have) / len(names):.0f} percent")
for name, group in sections.items():
    missing = [n for n in group if n not in carried]
    if missing:
        print(f"\n{name}: {len(group) - len(missing)} of {len(group)} carried, left out:")
        print("  " + ", ".join(missing))

if check:
    page = PAGE.read_text()
    # The two tables, from the heading of the first to the next heading after the second.
    start = page.index("## raylib's functions C# has")
    end = page.index("\n## ", page.index("## Not carried"))
    # The functions a row answers are named in its first cell, the rest of the row saying what
    # answers them, which may name a function that is carried.
    firsts = [line.split("|")[1] for line in page[start:end].splitlines() if line.startswith("| ") and not line.startswith("| raylib |")]
    answered = {n for cell in firsts for n in re.findall(r"`(\w+)`", cell)} & set(names)
    left_out = set(names) - set(have)
    problems = [f"{n} is not carried, and docs/compared-with-raylib.md says nothing of it" for n in sorted(left_out - answered)]
    problems += [f"{n} is carried, and docs/compared-with-raylib.md answers it as not carried" for n in sorted(answered - left_out)]
    for counted in (f"{len(have)} of the {len(names)} functions in `raylib.h` are carried, {percent} percent",
                    f"| Functions of `raylib.h` carried | {len(names)} | {len(have)} ({percent} percent) |"):
        if counted not in " ".join(page.split()):
            problems.append(f"docs/compared-with-raylib.md does not say \"{counted}\"")
    for problem in problems:
        print(f"::error::{problem}")
    if problems:
        sys.exit(f"{len(problems)} difference(s) between docs/compared-with-raylib.md and raylib.h's functions carried")
    print(f"\ndocs/compared-with-raylib.md answers each of the {len(left_out)} functions not carried")

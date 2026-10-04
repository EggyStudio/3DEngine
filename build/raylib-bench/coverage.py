#!/usr/bin/env python3
# Counts how many of raylib.h's functions the flat API carries, by name, against the lines of
# CHEATSHEET.md, which a test keeps equal to Engine3D's public functions, and names the rest by the
# section of raylib.h they are declared in.
#
#   build/raylib-bench/coverage.py <path to raylib.h>
import re
import sys
from collections import OrderedDict
from pathlib import Path

header = Path(sys.argv[1]).read_text()
cheatsheet = (Path(__file__).resolve().parents[2] / "CHEATSHEET.md").read_text()
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
print(f"{len(have)} of {len(names)} functions in raylib.h are carried, {100 * len(have) / len(names):.0f} percent")
for name, group in sections.items():
    missing = [n for n in group if n not in carried]
    if missing:
        print(f"\n{name}: {len(group) - len(missing)} of {len(group)} carried, left out:")
        print("  " + ", ".join(missing))

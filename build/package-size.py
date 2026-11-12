#!/usr/bin/env python3
"""Says the size of each package of a version in build/package against the size nuget.org takes, and
fails where one is over, so a pack run stops before its push and not at it.

    build/package-size.py <version>

The engine is one package, carrying every area's assembly, the built-in shaders and their compiled
cache, so its size grows with each of them. A push nuget.org refuses would fail the run after every
game and capture of it passed, so the pack job measures first, before anything is kept or pushed,
and says each package's room in the run's summary, where a shrinking margin is seen before it runs
out.

nuget.org's documents give the size as 250 MB. The gallery itself refuses a push whose request is
over 262,144,000 bytes, ASP.NET's maxRequestLength of 256,000 KB in its Web.config, with IIS's
maxAllowedContentLength the same, and answers 413, the package file exceeding the size limit. The
request is the package in a form of one part, as dotnet nuget push sends it, a few hundred bytes of
framing, and the check leaves 4,096 bytes for it.

PACKAGE_SIZE_LIMIT gives the request's limit in bytes in place of the gallery's, for a test.
"""

import glob
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FOLDER = os.path.join(ROOT, "build", "package")
REQUEST_LIMIT = int(os.environ.get("PACKAGE_SIZE_LIMIT", "262144000"))
FRAMING = 4096
LIMIT = REQUEST_LIMIT - FRAMING


def say(line, summary):
    print(line, flush=True)
    path = os.environ.get("GITHUB_STEP_SUMMARY")
    if path:
        with open(path, "a", encoding="utf-8") as page:
            page.write(summary + "\n")


def main():
    if len(sys.argv) != 2:
        sys.exit("usage: build/package-size.py <version>")
    version = sys.argv[1]

    files = sorted(glob.glob(os.path.join(FOLDER, f"*.{version}.nupkg")))
    if not files:
        sys.exit(f"no package of {version} in {FOLDER}")

    say(f"{len(files)} packages of {version}, each against {LIMIT:,} bytes, nuget.org's {REQUEST_LIMIT:,} of a request less {FRAMING:,} for its framing",
        f"### The packages against nuget.org's size\n\nEach against {LIMIT:,} bytes, nuget.org's {REQUEST_LIMIT:,} of a request less "
        f"{FRAMING:,} for its framing.\n\n| Package | Bytes | Room |\n|---|---:|---:|")
    over = []
    for path in files:
        name = os.path.basename(path)
        size = os.path.getsize(path)
        room = LIMIT - size
        if room < 0:
            over.append(name)
        say(f"  {name} {size:,} bytes, {room:,} bytes of room ({room / LIMIT:.2%})" + (", over" if room < 0 else ""),
            f"| {name} | {size:,} | {room:,} ({room / LIMIT:.2%}) |")
    if over:
        sys.exit(f"over the size nuget.org takes, so nothing is pushed: {', '.join(over)}")


if __name__ == "__main__":
    main()

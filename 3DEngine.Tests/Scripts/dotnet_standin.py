#!/usr/bin/env python3
"""Stands for dotnet in the tests of build/test.py, so they need no dotnet and no suite.

Its tests are 40 under Rendering, 35 under Api and 5 of NormTests. Asked for them with
--list-tests it lists them. Asked to run them with no filter, as the suite whole, it does what
E3D_STANDIN says: hang, grow until it is ended, or die with exit code 134, leaving a minidump where
the runtime is asked to. With a filter, as a
part, it passes the tests the filter takes and writes their results file.
"""

import os
import sys
import time

TESTS = ([f"Engine.Tests.Rendering.Drawing.Draws_{i}" for i in range(40)]
         + [f"Engine.Tests.Api.Calls.Answers_{i}" for i in range(35)]
         + [f"Engine.Tests.NormTests.N_1_{i}" for i in range(5)])


def value(flag):
    args = sys.argv[1:]
    return args[args.index(flag) + 1] if flag in args else None


def taken(name, filter_):
    for clause in filter_.split("&"):
        if clause.startswith("FullyQualifiedName!~") and clause[len("FullyQualifiedName!~"):] in name:
            return False
        if clause.startswith("FullyQualifiedName~") and clause[len("FullyQualifiedName~"):] not in name:
            return False
    return True


def main():
    if "--list-tests" in sys.argv:
        print("The following Tests are available:")
        for name in TESTS:
            print("    " + name)
        return 0

    filter_ = value("--filter")
    if filter_ is None:
        mode = os.environ.get("E3D_STANDIN", "pass")
        print("  Passed Engine.Tests.Api.Calls.Answers_0 [1 ms]", flush=True)
        if mode == "hang":
            time.sleep(600)
        elif mode == "grow":
            # 200 MB a second, up to a gigabyte, so a system where the script cannot read it
            # takes no more before the time limit ends it.
            held = []
            while True:
                if len(held) < 100:
                    held.append(b"x" * (10 * 1024 * 1024))
                time.sleep(0.05)
        elif mode == "die":
            # How far a test had got, and the minidump the runtime leaves where it is asked to.
            print("[leak test] app 37 of 100", flush=True)
            dump = os.environ.get("DOTNET_DbgMiniDumpName")
            if dump:
                with open(dump.replace("%e", "testhost").replace("%p", "4242"), "wb") as f:
                    f.write(b"MDMP")
            print("the last words of a process about to die", flush=True)
            os._exit(134)

    names = [name for name in TESTS if filter_ is None or taken(name, filter_)]
    results = value("--results-directory")
    logger = next(arg for arg in sys.argv if arg.startswith("trx;LogFileName="))
    path = os.path.join(results, logger.split("=", 1)[1])
    rows = "".join(f'<UnitTestResult testName="{name}" outcome="Passed" />' for name in names)
    with open(path, "w", encoding="utf-8") as f:
        f.write('<?xml version="1.0" encoding="utf-8"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                f"<Results>{rows}</Results></TestRun>")
    for name in names:
        print(f"  Passed {name} [1 ms]")
    return 0


if __name__ == "__main__":
    sys.exit(main())

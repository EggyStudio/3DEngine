#!/usr/bin/env python3
"""Stands for dotnet in the tests of build/test.py, so they need no dotnet and no suite.

Its tests are 40 under Rendering, 35 under Api and 5 of NormTests. Asked for them with
--list-tests it lists them. Asked to run them with no filter, as the suite whole, it does what
E3D_STANDIN says: hang, grow until it is ended, or die with exit code 134, leaving a minidump where
the runtime is asked to and saying, as createdump does, the thread it crashed on and its signal, or
die-unsaid, the same without that line, or most, ending every test of Api and NormTests, one of
those failing with its message, and ten of Rendering, each longer than the one before, and then
hanging. With a filter, as a part, it passes the tests the filter takes and writes their results
file. Told shuffle-fails, the suite whole passes, unless E3D_SCHEDULE_SHUFFLE_SEED is set, when one
test of NormTests fails, as a test leaning on the order of an app's systems does in another order.
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
        if mode == "most":
            for name in TESTS[40:]:
                if name.endswith("N_1_3"):
                    print(f"  Failed {name} [3 ms]\n  Error Message:\n   Expected the rule to hold, but it did not.\n"
                          "  Stack Trace:\n     at Engine.Tests.NormTests.N_1_3() in /src/NormTests.cs:line 30", flush=True)
                else:
                    print(f"  Passed {name} [2 ms]", flush=True)
            for i in range(10):
                seconds = i * 7
                print(f"  Passed {TESTS[i]} [{f'{seconds // 60} m {seconds % 60} s' if seconds >= 60 else f'{seconds} s'}]", flush=True)
            time.sleep(600)
        print("  Passed Engine.Tests.Api.Calls.Answers_0 [1 ms]", flush=True)
        if mode == "hang":
            time.sleep(600)
        elif mode == "grow":
            # 200 MB a second, up to a gigabyte, so a system where the script cannot read it
            # takes no more before the time limit ends it. Each step copies random bytes, since
            # macOS can compress a page of one byte repeated when memory is short, and a compressed
            # page is left out of the resident size the script reads.
            held = []
            block = os.urandom(10 * 1024 * 1024)
            while True:
                if len(held) < 100:
                    held.append(bytearray(block))
                time.sleep(0.05)
        elif mode in ("die", "die-unsaid"):
            # How far a test had got, and the minidump the runtime leaves where it is asked to.
            print("[leak test] app 37 of 100", flush=True)
            if mode == "die":
                print("[createdump] Gathering state for process 4242 testhost", flush=True)
                print("[createdump] Crashing thread 1a2e signal 11 (000b)", flush=True)
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
    shuffled = os.environ.get("E3D_STANDIN") == "shuffle-fails" and os.environ.get("E3D_SCHEDULE_SHUFFLE_SEED")
    rows = "".join(
        f'<UnitTestResult testName="{name}" outcome="Failed"><Output><ErrorInfo><Message>Expected the order to hold, '
        f'but seed {shuffled} broke it.</Message><StackTrace>   at Engine.Tests.NormTests.N_1_3()</StackTrace></ErrorInfo></Output></UnitTestResult>'
        if shuffled and name.endswith("N_1_3") else f'<UnitTestResult testName="{name}" outcome="Passed" />'
        for name in names)
    with open(path, "w", encoding="utf-8") as f:
        f.write('<?xml version="1.0" encoding="utf-8"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                f"<Results>{rows}</Results></TestRun>")
    for name in names:
        print(f"  Passed {name} [1 ms]")
    return 0


if __name__ == "__main__":
    sys.exit(main())

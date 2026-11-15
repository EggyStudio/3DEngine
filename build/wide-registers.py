#!/usr/bin/env python3
"""Reads the compiled code of the model passes' record loops for a write of a wide register that
reaches a call of the device's with nothing clearing the registers' upper halves between.

    python3 build/wide-registers.py

The compiler clears the upper halves (vzeroupper) only where a method that wrote a 256-bit
register returns, and the device's wrappers write none before they call the driver, so a loop that
copies a struct of 32 bytes or more through a wide register and then records a command hands the
driver's code, which uses the narrow registers, a state that slows every instruction it runs. The
loops pass their views by reference and make their copies in calls of their own, and this reads
that they still do, the scene's draws, the window's depth, the shadows' batches and the subsurface
pass, built as a game's release build is. MeshEntityDrawsTests's test of their names compiles them
and every other method of the renderer's and its view memo's with tiered compilation off, and each
loop is read from its start, a call of a method whose every return clears the upper halves
clearing the state. A debug build's code, which copies every struct through the stack, would read
the same loops dirty. The code read is the one the machine's own extensions give, a machine of
AVX-512 copying through its widest registers, read alike. A machine that is not x86-64 has no such
registers, and the read passes there.
"""
import os
import platform
import re
import subprocess
import sys
import tempfile

LOOPS = ["Draw(Engine.TrackedRenderPass", "DrawDepth(", "DrawShadowBatches(", "DrawSubsurface("]
DEVICE = re.compile(r"Engine\.(I?GraphicsDevice|TrackedRenderPass):|Vortice\.Vulkan")
WRITES = re.compile(r"^\S+\s+[yz]mm\d+\b")


def listings(text):
    named = {}
    for part in re.split(r"^; Assembly listing for method ", text, flags=re.M)[1:]:
        head = part.split("\n", 1)[0]
        named[re.sub(r"\s*\((FullOpts|Tier\d|MinOpts|OSR|Instrumented[^)]*)\).*$", "", head).strip()] = part
    return named


def callee(code):
    """The name a call names, as `call [r11]Name` or `call [Name]` write it."""
    named = re.sub(r"^\[r\w+\]", "", code[len("call"):].strip())
    return named[1:-1] if named.startswith("[") and named.endswith("]") else named


def blocks(part):
    """The method's groups of code in order, each its label and its instructions."""
    found, label, codes = [], "entry", []
    for line in part.split("\n"):
        named = re.match(r"^(G_M\d+_IG\d+):", line)
        if named:
            found.append((label, codes))
            label, codes = named.group(1), []
            continue
        code = line.strip()
        if code and not code.startswith(";"):
            codes.append(code)
    found.append((label, codes))
    return found


def clears(part):
    """Whether the method returns with the upper halves clear, each group of its code that returns
    clearing them before the return."""
    returns = [codes for _, codes in blocks(part) if any(c.startswith("ret") for c in codes)]
    return bool(returns) and all(
        any(c.startswith("vzeroupper") for c in codes[:[c.startswith("ret") for c in codes].index(True)])
        for codes in returns)


def dirty_calls(part, cleans):
    """Each call of the device's the method reaches on some path with a wide register written since
    its start, the last vzeroupper or the last call of a method that returns clearing them, the
    writes that reach it carried along every jump and fall from group to group until none changes."""
    groups = blocks(part)
    index = {label: n for n, (label, _) in enumerate(groups)}
    following = []
    for n, (label, codes) in enumerate(groups):
        targets = [index[t] for c in codes if c.startswith("j")
                   for t in re.findall(r"\b(G_M\d+_IG\d+)\b", c) if t in index]
        last = codes[-1] if codes else ""
        ends = last.startswith(("ret", "jmp", "int3"))
        following.append(targets + ([n + 1] if not ends and n + 1 < len(groups) else []))
    entering = [frozenset() for _ in groups]
    found = {}
    changed, rounds = True, 0
    while changed and rounds < 10000:
        changed, rounds = False, rounds + 1
        for n, (label, codes) in enumerate(groups):
            written = entering[n]
            for code in codes:
                if code.startswith("vzeroupper"):
                    written = frozenset()
                elif WRITES.match(code):
                    written = written | {(label, code[:60])}
                elif code.startswith("call"):
                    if DEVICE.search(code):
                        for where in written:
                            found.setdefault(f'{where[0]} "{where[1]}" reaches {label} {code[:100]}', None)
                    elif cleans.get(callee(code)):
                        written = frozenset()
            for after in following[n]:
                joined = entering[after] | written
                if joined != entering[after]:
                    entering[after], changed = joined, True
    return list(found)


def main():
    if platform.machine().lower() not in ("x86_64", "amd64"):
        print(f"the record loops have no wide registers to read on {platform.machine()}")
        return 0
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    built = subprocess.run(["dotnet", "build", "3DEngine.Tests", "-c", "Release", "-v", "q", "--nologo"], cwd=root,
                           capture_output=True, text=True)
    if built.returncode != 0:
        print(built.stdout[-3000:] + built.stderr[-2000:])
        print("the release build of the tests failed")
        return 1
    with tempfile.TemporaryDirectory() as folder:
        out = os.path.join(folder, "listing.txt")
        env = dict(os.environ, DOTNET_TieredCompilation="0", DOTNET_JitStdOutFile=out,
                   DOTNET_JitDisasm="Engine.ModelRenderer:* Engine.ModelRenderer+Frustum:* Engine.ViewMemo:*")
        run = subprocess.run(["dotnet", "test", "3DEngine.Tests", "-c", "Release", "--no-build", "--filter",
                              "FullyQualifiedName~The_Record_Loops_The_Wide_Registers_Read_Are_Compiled_With_The_Renderers_Own_Calls"],
                             cwd=root, env=env, capture_output=True, text=True)
        if run.returncode != 0:
            print(run.stdout[-2000:] + run.stderr[-2000:])
            print("the test that compiles the record loops failed")
            return 1
        text = open(out, errors="replace").read() if os.path.exists(out) else ""
    named = listings(text)
    cleans = {name: clears(part) for name, part in named.items()}
    failed = False
    for loop in LOOPS:
        parts = [part for name, part in named.items() if name.startswith("Engine.ModelRenderer:" + loop)]
        if not parts:
            print(f"ModelRenderer.{loop.rstrip('(')} was not compiled")
            failed = True
            continue
        for part in parts:
            calls = dirty_calls(part, cleans)
            if calls:
                failed = True
                print(f"ModelRenderer.{loop.rstrip('(')} calls the device with the wide registers' upper halves in use:")
                for call in calls[:8]:
                    print("  " + call)
    if not failed:
        print(f"the {len(LOOPS)} record loops call the device with the wide registers' upper halves clear")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())

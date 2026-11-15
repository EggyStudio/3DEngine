#!/usr/bin/env python3
"""Reads the compiled code of the model passes' record loops for a write of a wide register that
reaches a call of the device's with nothing clearing the registers' upper halves between.

    python3 build/wide-registers.py

The compiler clears the upper halves (vzeroupper) only where a method that wrote a 256-bit
register returns, and the device's wrappers write none before they call the driver, so a loop that
copies or clears a struct of 32 bytes or more through a wide register and then records a command
hands the driver's code, which uses the narrow registers, a state that slows every instruction it
runs. The loops pass their views and draws by reference and make their copies in calls of their
own, and this reads that they still do, the scene's draws, the window's depth, the shadows' batches
and the subsurface pass, built as a game's release build is. MeshEntityDrawsTests's test of their
names compiles them and every other method of the renderer's and its view memo's with tiered
compilation off, and each loop is read from its start, a call of a method whose every return clears
the upper halves clearing the state. A debug build's code, which copies every struct through the
stack, would read the same loops dirty.

The compiler at run time takes the instruction sets of the machine it runs on, and its settings
can leave a set out and not add one the machine lacks, so the same loops are compiled ahead too by
crossgen2 of the runtime's own version for a machine of AVX2 and one of AVX-512, x86-64-v3 and
x86-64-v4, the assemblies one bubble so it inlines across them as the run's compiler does, and read
alike. A machine of AVX-512 clears a struct of 64 bytes or more with a 256-bit xor before its 512-bit
stores, where one of AVX2 clears it through the narrow registers. crossgen2 is taken from the NuGet
packages' folder, and where it is not there it is restored into it from nuget.org, which needs the
network and the folder writable, so a runner with neither fails the read for that alone and says
so. Every listing read is kept in build/artifacts/wide-registers. A machine that is not x86-64 has no such registers, and the read
passes there.
"""
import os
import platform
import re
import subprocess
import sys
import tempfile

KEPT = os.path.join("build", "artifacts", "wide-registers")
NAMES = "Engine.ModelRenderer:* Engine.ModelRenderer+Frustum:* Engine.ViewMemo:*"
SETS = ["x86-64-v3", "x86-64-v4"]
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


def read(text, where):
    """Whether a listing has each loop and reads it clean, saying of each loop that does not what
    reaches which call."""
    named = listings(text)
    cleans = {name: clears(part) for name, part in named.items()}
    failed = False
    for loop in LOOPS:
        parts = [part for name, part in named.items() if name.startswith("Engine.ModelRenderer:" + loop)]
        if not parts:
            print(f"{where}: ModelRenderer.{loop.rstrip('(')} was not compiled")
            failed = True
            continue
        for part in parts:
            calls = dirty_calls(part, cleans)
            if calls:
                failed = True
                print(f"{where}: ModelRenderer.{loop.rstrip('(')} calls the device with the wide registers' upper halves in use:")
                for call in calls[:8]:
                    print("  " + call)
    return not failed


def runtime(major):
    """The version and folder of the newest runtime of the major version the tests target, the one
    they run on."""
    listed = subprocess.run(["dotnet", "--list-runtimes"], capture_output=True, text=True).stdout
    found = [(tuple(int(n) for n in version.split(".")), version, os.path.join(path, version))
             for version, path in re.findall(r"^Microsoft\.NETCore\.App (\d+\.\d+\.\d+) \[(.+)\]$", listed, re.M)
             if version.split(".")[0] == major]
    return max(found)[1:] if found else (None, None)


def crossgen(version, major):
    """The crossgen2 of the runtime's version from the NuGet packages' folder, restored there first
    where it is not, and what the restore said where it gave none."""
    rid = {"linux": "linux-x64", "darwin": "osx-x64", "win32": "win-x64"}[sys.platform]
    packages = os.environ.get("NUGET_PACKAGES") or os.path.join(os.path.expanduser("~"), ".nuget", "packages")
    tool = os.path.join(packages, f"microsoft.netcore.app.crossgen2.{rid}", version, "tools",
                        "crossgen2.exe" if sys.platform == "win32" else "crossgen2")
    if not os.path.exists(tool):
        with tempfile.TemporaryDirectory() as folder:
            with open(os.path.join(folder, "crossgen2.csproj"), "w") as project:
                project.write(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net{major}.0</TargetFramework>'
                              f'</PropertyGroup><ItemGroup><PackageDownload Include="Microsoft.NETCore.App.Crossgen2.{rid}" '
                              f'Version="[{version}]" /></ItemGroup></Project>')
            restored = subprocess.run(["dotnet", "restore", folder, "-v", "q", "--nologo"], capture_output=True, text=True)
        if not os.path.exists(tool):
            return None, (f"restoring Microsoft.NETCore.App.Crossgen2.{rid} {version} into {packages} needs nuget.org "
                          f"reachable and the folder writable, and the restore said {(restored.stdout + restored.stderr).strip()[-1500:]}")
    if not os.access(tool, os.X_OK):
        os.chmod(tool, 0o755)
    return tool, None


def ahead(tool, built, framework, wanted, kept):
    """The renderer compiled ahead for an instruction set with the compiler's listing on, or why not,
    on one thread so no two methods' listings are written into each other."""
    with tempfile.TemporaryDirectory() as folder:
        made = subprocess.run([tool, os.path.join(built, "3DEngine.Rendering.dll"), "-o", os.path.join(folder, "out.dll"),
                               "-r", os.path.join(built, "*.dll"), "-r", os.path.join(framework, "*.dll"), "-O",
                               "--targetarch", "x64", "--instruction-set", wanted, "--inputbubble", "--parallelism", "1",
                               "--inputbubbleref", os.path.join(built, "*.dll"), "--inputbubbleref", os.path.join(framework, "*.dll"),
                               "--codegenopt", f"JitDisasm={NAMES}", "--codegenopt", f"JitStdOutFile={os.path.abspath(kept)}"],
                              capture_output=True, text=True)
    if made.returncode != 0 or not os.path.exists(kept):
        return None, (made.stdout[-1500:] + made.stderr[-1500:]).strip()
    return open(kept, errors="replace").read(), None


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
    kept = os.path.join(root, KEPT)
    os.makedirs(kept, exist_ok=True)
    out = os.path.join(kept, "run.txt")
    if os.path.exists(out):
        os.remove(out)
    env = dict(os.environ, DOTNET_TieredCompilation="0", DOTNET_JitStdOutFile=out, DOTNET_JitDisasm=NAMES)
    run = subprocess.run(["dotnet", "test", "3DEngine.Tests", "-c", "Release", "--no-build", "--filter",
                          "FullyQualifiedName~The_Record_Loops_The_Wide_Registers_Read_Are_Compiled_With_The_Renderers_Own_Calls"],
                         cwd=root, env=env, capture_output=True, text=True)
    if run.returncode != 0:
        print(run.stdout[-2000:] + run.stderr[-2000:])
        print("the test that compiles the record loops failed")
        return 1
    clean = read(open(out, errors="replace").read() if os.path.exists(out) else "", "this machine")

    target = os.path.join(root, "3DEngine.Tests", "bin", "Release")
    folders = sorted(f for f in os.listdir(target) if re.fullmatch(r"net\d+\.\d+", f))
    major = folders[-1][3:].split(".")[0]
    version, framework = runtime(major)
    tool, why = crossgen(version, major) if version else (None, f"no runtime {major} is listed")
    if tool is None:
        print(f"no crossgen2 of the runtime {version or major} to compile the loops ahead with, {why}")
        return 1
    for wanted in SETS:
        text, why = ahead(tool, os.path.join(target, folders[-1]), framework, wanted, os.path.join(kept, f"{wanted}.txt"))
        if text is None:
            print(f"crossgen2 {version} did not compile the renderer for {wanted}: {why}")
            clean = False
            continue
        clean = read(text, f"{wanted} ahead") and clean
    if clean:
        print(f"the {len(LOOPS)} record loops call the device with the wide registers' upper halves clear, "
              f"on this machine and compiled ahead for {' and '.join(SETS)} by crossgen2 {version}")
    else:
        print(f"the listings read are in {KEPT}")
    return 0 if clean else 1


if __name__ == "__main__":
    sys.exit(main())

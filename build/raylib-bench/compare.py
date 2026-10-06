#!/usr/bin/env python3
"""Measures each written example against raylib's own program of the same name (NORM.md, N 5.2).

    build/raylib-bench/compare.py <group, example or all>... [--record <file>] [--against <file>]

raylib's example is built from the checkout build/examples-table.py reads, of the commit
build/raylib-bench/run.sh pins, against raylib built as run.sh builds it, with its SDL3 backend,
and with PLATFORM_DESKTOP defined so a shader example loads its GLSL 330 shaders. shim.c beside it
is linked in, so the program writes the frame it draws at the number this engine's capture of the
example was taken at, by build/capture-example.sh, and ends. Both programs count each frame a
sixtieth of a second and seed raylib's generator alike, this engine's by E3D_FRAME_TIME and E3D_SEED
and raylib's by the shim, so what moves by the frame's time or is placed at random draws the same
frame in each. This engine's capture is given none of the input capture-example.sh gives some
examples, as raylib's program is given none. Both are drawn with no window shown, raylib's through
SDL's offscreen video driver and OpenGL, this engine's offscreen through Vulkan.

A pair is compared as 3DEngine.Tests' reference frames are: a pixel is apart when one of its
channels differs by more than 24 of 255, and the share of pixels apart is written for the example
into 3DEngine.Examples/measured.tsv, which build/examples-table.py puts beside the example in
.github/EXAMPLES.md. The two pictures are kept under build/raylib-bench/work/compare to look at.

--record writes the shares into the file given in place of measured.tsv. --against holds each pair
to the share the file given recorded for it on the same machine before, and the run fails where a
pair stands more than one point above it, where it drew a frame and draws none, or where its
pictures came to differ in size. A pair with no share recorded is measured and recorded for the
first time, and one triage.tsv marks as moving, by the clock or the device, is left out, with its
reason. Where the file holds no share at all, as before its first run is recorded, what would fail
is said and the run does not fail. The build workflow's examples job runs every pair so, against
3DEngine.Examples/measured-ci.tsv, the shares its own device recorded, so two machines' drivers are
never compared with each other. Under GitHub Actions the pairs measured for the first time are
notices as well, at most ten, which a reader not signed in sees where the run's summary and its
files need a sign-in.
"""
import glob
import importlib.util
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HERE = os.path.join(ROOT, "build", "raylib-bench")
WORK = os.path.join(HERE, "work")
OUT = os.path.join(WORK, "compare")
MEASURED = os.path.join(ROOT, "3DEngine.Examples", "measured.tsv")
STEP = 24
# Each frame's seconds and the random seed both programs of a pair are given, so a program that moves
# by its frame time or places things at random draws the same frame in each.
FRAME_TIME = 1 / 60
SEED = 20261006

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(ROOT, "build"))
from page import ANNOTATIONS, annotate

_spec = importlib.util.spec_from_file_location("examples_table", os.path.join(ROOT, "build", "examples-table.py"))
table = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(table)


def sdl_library():
    """The folder of the SDL3 library the engine's package brings, which raylib is linked against."""
    found = sorted(glob.glob(os.path.expanduser("~/.nuget/packages/sdl3-cs.native/*/runtimes/linux-x64/native/libSDL3.so")))
    if not found:
        sys.exit("no libSDL3.so under ~/.nuget/packages/sdl3-cs.native; build the solution first")
    return os.path.dirname(found[-1])


def build_raylib(source):
    """raylib's static library, built as run.sh builds it when it is not built yet."""
    library = os.path.join(source, "src", "libraylib.a")
    if os.path.exists(library):
        return library
    sdl = os.path.join(WORK, "sdl")
    if not os.path.isdir(sdl):
        subprocess.run(["git", "clone", "-q", "--depth", "1", "--filter=blob:none", "--sparse", "--branch", "release-3.4.2",
                        "https://github.com/libsdl-org/SDL.git", sdl], check=True)
        subprocess.run(["git", "-C", sdl, "sparse-checkout", "set", "include"], check=True)
    subprocess.run(["make", "-C", os.path.join(source, "src"), f"-j{os.cpu_count()}", "PLATFORM=PLATFORM_DESKTOP_SDL",
                    f"SDL_INCLUDE_PATH={os.path.join(sdl, 'include')}", "CUSTOM_CFLAGS=-DUSING_SDL3_PROJECT -O2"],
                   check=True, stdout=subprocess.DEVNULL)
    return library


def build_example(source, library, example):
    """raylib's program of the example, with the shim linked in, or None where it does not build."""
    binary = os.path.join(OUT, "bin", example["name"])
    os.makedirs(os.path.dirname(binary), exist_ok=True)
    shim = os.path.join(HERE, "shim.c")
    if os.path.exists(binary) and os.path.getmtime(binary) > os.path.getmtime(shim):
        return binary
    sdl = sdl_library()
    folder = os.path.join(source, "examples", example["group"])
    built = subprocess.run(["gcc", "-O2", "-DPLATFORM_DESKTOP", os.path.join(source, example["path"]), os.path.join(HERE, "shim.c"),
                            "-I", os.path.join(source, "src"), "-I", folder, "-I", os.path.join(source, "src", "external"),
                            library, f"-L{sdl}", "-l:libSDL3.so", "-lm", "-ldl", "-lpthread", f"-Wl,-rpath,{sdl}",
                            "-Wl,--wrap=BeginDrawing,--wrap=EndDrawing,--wrap=GetFrameTime,--wrap=GetTime,--wrap=InitWindow,--wrap=UpdateCamera", "-o", binary],
                           capture_output=True, text=True)
    if built.returncode != 0:
        print(f"  {example['name']}: raylib's program does not build here: {built.stderr.strip().splitlines()[-1] if built.stderr.strip() else ''}")
        return None
    return binary


def capture_ours(name):
    """This engine's capture of the example, as the README's is taken, and the frame it was taken at."""
    picture = os.path.join(OUT, name + ".ours.png")
    frame_file = os.path.join(OUT, name + ".frame")
    # One sample a pixel unless the program asks for more, as raylib draws, and no input, as raylib's
    # program is given none.
    environment = dict(os.environ, CAPTURE_FRAME_FILE=frame_file, CAPTURE_NO_INPUT="1", E3D_SAMPLES="1",
                       E3D_FRAME_TIME=repr(FRAME_TIME), E3D_SEED=str(SEED))
    subprocess.run([os.path.join(ROOT, "build", "capture-example.sh"), name, picture, "--offscreen"], check=True,
                   env=environment, cwd=ROOT, stdout=subprocess.DEVNULL)
    with open(frame_file, encoding="utf-8") as text:
        return picture, int(text.read().strip())


def capture_theirs(binary, example, frame):
    """raylib's frame of the same number, or None where the program ended or hung before it."""
    picture = os.path.join(OUT, example["name"] + ".raylib.png")
    if os.path.exists(picture):
        os.remove(picture)
    environment = dict(os.environ, SDL_VIDEO_DRIVER="offscreen", SHOT_FRAME=str(frame), SHOT_PATH=picture,
                       SHOT_FRAME_TIME=repr(FRAME_TIME), SHOT_SEED=str(SEED))
    folder = os.path.join(table.raylib_source(table.raylib_commit(), None), "examples", example["group"])
    try:
        subprocess.run([binary], cwd=folder, env=environment, timeout=120, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except subprocess.TimeoutExpired:
        return None
    return picture if os.path.exists(picture) else None


def apart(ours, theirs):
    """The share of pixels whose channels differ by more than STEP, or None for pictures of two sizes."""
    from PIL import Image
    a = Image.open(ours).convert("RGB")
    b = Image.open(theirs).convert("RGB")
    if a.size != b.size:
        return None
    pa, pb = a.tobytes(), b.tobytes()
    differing = sum(1 for i in range(0, len(pa), 3)
                    if max(abs(pa[i] - pb[i]), abs(pa[i + 1] - pb[i + 1]), abs(pa[i + 2] - pb[i + 2])) > STEP)
    return differing / (a.size[0] * a.size[1])


def read_measured(path=MEASURED):
    measured = {}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as lines:
            for line in lines:
                if line.strip() and not line.startswith("#"):
                    name, share = line.rstrip("\n").split("\t")[:2]
                    measured[name] = share
    return measured


def write_measured(measured, path=MEASURED):
    with open(path, "w", encoding="utf-8") as out:
        out.write("# Each written example's share of pixels apart from raylib's own program at the same frame, written by\n"
                  "# build/raylib-bench/compare.py and read by build/examples-table.py. A name, then the share in percent, or a\n"
                  "# word where no share was measured: size (the two pictures are of two sizes) or none (raylib's program\n"
                  "# drew no such frame).\n")
        for name in sorted(measured):
            out.write(f"{name}\t{measured[name]}\n")


def option(name):
    """The value given after --name, or None."""
    arguments = sys.argv[1:]
    return arguments[arguments.index(name) + 1] if name in arguments and arguments.index(name) + 1 < len(arguments) else None


# How far, in points of the share, a pair may stand above the share recorded for it before a run held
# against the record fails, room for a driver's rounding that moves from run to run.
ROOM = 1.0


def held(name, share, recorded):
    """Why a pair's share fails the share recorded for it, or None where it holds."""
    if recorded is None:
        return None
    if share in ("none", "size") or recorded in ("none", "size"):
        return None if share == recorded else f"it was {recorded} and is {share}"
    return f"{share}% apart where {recorded}% was recorded" if float(share) > float(recorded) + ROOM else None


def notices(names, measured):
    """The pairs measured for the first time as notices, each name with its share, shared out among
    as few notices as hold them and no more than GitHub shows."""
    size = -(-len(names) // ANNOTATIONS)
    parts = [names[i:i + size] for i in range(0, len(names), size)] if names else []
    for index, part in enumerate(parts, 1):
        annotate("notice", f"Measured for the first time, {index} of {len(parts)}", [f"{name}\t{measured[name]}" for name in part])


def main():
    values = {option("--record"), option("--against")}
    wanted = [a for a in sys.argv[1:] if not a.startswith("--") and a not in values]
    if not wanted:
        sys.exit(__doc__)
    record, against = option("--record"), option("--against")
    commit = table.raylib_commit()
    source = table.raylib_source(commit, None)
    written = table.written_examples()
    examples = [e for e in table.read_examples(source) if e["name"] in written and ("all" in wanted or e["group"] in wanted or e["name"] in wanted)]
    if not examples:
        sys.exit("no written example in " + ", ".join(wanted))
    recorded = read_measured(against) if against else {}
    moving = {name: note for name, (state, note) in table.read_triage().items() if state == "moves"}
    if against:
        for example in [e for e in examples if e["name"] in moving]:
            print(f"  {example['name']}: left out, {moving[example['name']]}")
        examples = [e for e in examples if e["name"] not in moving]
    library = build_raylib(source)
    # The build ./e3d open starts, made from the checkout as it is, so no capture is of an older one.
    subprocess.run(["dotnet", "build", os.path.join(ROOT, "3DEngine.Examples"), "-v", "q", "--nologo"], check=True,
                   stdout=subprocess.DEVNULL)
    os.makedirs(OUT, exist_ok=True)
    measured = read_measured(record) if record else read_measured()
    failures, first = [], []
    for example in examples:
        name = example["name"]
        binary = build_example(source, library, example)
        if binary is None:
            if against and name in recorded:
                failures.append(f"{name}: raylib's program no longer builds")
            continue
        try:
            ours, frame = capture_ours(name)
        except (subprocess.CalledProcessError, OSError, ValueError) as error:
            print(f"  {name}: this engine's capture failed: {error}")
            if against:
                failures.append(f"{name}: this engine's capture failed")
            continue
        theirs = capture_theirs(binary, example, frame)
        if theirs is None:
            measured[name] = "none"
            print(f"  {name}: raylib's program drew no frame {frame}")
        else:
            share = apart(ours, theirs)
            measured[name] = "size" if share is None else f"{100 * share:.1f}"
            print(f"  {name}: frame {frame}, " + ("the pictures are of two sizes" if share is None else f"{100 * share:.1f}% apart"))
        if against:
            if name not in recorded:
                first.append(name)
            elif (why := held(name, measured[name], recorded[name])) is not None:
                failures.append(f"{name}: {why}")
        write_measured(measured, record or MEASURED)
    write_measured(measured, record or MEASURED)

    if first:
        print(f"{len(first)} pair(s) measured for the first time, recorded in {record or MEASURED} for {against}:")
        for name in first:
            print(f"{name}\t{measured[name]}")
        notices(first, measured)
    if failures:
        print(f"{len(failures)} pair(s) do not hold to the share recorded for them, more than {ROOM:g} point above it or no longer drawn:")
        for failure in failures:
            print(f"  {failure}")
        if against and not recorded:
            print(f"{against} holds no share yet, so the run does not fail until it is recorded.")
            return
        sys.exit(1)


if __name__ == "__main__":
    main()

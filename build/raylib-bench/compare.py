#!/usr/bin/env python3
"""Measures each written example against raylib's own program of the same name (NORM.md, N 5.2).

    build/raylib-bench/compare.py <group, example or all>... [--record <file>] [--against <file>] [--pictures <folder>]

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
pair stands more than one point above it, or the room a third column of the file gives it, where it
drew a frame and draws none, or where its pictures came to differ in size. A run in which raylib's programs drew no frame for any pair, as
where they cannot start, records nothing and fails, saying why the first drew none. A pair with no share recorded is measured and recorded for the
first time, and one triage.tsv marks as moving, by the clock or the device, is left out, with its
reason. Where the file holds no share at all, as before its first run is recorded, what would fail
is said and the run does not fail. The captures job runs every pair so in the pack run, against
3DEngine.Examples/measured-ci.tsv, the shares its own device recorded, so two machines' drivers are
never compared with each other. Under GitHub Actions the pairs measured for the first time are
notices as well, at most nine, which a reader not signed in sees where the run's summary and its
files need a sign-in, and the pairs that fail a run are errors and lines of the summary, each with
the share measured, the share recorded and its room, or what it drew in place of a frame. Printed
alone, they left the run's page with no word of which pairs failed (REVIEW.md, Verdict 66). A
notice on every run, the tenth GitHub shows, and the summary say the processor the run measured on
and whether gfni is among its flags, the summary with how many pairs were measured and how many
failed, since both programs draw with code LLVM compiles for that processor and the runners are of
two kinds, and a pair that held before each kind compiled its own failed on the two runs since and
held on the next (REVIEW.md, Verdict 67). --pictures copies both pictures of every pair into the
folder given, which the captures job keeps with its captures, so a pair's frames are compared
between two runs.
"""
import importlib.util
import os
import re
import shutil
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
from page import ANNOTATION_LENGTH, ANNOTATIONS, annotate, escape, fit, summarize

_spec = importlib.util.spec_from_file_location("examples_table", os.path.join(ROOT, "build", "examples-table.py"))
table = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(table)


def sdl_library():
    """A folder holding the SDL3 library of the version the engine references, which raylib is linked
    against, under its own name and the name a program built against it loads it by, libSDL3.so.0.
    The package brings the first alone, so a program linked to the package's folder loads whatever
    SDL3 the system has, and on a machine with none, as the workflow's runner is, fails to start."""
    with open(os.path.join(ROOT, "3DEngine", "3DEngine.csproj"), encoding="utf-8") as project:
        version = re.search(r'Include="SDL3-CS\.Native" Version="([^"]+)"', project.read()).group(1)
    library = os.path.expanduser(f"~/.nuget/packages/sdl3-cs.native/{version.lower()}/runtimes/linux-x64/native/libSDL3.so")
    if not os.path.exists(library):
        sys.exit(f"no {library}; build the solution first")
    folder = os.path.join(WORK, "sdl-library")
    os.makedirs(folder, exist_ok=True)
    for name in ("libSDL3.so", "libSDL3.so.0"):
        link = os.path.join(folder, name)
        if os.path.islink(link) and os.readlink(link) == library:
            continue
        if os.path.lexists(link):
            os.remove(link)
        os.symlink(library, link)
    return folder


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
    # Built again when the shim or the way this script builds it has changed since.
    if os.path.exists(binary) and os.path.getmtime(binary) > max(os.path.getmtime(shim), os.path.getmtime(__file__)):
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
    """raylib's frame of the same number, or None where the program ended or hung before it, and
    why it drew none: its exit code and its last lines."""
    picture = os.path.join(OUT, example["name"] + ".raylib.png")
    if os.path.exists(picture):
        os.remove(picture)
    environment = dict(os.environ, SDL_VIDEO_DRIVER="offscreen", SHOT_FRAME=str(frame), SHOT_PATH=picture,
                       SHOT_FRAME_TIME=repr(FRAME_TIME), SHOT_SEED=str(SEED))
    folder = os.path.join(table.raylib_source(table.raylib_commit(), None), "examples", example["group"])
    try:
        ran = subprocess.run([binary], cwd=folder, env=environment, timeout=120, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                             text=True, errors="replace")
    except subprocess.TimeoutExpired:
        return None, ["it ran past 120 seconds without its frame"]
    if os.path.exists(picture):
        return picture, []
    # A program that ended without its frame says why in its last lines, which a library it could
    # not load, or a window it could not open, is.
    ended = f"raylib's own program ended with signal {-ran.returncode}" if ran.returncode < 0 else f"raylib's own program ended with {ran.returncode}"
    return None, [ended] + ran.stdout.strip().splitlines()[-LAST_LINES:]


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
                    name, share, *rest = line.rstrip("\n").split("\t")
                    measured[name] = share
                    if rest and rest[0]:
                        ROOMS[name] = float(rest[0])
    return measured


def write_measured(measured, path=MEASURED):
    with open(path, "w", encoding="utf-8") as out:
        out.write("# Each written example's share of pixels apart from raylib's own program at the same frame, written by\n"
                  "# build/raylib-bench/compare.py and read by build/examples-table.py. A name, then the share in percent, or a\n"
                  "# word where no share was measured: size (the two pictures are of two sizes) or none (raylib's program\n"
                  "# drew no such frame).\n")
        for name in sorted(measured):
            out.write(f"{name}\t{measured[name]}\n")


def processor():
    """The processor this runs on and whether gfni is among its flags, as build/open-game.sh says it
    on a game step's page."""
    try:
        described = subprocess.run(["lscpu"], capture_output=True, text=True, env=dict(os.environ, LC_ALL="C")).stdout
    except OSError:
        return "a processor lscpu could not name"
    lines = described.splitlines()
    model = next((line.split(":", 1)[1].strip() for line in lines if line.startswith("Model name:")), "an unnamed processor")
    flags = next((line.split(":", 1)[1].split() for line in lines if line.startswith("Flags:")), [])
    return f"{model}, gfni {'among' if 'gfni' in flags else 'not among'} its flags"


def option(name):
    """The value given after --name, or None."""
    arguments = sys.argv[1:]
    return arguments[arguments.index(name) + 1] if name in arguments and arguments.index(name) + 1 < len(arguments) else None


# How many of the last lines of raylib's program are kept to say why it drew no frame.
LAST_LINES = 4

# How far, in points of the share, a pair may stand above the share recorded for it before a run held
# against the record fails, room for a driver's rounding that moves from run to run. A pair whose
# picture moves further from run to run has its own room in a third column of the record, with the
# reason in a comment above it.
ROOM = 1.0
ROOMS = {}


def described(share):
    """A share as a reader reads it, a word written in place of a share said in words."""
    return {"none": "no frame from raylib's program", "size": "pictures of two sizes"}.get(share, f"{share}% apart")


def held(name, share, recorded):
    """Why a pair's share fails the share recorded for it, or None where it holds."""
    if recorded is None or share == recorded:
        return None
    if share in ("none", "size") or recorded in ("none", "size"):
        return f"{described(share)} where {described(recorded)} was recorded"
    room = ROOMS.get(name, ROOM)
    if float(share) <= float(recorded) + room:
        return None
    return f"{share}% apart where {recorded}% was recorded, past its room of {room:g} point{'' if room == 1 else 's'}"


def nothing_drawn(blind, count):
    """What fails a run in which raylib's programs drew no frame for any of its count pairs, given the
    first pair's name and why it drew none."""
    name, why = blind
    return ([f"raylib's program drew no frame for any of the {count} pairs, so the measure saw nothing and records nothing.",
             f"The first, {name}, drew none because:"] + [f"  {line}" for line in why])


def notices(names, measured):
    """The pairs measured for the first time as notices, each name with its share, shared out among
    as few notices as hold them and no more than GitHub shows beside the processor's."""
    size = -(-len(names) // (ANNOTATIONS - 1))
    parts = [names[i:i + size] for i in range(0, len(names), size)] if names else []
    for index, part in enumerate(parts, 1):
        annotate("notice", f"Measured for the first time, {index} of {len(parts)}", [f"{name}\t{measured[name]}" for name in part])


def errors(failures, where):
    """The pairs that fail the shares recorded for them as errors under GitHub Actions and lines of the
    job's summary, a pair a line, shared out among as few errors as hold them within the length GitHub
    keeps and no more than it shows, the last saying how many lines are left out where they do not
    fit."""
    title = f"{len(failures)} pair(s) do not hold to the share recorded for them"
    lines = fit([f"on {where}, more than {ROOM:g} point above it, or past the room the record gives, or no longer drawn:"] + failures)
    parts, part = [], []
    for line in lines:
        if part and len(escape("\n".join(part + [line]))) > ANNOTATION_LENGTH - 200:
            parts.append(part)
            part = []
        part.append(line)
    if part:
        parts.append(part)
    if len(parts) > ANNOTATIONS:
        left = sum(len(p) for p in parts[ANNOTATIONS - 1:])
        parts = parts[:ANNOTATIONS - 1] + [[f"{left} lines more, which the step's log holds"]]
    for index, part in enumerate(parts, 1):
        annotate("error", title if len(parts) == 1 else f"{title}, {index} of {len(parts)}", part)
    summarize([f"### {title}", ""] + [lines[0] + "  "] + ["    " + line for line in lines[1:]])


def main():
    values = {option("--record"), option("--against"), option("--pictures")}
    wanted = [a for a in sys.argv[1:] if not a.startswith("--") and a not in values]
    if not wanted:
        sys.exit(__doc__)
    record, against, pictures = option("--record"), option("--against"), option("--pictures")
    where = processor()
    print(f"Measured on {where}.")
    # A notice on every run, so the page of a run that holds says which processor its pairs held on,
    # as a failing run's errors say where they failed.
    annotate("notice", "The processor the measure ran on", [f"Measured on {where}."])
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
    if pictures:
        os.makedirs(pictures, exist_ok=True)
    # Whether any of raylib's programs drew its frame, and why the first that drew none did not. A
    # run where none drew is a measure that sees nothing, as on a machine where raylib's programs
    # cannot start, so it records nothing and fails.
    drew, blind = False, None
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
        theirs, why = capture_theirs(binary, example, frame)
        if pictures:
            for picture in (ours, theirs):
                if picture is not None:
                    shutil.copy(picture, pictures)
        if theirs is None:
            measured[name] = "none"
            print(f"  {name}: raylib's program drew no frame {frame}, " + "; ".join(why[:2]))
            blind = blind or (name, why)
        else:
            drew = True
            share = apart(ours, theirs)
            measured[name] = "size" if share is None else f"{100 * share:.1f}"
            print(f"  {name}: frame {frame}, " + ("the pictures are of two sizes" if share is None else f"{100 * share:.1f}% apart"))
        if against:
            if name not in recorded:
                first.append(name)
            elif (broken := held(name, measured[name], recorded[name])) is not None:
                # Where raylib's program drew none, why, which its exit code says first.
                failures.append(f"{name}: {broken}" + (f", {why[0]}" if theirs is None and why else ""))
        if drew:
            write_measured(measured, record or MEASURED)
    if not drew and blind is not None:
        lines = nothing_drawn(blind, len(examples))
        print("\n".join(lines))
        annotate("error", "No frame from raylib's programs", lines)
        sys.exit(1)
    write_measured(measured, record or MEASURED)
    summarize([f"### The measure against raylib, on {where}", "",
               f"{len(examples)} pairs, {len(failures)} not holding to the share recorded for them, {len(first)} measured for the first time"])

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
        errors(failures, where)
        sys.exit(1)


if __name__ == "__main__":
    main()

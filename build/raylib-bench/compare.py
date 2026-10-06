#!/usr/bin/env python3
"""Measures each written example against raylib's own program of the same name (NORM.md, N 5.2).

    build/raylib-bench/compare.py <group or example>...

raylib's example is built from the checkout build/examples-table.py reads, of the commit
build/raylib-bench/run.sh pins, against raylib built as run.sh builds it, with its SDL3 backend,
and with PLATFORM_DESKTOP defined so a shader example loads its GLSL 330 shaders. shim.c beside it is
linked around EndDrawing, so the program writes the frame it draws at the number this engine's
capture of the example was taken at, by build/capture-example.sh, and ends. Both are drawn with no
window shown, raylib's through SDL's offscreen video driver and OpenGL, this engine's offscreen
through Vulkan.

A pair is compared as 3DEngine.Tests' reference frames are: a pixel is apart when one of its
channels differs by more than 24 of 255, and the share of pixels apart is written for the example
into 3DEngine.Examples/measured.tsv, which build/examples-table.py puts beside the example in
.github/EXAMPLES.md. The two pictures are kept under build/raylib-bench/work/compare to look at.
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

sys.dont_write_bytecode = True
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
    """raylib's program of the example, with the shim around EndDrawing, or None where it does not build."""
    binary = os.path.join(OUT, "bin", example["name"])
    os.makedirs(os.path.dirname(binary), exist_ok=True)
    if os.path.exists(binary):
        return binary
    sdl = sdl_library()
    folder = os.path.join(source, "examples", example["group"])
    built = subprocess.run(["gcc", "-O2", "-DPLATFORM_DESKTOP", os.path.join(source, example["path"]), os.path.join(HERE, "shim.c"),
                            "-I", os.path.join(source, "src"), "-I", folder, "-I", os.path.join(source, "src", "external"),
                            library, f"-L{sdl}", "-l:libSDL3.so", "-lm", "-ldl", "-lpthread", f"-Wl,-rpath,{sdl}",
                            "-Wl,--wrap=EndDrawing", "-o", binary], capture_output=True, text=True)
    if built.returncode != 0:
        print(f"  {example['name']}: raylib's program does not build here: {built.stderr.strip().splitlines()[-1] if built.stderr.strip() else ''}")
        return None
    return binary


def capture_ours(name):
    """This engine's capture of the example, as the README's is taken, and the frame it was taken at."""
    picture = os.path.join(OUT, name + ".ours.png")
    frame_file = os.path.join(OUT, name + ".frame")
    # One sample a pixel unless the program asks for more, as raylib draws.
    environment = dict(os.environ, CAPTURE_FRAME_FILE=frame_file, E3D_SAMPLES="1")
    subprocess.run([os.path.join(ROOT, "build", "capture-example.sh"), name, picture, "--offscreen"], check=True,
                   env=environment, cwd=ROOT, stdout=subprocess.DEVNULL)
    with open(frame_file, encoding="utf-8") as text:
        return picture, int(text.read().strip())


def capture_theirs(binary, example, frame):
    """raylib's frame of the same number, or None where the program ended or hung before it."""
    picture = os.path.join(OUT, example["name"] + ".raylib.png")
    if os.path.exists(picture):
        os.remove(picture)
    environment = dict(os.environ, SDL_VIDEO_DRIVER="offscreen", SHOT_FRAME=str(frame), SHOT_PATH=picture)
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


def read_measured():
    measured = {}
    if os.path.exists(MEASURED):
        with open(MEASURED, encoding="utf-8") as lines:
            for line in lines:
                if line.strip() and not line.startswith("#"):
                    name, share = line.rstrip("\n").split("\t")[:2]
                    measured[name] = share
    return measured


def write_measured(measured):
    with open(MEASURED, "w", encoding="utf-8") as out:
        out.write("# Each written example's share of pixels apart from raylib's own program at the same frame, written by\n"
                  "# build/raylib-bench/compare.py and read by build/examples-table.py. A name, then the share in percent, or a\n"
                  "# word where no share was measured: size (the two pictures are of two sizes) or none (raylib's program\n"
                  "# drew no such frame).\n")
        for name in sorted(measured):
            out.write(f"{name}\t{measured[name]}\n")


def main():
    wanted = [a for a in sys.argv[1:] if not a.startswith("--")]
    if not wanted:
        sys.exit(__doc__)
    commit = table.raylib_commit()
    source = table.raylib_source(commit, None)
    written = table.written_examples()
    examples = [e for e in table.read_examples(source) if e["name"] in written and (e["group"] in wanted or e["name"] in wanted)]
    if not examples:
        sys.exit("no written example in " + ", ".join(wanted))
    library = build_raylib(source)
    os.makedirs(OUT, exist_ok=True)
    measured = read_measured()
    for example in examples:
        name = example["name"]
        binary = build_example(source, library, example)
        if binary is None:
            continue
        ours, frame = capture_ours(name)
        theirs = capture_theirs(binary, example, frame)
        if theirs is None:
            measured[name] = "none"
            print(f"  {name}: raylib's program drew no frame {frame}")
            continue
        share = apart(ours, theirs)
        measured[name] = "size" if share is None else f"{100 * share:.1f}"
        print(f"  {name}: frame {frame}, " + ("the pictures are of two sizes" if share is None else f"{100 * share:.1f}% apart"))
        write_measured(measured)
    write_measured(measured)


if __name__ == "__main__":
    main()

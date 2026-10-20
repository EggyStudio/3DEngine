#!/usr/bin/env python3
"""Writes .github/EXAMPLES.md, a row for each of raylib's examples and what this engine makes of it,
and the README's gallery and its count of the examples.

    build/examples-table.py [--raylib <checkout>]            # write the table and the README's counts
    build/examples-table.py [--raylib <checkout>] --check    # fail where either is out of date, or an example has no row
    build/examples-table.py [--raylib <checkout>] --triage   # print a line for each example with no row yet

The examples are read from examples/examples_list.txt of the raylib that build/raylib-bench/run.sh
pins, so none is left out and a newer raylib brings its new ones in. The checkout is the one given,
or $RAYLIB_SOURCE, or a clone of that commit under build/raylib-bench/work. An example is written
when 3DEngine.Examples/Program.cs opens it by raylib's name, and otherwise its state is the line
3DEngine.Examples/triage.tsv has for it. The groups are raylib's own, in the order its list gives.

--triage reads each unwritten example's C for the functions of raylib.h it calls that
CHEATSHEET.md does not carry, a start for its line in triage.tsv that a person then reads over.

The README's gallery shows every written example's capture and every game's, each picture opening
the program that drew it. It is a table a folder of 3DEngine.Examples under a heading naming it,
in the folders' order, each table one row of the folder's examples in the order Program.cs opens
them, and the games last, which scrolls sideways on GitHub and on nuget.org where it is wider than
the page. Each picture is the capture made small, into .github/assets/gallery, at the width the
gallery shows it, since the tables are Markdown, which nuget.org draws, where it prints HTML as its
text, and a Markdown picture is shown at its own size. --check fails where a picture is missing,
was made from a capture taken again since, or is shown by no example or game.
"""

import glob
import hashlib
import os
import re
import shutil
import subprocess
import sys
import textwrap

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TABLE = os.path.join(ROOT, ".github", "EXAMPLES.md")
README = os.path.join(ROOT, "README.md")
TRIAGE = os.path.join(ROOT, "3DEngine.Examples", "triage.tsv")
MEASURED = os.path.join(ROOT, "3DEngine.Examples", "measured.tsv")
EXAMPLES = os.path.join(ROOT, "3DEngine.Examples")
CAPTURES = os.path.join(ROOT, ".github", "assets", "examples")
GALLERY = os.path.join(ROOT, ".github", "assets", "gallery")

# The capture each of the gallery's pictures was made from, by its SHA-256, so a check finds a
# picture whose capture was taken again without making it again, which another ImageMagick would
# make in other bytes.
MADE_FROM = os.path.join(GALLERY, "captures.tsv")

# A capture made small to the width the gallery shows it at, and raylib's window's shape.
THUMBNAIL = (260, 146)

# The games under games/, in the order the workflows play them, the gallery's last row.
GAMES = ["Hopper", "Summit", "Swarm", "Rally", "Manor", "Tactics", "Tempo", "Sumo", "Wordfall", "Slide", "Jelly", "Wick", "Ore", "Pinball"]

# The lines the gallery stands between in the README.
GALLERY_START, GALLERY_END = "<!-- example-gallery -->", "<!-- /example-gallery -->"

# The table is read on GitHub, where a picture is shown from its full address.
BLOB = "https://github.com/EggyStudio/3DEngine/blob/main/"
RAW = "https://raw.githubusercontent.com/EggyStudio/3DEngine/main/"
STATES = {"written": "written", "part": "written in part", "can": "can be written", "missing": "missing", "n/a": "does not apply"}
ORDER = ["written", "part", "can", "missing", "n/a"]
GROUPS = {"core": "Core", "shapes": "Shapes", "textures": "Textures", "text": "Text", "models": "Models",
          "shaders": "Shaders", "audio": "Audio", "others": "Others"}


def raylib_commit():
    """The commit of raylib build/raylib-bench/run.sh pins."""
    with open(os.path.join(ROOT, "build", "raylib-bench", "run.sh"), encoding="utf-8") as script:
        match = re.search(r"^raylib_commit=([0-9a-f]{40})$", script.read(), re.M)
    if not match:
        sys.exit("build/raylib-bench/run.sh pins no raylib commit")
    return match.group(1)


def raylib_source(commit, given):
    """A checkout of raylib at the pinned commit, cloned under build/raylib-bench/work when none is given."""
    source = given or os.environ.get("RAYLIB_SOURCE") or os.path.join(ROOT, "build", "raylib-bench", "work", "raylib")
    head = subprocess.run(["git", "-C", source, "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip() \
        if os.path.isdir(os.path.join(source, ".git")) else ""
    if head != commit:
        # The pinned commit alone, which GitHub hands over by its hash, rather than raylib's history.
        os.makedirs(source, exist_ok=True)
        if not head:
            subprocess.run(["git", "-C", source, "init", "-q"], check=True)
            subprocess.run(["git", "-C", source, "remote", "add", "origin", "https://github.com/raysan5/raylib.git"], check=True)
        subprocess.run(["git", "-C", source, "fetch", "-q", "--depth", "1", "origin", commit], check=True)
        subprocess.run(["git", "-C", source, "checkout", "-q", "FETCH_HEAD"], check=True)
    return source


def read_examples(source):
    """Every example raylib's list names, in its order, with its group and source path."""
    examples = []
    with open(os.path.join(source, "examples", "examples_list.txt"), encoding="utf-8") as lines:
        for line in lines:
            if not line.strip() or line.startswith("#"):
                continue
            group, name = line.split(";")[:2]
            examples.append({"name": name, "group": group, "path": f"examples/{group}/{name}.c"})
    return examples


def read_triage():
    """The state and note of each example not written yet, and of a written one that differs, by name."""
    triage = {}
    if not os.path.exists(TRIAGE):
        return triage
    with open(TRIAGE, encoding="utf-8") as lines:
        for number, line in enumerate(lines, 1):
            line = line.rstrip("\n")
            if not line or line.startswith("#"):
                continue
            parts = line.split("\t")
            if len(parts) < 2 or parts[1] not in ("can", "part", "missing", "n/a", "moves"):
                sys.exit(f"triage.tsv:{number}: '{line}' is not a name, a state and a note")
            if parts[1] in ("part", "missing", "n/a", "moves") and (len(parts) < 3 or not parts[2]):
                sys.exit(f"triage.tsv:{number}: {parts[0]} is {parts[1]} and says nothing about why")
            triage[parts[0]] = (parts[1], parts[2] if len(parts) > 2 else "")
    return triage


def read_measured():
    """Each measured example's share of pixels apart from raylib's frame, by name, as build/raylib-bench/compare.py wrote it."""
    measured = {}
    if os.path.exists(MEASURED):
        with open(MEASURED, encoding="utf-8") as lines:
            for line in lines:
                if line.strip() and not line.startswith("#"):
                    name, share = line.rstrip("\n").split("\t")[:2]
                    measured[name] = share
    return measured


def written_examples():
    """Each example Program.cs opens, by name, with the file its class is written in."""
    program = open(os.path.join(EXAMPLES, "Program.cs"), encoding="utf-8").read()
    runs = dict(re.findall(r'\["([a-z0-9_]+)"\]\s*=\s*(\w+)\.Run', program))
    files = {}
    for folder, _, names in os.walk(EXAMPLES):
        if os.sep + "bin" in folder or os.sep + "obj" in folder:
            continue
        for name in names:
            if name.endswith(".cs"):
                path = os.path.join(folder, name)
                for declared in re.findall(r"\bclass\s+(\w+)", open(path, encoding="utf-8").read()):
                    files.setdefault(declared, os.path.relpath(path, ROOT).replace(os.sep, "/"))
    return {name: files[type] for name, type in runs.items() if type in files}


def carried():
    """The functions of the flat API, by name, as CHEATSHEET.md lists them."""
    cheatsheet = open(os.path.join(ROOT, "CHEATSHEET.md"), encoding="utf-8").read()
    return set(re.findall(r"^[\w<>\[\],? ]+?\b(\w+)(?:<\w+>)?\(", cheatsheet, re.M))


# What raylib's C carries because C has none of it, its strings, codepoints, files, directories,
# hashes, compression and freeing of memory, which a program here has from C# itself.
LANGUAGE = {
    "MemAlloc", "MemRealloc", "MemFree", "LoadUTF8", "UnloadUTF8", "GetCodepointCount", "GetCodepoint",
    "GetCodepointNext", "GetCodepointPrevious", "CodepointToUTF8", "LoadTextLines", "UnloadTextLines",
    "TextCopy", "TextIsEqual", "TextLength", "TextFormat", "TextSubtext", "TextRemoveSpaces", "GetTextBetween",
    "TextReplace", "TextReplaceAlloc", "TextReplaceBetween", "TextReplaceBetweenAlloc", "TextInsert",
    "TextInsertAlloc", "TextJoin", "TextSplit", "TextAppend", "TextFindIndex", "TextToUpper", "TextToLower",
    "TextToPascal", "TextToSnake", "TextToCamel", "TextToInteger", "TextToFloat", "UnloadFileData",
    "UnloadFileText", "FileRename", "FileRemove", "FileCopy", "FileMove", "FileTextReplace", "FileTextFindIndex",
    "DirectoryExists", "IsFileExtension", "IsFileHidden", "GetFileLength", "GetFileModTime", "GetFileExtension",
    "GetFileName", "GetFileNameWithoutExt", "GetDirectoryPath", "GetPrevDirectoryPath", "GetWorkingDirectory",
    "MakeDirectory", "ChangeDirectory", "IsPathFile", "IsPathDirectory", "IsPathAbsolute", "IsFileNameValid",
    "LoadDirectoryFiles", "LoadDirectoryFilesEx", "UnloadDirectoryFiles", "GetDirectoryFileCount",
    "GetDirectoryFileCountEx", "CompressData", "DecompressData", "EncodeDataBase64", "DecodeDataBase64",
    "ComputeCRC32", "ComputeMD5", "ComputeSHA1", "ComputeSHA256", "UnloadImageColors", "UnloadImagePalette",
    "UnloadCodepoints", "UnloadRandomSequence", "UnloadFontData", "UnloadMaterial",
}

# rlgl's matrix stack and its vertices one at a time, which the flat API could carry, where the
# rest of rlgl reaches OpenGL's own state.
RLGL_CARRIABLE = {
    "rlPushMatrix", "rlPopMatrix", "rlTranslatef", "rlRotatef", "rlScalef", "rlMultMatrixf", "rlLoadIdentity",
    "rlBegin", "rlEnd", "rlVertex2f", "rlVertex2i", "rlVertex3f", "rlTexCoord2f", "rlNormal3f", "rlColor4ub",
    "rlColor3f", "rlColor4f", "rlSetTexture", "rlCheckRenderBatchLimit", "rlDrawRenderBatchActive",
}


def triage_line(source, example, api, have):
    """A first line for triage.tsv, from the functions of raylib.h and rlgl the example calls."""
    code = open(os.path.join(source, example["path"]), encoding="utf-8", errors="replace").read()
    gui = "raygui.h" in code
    code = re.sub(r"/\*.*?\*/|//[^\n]*", "", code, flags=re.S)
    called = {name for name in re.findall(r"\b([A-Z]\w+)\s*\(", code) if name in api}
    gl = set(re.findall(r"\b(rl[A-Z]\w+)\s*\(", code))
    lacking = sorted((called - have - LANGUAGE) | (gl & RLGL_CARRIABLE))
    opengl = sorted(gl - RLGL_CARRIABLE)
    # Vulkan has each state rlgl sets, so such a row is missing until it is read over by hand for
    # what the example shows, which the engine may already do by means of its own.
    if opengl:
        return f"{example['name']}\tmissing\tsets OpenGL's state through rlgl ({', '.join(opengl[:4])}), to be read over for what it shows"
    gui_note = "with ImGui in raygui's place" if gui else ""
    if lacking:
        return f"{example['name']}\tmissing\t{', '.join(lacking)}" + (f", {gui_note}" if gui_note else "")
    return f"{example['name']}\tcan" + (f"\t{gui_note}" if gui_note else "")


def build(commit, examples, triage, written):
    measured = read_measured()
    names = {example["name"] for example in examples}
    unknown = sorted(set(triage) - names)
    if unknown:
        sys.exit("triage.tsv names examples raylib does not have: " + ", ".join(unknown))

    for example in examples:
        name = example["name"]
        if name in written:
            # A line kept for a written example says how it differs from raylib's, one marked part
            # names what it leaves out, and one marked moves why its picture moves by the clock or
            # the device, which the pack run's measure then leaves out.
            state, note = triage.get(name, ("can", ""))
            if state == "moves":
                note += ", so the pack run's measure leaves it out"
            example["state"], example["note"] = ("part" if state == "part" else "written"), note
        elif name in triage:
            example["state"], example["note"] = triage[name]
        else:
            sys.exit(f"{name} ({example['group']}) has no line in triage.tsv and is not written")

    groups = []
    for example in examples:
        if example["group"] not in groups:
            groups.append(example["group"])

    def counts(rows):
        return {state: sum(1 for row in rows if row["state"] == state) for state in ORDER}

    total = counts(examples)
    applies = len(examples) - total["n/a"]
    own = sorted(name for name in written if name not in names)

    out = ["# Examples", ""]
    out.append(
        f"raylib has {len(examples)} examples at the commit `build/raylib-bench/run.sh` pins, "
        f"[`{commit[:8]}`](https://github.com/raysan5/raylib/tree/{commit}/examples). Each is a row "
        "here, made by `build/examples-table.py` from raylib's own `examples_list.txt`, so a row is "
        "a thing raylib shows how to do and the table is how much of raylib a program here can do "
        "the same way.")
    out.append("")
    out.append(
        "An example written here is a program in `3DEngine.Examples` under raylib's name, in a "
        "window of 800 by 450 with raylib's scene, opened by "
        "`dotnet run --project 3DEngine.Examples -- <name>` or `./e3d open 3DEngine.Examples <name>`, "
        "and its capture stands beside the screenshot raylib keeps next to its source. One "
        "`written in part` names what it leaves out. One that `can be written` calls only what the "
        "flat API carries and waits for its turn. One that is `missing` names the functions it "
        "calls that the flat API lacks, and one that `does not apply` says why it is not a thing "
        "a program here does. `Apart` is the share of a written example's pixels apart from "
        "raylib's own program built from its source and drawn to the same frame, a pixel apart "
        "where a channel differs by more than 24 of 255, as the reference frames are compared, "
        "which `build/raylib-bench/compare.py` measures with each program at one sample a pixel "
        "unless it asks for more, a sixtieth of a second a frame, the same random seed and no "
        "input. The pack run measures every pair again on its own device and fails where "
        "one stands more than a point above the share that device recorded for it, leaving out a "
        "pair whose row says its picture moves by the clock or the device.")
    out.append("")
    out.append(
        f"**{total['written']} written, {total['part']} written in part, {total['can']} can be written, "
        f"{total['missing']} missing and {total['n/a']} {'does' if total['n/a'] == 1 else 'do'} not apply.** Of the {applies} that apply, "
        f"{total['written'] + total['part'] + total['can']} can be written with what the flat API carries.")
    out.append("")
    out.append("| Group | Written | Written in part | Can be written | Missing | Does not apply |")
    out.append("|---|---:|---:|---:|---:|---:|")
    for group in groups:
        c = counts([example for example in examples if example["group"] == group])
        title = GROUPS.get(group, group)
        out.append(f"| [{title}](#{title.lower()}) | {c['written']} | {c['part']} | {c['can']} | {c['missing']} | {c['n/a']} |")
    out.append(f"| **All** | **{total['written']}** | **{total['part']}** | **{total['can']}** | **{total['missing']}** | **{total['n/a']}** |")

    for group in groups:
        out += ["", f"## {GROUPS.get(group, group)}", "", "| Example | raylib | Here | Apart | State |", "|---|---|---|---:|---|"]
        for example in (e for e in examples if e["group"] == group):
            name = example["name"]
            source = f"https://github.com/raysan5/raylib/blob/{commit}/{example['path']}"
            shot = f"https://raw.githubusercontent.com/raysan5/raylib/{commit}/examples/{group}/{name}.png"
            state = STATES[example["state"]]
            here = ""
            if example["state"] in ("written", "part"):
                state = f"[{state}]({BLOB}{written[name]})"
                if os.path.exists(os.path.join(CAPTURES, name + ".webp")):
                    here = f'<img src="{RAW}.github/assets/examples/{name}.webp" width="200"/>'
                theirs = f'<img src="{shot}" width="200"/>'
            else:
                theirs = ""
            if example["note"]:
                state += f", {example['note']}"
            share = measured.get(name, "") if example["state"] in ("written", "part") else ""
            apart = {"size": "of two sizes", "none": "no frame"}.get(share, f"{share}%" if share else "")
            out.append(f"| [`{name}`]({source}) | {theirs} | {here} | {apart} | {state} |")

    out += ["", "## This engine's own", ""]
    out.append(
        "The programs here that are no example of raylib's, each showing what the engine has "
        "beside the flat API raylib's examples use.")
    out += ["", "| Example | Here |", "|---|---|"]
    for name in own:
        picture = f'<img src="{RAW}.github/assets/examples/{name}.webp" width="200"/>' if os.path.exists(os.path.join(CAPTURES, name + ".webp")) else ""
        out.append(f"| [`{name}`]({BLOB}{written[name]}) | {picture} |")
    return "\n".join(out) + "\n"


def gallery(written):
    """The README's gallery, a heading and a table a folder of the examples and one of the games, and
    the names of the captures it shows."""
    rows = {}
    for name, path in written.items():
        if os.path.exists(os.path.join(CAPTURES, name + ".webp")):
            rows.setdefault(path.split("/")[1], []).append((name, name, BLOB + path))
    games = [(game.lower(), game, f"{BLOB}games/{game}/Program.cs") for game in GAMES
             if os.path.exists(os.path.join(CAPTURES, game.lower() + ".webp"))]
    sections, shown = [], []
    for title, row in [*sorted(rows.items()), ("Games", games)]:
        if not row:
            continue
        heads, captions = [], []
        for picture, name, link in row:
            shown.append(picture)
            heads.append(f"[![{picture}]({RAW}.github/assets/gallery/{picture}.webp)]({link})")
            # The caption sets each column's width, its name padded with spaces that do not break to
            # thirty-two characters, about the picture's width in code, so a column is as wide as
            # its picture whatever the name, as BevyCSharp's gallery measured on both sites.
            captions.append(f"[`{name}" + "\u00a0" * max(0, 32 - len(name)) + f"`]({link})")
        # The pictures are the table's head, so each name stands under its picture.
        sections.append(f"### {title} ({len(row)})\n\n| " + " | ".join(heads) + " |\n|" + "---|" * len(heads)
                        + "\n| " + " | ".join(captions) + " |")
    return "\n\n".join(sections), shown, len(games)


def made_from():
    """The capture each picture of the gallery was made from, as its SHA-256 by its name."""
    if not os.path.exists(MADE_FROM):
        return {}
    with open(MADE_FROM, encoding="utf-8") as listed:
        return dict(line.rstrip("\n").split("\t") for line in listed if line.strip() and not line.startswith("#"))


def digest(path):
    with open(path, "rb") as file:
        return hashlib.sha256(file.read()).hexdigest()


def make_pictures(shown, check):
    """Makes the gallery's pictures whose captures changed, or names them where only checking.

    A picture is made again where it is missing or its capture's SHA-256 is not the one it was made
    from, and one the gallery does not show is taken away. ImageMagick makes it where there is
    one, and libwebp's cwebp where ImageMagick was built without WebP.
    """
    listed = made_from()
    wanted = {name: digest(os.path.join(CAPTURES, name + ".webp")) for name in shown}
    stale = [name for name, sha in wanted.items()
             if listed.get(name) != sha or not os.path.exists(os.path.join(GALLERY, name + ".webp"))]
    present = {os.path.basename(path)[:-5] for path in glob.glob(os.path.join(GALLERY, "*.webp"))}
    gone = sorted((present | set(listed)) - set(wanted))
    if check:
        return [os.path.join(GALLERY, name + ".webp") for name in sorted(stale) + gone]

    os.makedirs(GALLERY, exist_ok=True)
    magick = shutil.which("magick") or shutil.which("convert")
    width, height = THUMBNAIL
    for name in stale:
        source, picture = os.path.join(CAPTURES, name + ".webp"), os.path.join(GALLERY, name + ".webp")
        if magick:
            command = [magick, source, "-resize", f"{width}x{height}!", "-quality", "85", "-strip", picture]
        elif shutil.which("cwebp"):
            command = ["cwebp", "-quiet", "-q", "85", "-resize", str(width), str(height), source, "-o", picture]
        else:
            sys.exit("ImageMagick or cwebp is needed to make the gallery's pictures")
        subprocess.run(command, check=True)
    for name in gone:
        if os.path.exists(os.path.join(GALLERY, name + ".webp")):
            os.remove(os.path.join(GALLERY, name + ".webp"))
    with open(MADE_FROM, "w", encoding="utf-8") as out:
        out.write("# The capture each picture here was made from, by its SHA-256, which build/examples-table.py\n"
                  "# reads to make a picture again where its capture was taken again.\n")
        out.writelines(f"{name}\t{wanted[name]}\n" for name in sorted(wanted))
    return []


def counted(readme, examples, total, own, games):
    """The README with a line before its gallery counting the table's examples, the engine's own and
    the games."""
    lines = readme.split("\n")
    states = [f"{total['written']} are written here"]
    states += [f"{total[state]} {words}" for state, words in (("part", "in part"), ("can", "can be written"), ("missing", "are missing")) if total[state]]
    states.append(f"{total['n/a']} {'does' if total['n/a'] == 1 else 'do'} not apply")
    line = textwrap.fill(
        f"**Of raylib's {len(examples)} examples {', '.join(states[:-1])} and {states[-1]}, and beside them are "
        f"{len(own)} examples of the engine's own and {games} games.**",
        width=100, break_long_words=False, break_on_hyphens=False)
    # The line stands between the section's opening paragraph and the gallery, in place of the line
    # written before.
    first = lines.index(GALLERY_START)
    paragraph_end = first - 1
    while lines[paragraph_end] == "":
        paragraph_end -= 1
    if lines[paragraph_end].startswith("**Of raylib's") or lines[paragraph_end].endswith("games.**"):
        top = paragraph_end
        while not lines[top].startswith("**Of raylib's"):
            top -= 1
        paragraph_end = top - 1
        while lines[paragraph_end] == "":
            paragraph_end -= 1
    lines[paragraph_end + 1:first] = ["", *line.split("\n"), ""]
    return "\n".join(lines)


def main():
    arguments = sys.argv[1:]
    given = arguments[arguments.index("--raylib") + 1] if "--raylib" in arguments else None
    commit = raylib_commit()
    source = raylib_source(commit, given)
    examples = read_examples(source)
    triage = read_triage()
    written = written_examples()

    if "--triage" in arguments:
        header = open(os.path.join(source, "src", "raylib.h"), encoding="utf-8").read()
        api = set(re.findall(r"^RLAPI\s+[\w\s\*]+?\b(\w+)\s*\(", header, re.M))
        have = carried()
        for example in examples:
            if example["name"] not in written and example["name"] not in triage:
                print(triage_line(source, example, api, have))
        return

    table = build(commit, examples, triage, written)
    names = {example["name"] for example in examples}
    total = {state: sum(1 for example in examples if example["state"] == state) for state in ORDER}
    readme = open(README, encoding="utf-8").read()
    pictures, shown, games = gallery(written)
    marked = re.compile(re.escape(GALLERY_START) + r"\n(?:.*?\n)?" + re.escape(GALLERY_END), re.S)
    if not marked.search(readme):
        sys.exit(f"README.md has no {GALLERY_START} and {GALLERY_END} around the gallery")
    written_readme = marked.sub(lambda _: GALLERY_START + "\n" + (pictures + "\n" if pictures else "") + GALLERY_END, readme, count=1)
    written_readme = counted(written_readme, examples, total, [name for name in written if name not in names], games)
    if "--check" in arguments:
        current = open(TABLE, encoding="utf-8").read() if os.path.exists(TABLE) else ""
        if current != table:
            sys.exit(".github/EXAMPLES.md is out of date; run build/examples-table.py")
        if readme != written_readme:
            sys.exit("README.md's gallery or its count of the examples is out of date; run build/examples-table.py")
        if stale := make_pictures(shown, check=True):
            sys.exit("the gallery's pictures are out of date, so run build/examples-table.py, for "
                     + ", ".join(os.path.relpath(path, ROOT) for path in stale))
        return
    make_pictures(shown, check=False)
    with open(TABLE, "w", encoding="utf-8") as out:
        out.write(table)
    with open(README, "w", encoding="utf-8") as out:
        out.write(written_readme)
    print(f"wrote {os.path.relpath(TABLE, ROOT)}, {len(examples)} examples, README.md's gallery of {len(shown)} pictures and its count")


if __name__ == "__main__":
    main()

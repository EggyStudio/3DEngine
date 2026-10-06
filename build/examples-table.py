#!/usr/bin/env python3
"""Writes .github/EXAMPLES.md, a row for each of raylib's examples and what this engine makes of it.

    build/examples-table.py [--raylib <checkout>]            # write the table
    build/examples-table.py [--raylib <checkout>] --check    # fail where it is out of date, or an example has no row
    build/examples-table.py [--raylib <checkout>] --triage   # print a line for each example with no row yet

The examples are read from examples/examples_list.txt of the raylib that build/raylib-bench/run.sh
pins, so none is left out and a newer raylib brings its new ones in. The checkout is the one given,
or $RAYLIB_SOURCE, or a clone of that commit under build/raylib-bench/work. An example is written
when 3DEngine.Examples/Program.cs opens it by raylib's name, and otherwise its state is the line
3DEngine.Examples/triage.tsv has for it. The groups are raylib's own, in the order its list gives.

--triage reads each unwritten example's C for the functions of raylib.h it calls that
CHEATSHEET.md does not carry, a start for its line in triage.tsv that a person then reads over.
"""

import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TABLE = os.path.join(ROOT, ".github", "EXAMPLES.md")
TRIAGE = os.path.join(ROOT, "3DEngine.Examples", "triage.tsv")
MEASURED = os.path.join(ROOT, "3DEngine.Examples", "measured.tsv")
EXAMPLES = os.path.join(ROOT, "3DEngine.Examples")
CAPTURES = os.path.join(ROOT, ".github", "assets", "examples")

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
            if len(parts) < 2 or parts[1] not in ("can", "part", "missing", "n/a"):
                sys.exit(f"triage.tsv:{number}: '{line}' is not a name, a state and a note")
            if parts[1] in ("part", "missing", "n/a") and (len(parts) < 3 or not parts[2]):
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
            # A line kept for a written example says how it differs from raylib's, and one marked
            # part names what it leaves out.
            state, note = triage.get(name, ("can", ""))
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
        "unless it asks for more.")
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
    if "--check" in arguments:
        current = open(TABLE, encoding="utf-8").read() if os.path.exists(TABLE) else ""
        if current != table:
            sys.exit(".github/EXAMPLES.md is out of date; run build/examples-table.py")
        return
    with open(TABLE, "w", encoding="utf-8") as out:
        out.write(table)
    print(f"wrote {os.path.relpath(TABLE, ROOT)}, {len(examples)} examples")


if __name__ == "__main__":
    main()

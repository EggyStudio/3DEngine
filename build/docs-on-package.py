#!/usr/bin/env python3
"""Builds every C# block of the guides under docs/ against the packed package, so a block that
calls what the surface no longer has fails the run with its page and block named.

    build/docs-on-package.py [page.md ...] [--package DIR] [--keep DIR]

Each ```csharp block becomes a file of a project outside the repository on the newest package in
build/package, or the folder --package names, as build/examples-on-package.sh builds the examples.
Each block's lines are sorted by what they declare at its own level: types go in a namespace of
the block's own, members (a method, a field, a property) in a class of its own, and statements in a
method of it, inside a loop run once so a fragment's continue and break have one, after the lines a
comment before its fence gives, which declare what the fragment takes from the page around it:

    <!-- compiled with:
    Vector2 player = default;
    -->

A block a comment before its fence marks as skipped, with its reason, is left out:

    <!-- not compiled: a sketch of a shader's code, in Slang -->

Every file takes the usings each page's blocks name and the flat API's own. An error is said with
the page and its line, as a GitHub annotation under GitHub Actions. docs/first-game.md is left out,
since build/first-game.sh builds every step of it as a whole program. --keep writes the project to a
folder to look at, where it is otherwise thrown away.
"""
import glob
import os
import re
import shutil
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from page import annotate

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
USINGS = ["System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Numerics", "Engine", "ImGuiNET"]
STATIC_USINGS = ["Engine.Engine3D"]
FENCE = re.compile(r"^```csharp\s*$")
CONTEXT = re.compile(r"^<!-- compiled with:\s*$")
SKIP = re.compile(r"^<!-- not compiled: (?P<reason>.+?) -->\s*$")
# A line that starts a type's declaration at the block's own level.
TYPE = re.compile(r"^(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|static|sealed|abstract|readonly|partial|unsafe|file|record)\s+)*"
                  r"(?:class|struct|record|enum|interface|delegate)\b")


def blocks(path):
    """Each csharp block of a page: its first line's number, its lines, the lines a context comment
    before it gives, and the reason a skip comment gives, or None."""
    lines = open(path, encoding="utf-8").read().split("\n")
    found = []
    i = 0
    while i < len(lines):
        if not FENCE.match(lines[i]):
            i += 1
            continue
        context, reason = [], None
        # The comments right above the fence, the nearest first, past blank lines.
        j = i - 1
        while j >= 0 and not lines[j].strip():
            j -= 1
        if j >= 0 and (m := SKIP.match(lines[j])):
            reason = m.group("reason")
        elif j >= 0 and lines[j].strip() == "-->":
            k = j - 1
            while k >= 0 and not CONTEXT.match(lines[k]):
                k -= 1
            if k >= 0:
                context = lines[k + 1:j]
        end = i + 1
        while end < len(lines) and not lines[end].startswith("```"):
            end += 1
        found.append((i + 2, lines[i + 1:end], context, reason))
        i = end + 1
    return found


MEMBER = re.compile(r"(?:\[[^\]]*\]\s*)*(?:public|private|internal|protected|static|override|async|unsafe)\b")
USING_LINE = re.compile(r"^using (?:static )?[\w.]+;")
STRINGS = re.compile(r'@?\$?"(?:[^"\\]|\\.)*"|\'(?:[^\'\\]|\\.)*\'|//.*$')


def depth_change(line):
    """How far a line moves the depth of braces, its strings, characters and comment left out."""
    bare = STRINGS.sub("", line)
    return bare.count("{") - bare.count("}")


def parts(code):
    """A block's lines sorted by what they belong to at the block's own level, each with its index
    in the block: types, which go in the file's namespace, members, which go in a class of their own,
    and statements, which go in a method. Comments and attributes go with the declaration after them."""
    types, members, statements = [], [], []
    pending, i = [], 0
    while i < len(code):
        line, text = code[i], code[i].strip()
        if USING_LINE.match(line) or not text:
            i += 1
            continue
        if not line.startswith((" ", "\t")) and (text.startswith("//") or re.fullmatch(r"(\[[^\]]*\]\s*)+", text)):
            pending.append(i)
            i += 1
            continue
        target = None
        if not line.startswith((" ", "\t")):
            target = types if TYPE.match(text) else members if MEMBER.match(text) else None
        if target is None:
            statements.extend(pending)
            pending = []
            # A statement and the lines its braces hold.
            depth = depth_change(line)
            statements.append(i)
            i += 1
            while depth > 0 and i < len(code):
                depth += depth_change(code[i])
                statements.append(i)
                i += 1
            continue
        # A declaration, to where its braces close, or its line where it ends there.
        target.extend(pending)
        pending = []
        depth, opened = depth_change(line), "{" in STRINGS.sub("", line)
        target.append(i)
        i += 1
        while i < len(code) and (depth > 0 or (not opened and not STRINGS.sub("", code[i - 1]).rstrip().endswith((";", "}")))):
            depth += depth_change(code[i])
            opened |= "{" in STRINGS.sub("", code[i])
            target.append(i)
            i += 1
    statements.extend(pending)
    return types, members, statements


def write(project, pages):
    """Writes the project's files and returns, for each file, its page, the page line of its block's
    first line, which block line each line of the file is, and the block's number."""
    places = {}
    usings = set(USINGS)
    for page in pages:
        for _, code, context, reason in blocks(page):
            if reason is None:
                usings.update(m.group(1) for line in code + context if (m := re.match(r"^using (?!static)([\w.]+);", line)))
    header = "".join(f"global using {u};\n" for u in sorted(usings)) + "".join(f"global using static {u};\n" for u in STATIC_USINGS)
    with open(os.path.join(project, "Usings.cs"), "w", encoding="utf-8") as f:
        f.write(header)
    for page in pages:
        slug = re.sub(r"\W", "_", os.path.splitext(os.path.basename(page))[0]).title().replace("_", "")
        for number, (first, code, context, reason) in enumerate(blocks(page), 1):
            if reason is not None:
                continue
            name = f"{slug}{number:02}"
            types, members, statements = parts(code)
            # The context is sorted the same way, a type it declares for the block's attributes going
            # where they can see it.
            context_types, context_members, context_statements = parts(context)
            # Each line of the file and the block line it is, None for a line of the scaffold or the
            # context, so an error is said at the page's line.
            out = [("namespace Docs." + name + ";", None), ("", None)]
            out += [(context[i], None) for i in context_types] + [(code[i], i) for i in types]
            out += [(f"internal partial class {name}", None), ("{", None)]
            out += [("    " + context[i], None) for i in context_members] + [("    " + code[i], i) for i in members]
            if statements or context_statements:
                out += [("    internal static async System.Threading.Tasks.Task Run()", None), ("    {", None),
                        ("        await System.Threading.Tasks.Task.CompletedTask;", None),
                        ("        for (var once = true; once; once = false)", None), ("        {", None)]
                out += [("            " + context[i], None) for i in context_statements]
                out += [("            " + code[i], i) for i in statements]
                out += [("        }", None), ("    }", None)]
            out += [("}", None)]
            with open(os.path.join(project, f"{name}.cs"), "w", encoding="utf-8") as f:
                f.write("\n".join(text for text, _ in out) + "\n")
            places[f"{name}.cs"] = (page, first, [at for _, at in out], number)
    return places


def main():
    args = sys.argv[1:]
    def option(name, default):
        if name in args:
            i = args.index(name)
            value = args[i + 1]
            del args[i:i + 2]
            return value
        return default
    package = os.path.abspath(option("--package", os.path.join(ROOT, "build", "package")))
    keep = option("--keep", None)
    pages = [os.path.abspath(p) for p in args] or sorted(p for p in glob.glob(os.path.join(ROOT, "docs", "*.md"))
                                                          if not p.endswith("first-game.md"))
    project = os.path.abspath(keep) if keep else tempfile.mkdtemp(prefix="docs-on-package-")
    if keep and os.path.isdir(project):
        shutil.rmtree(project)
    os.makedirs(project, exist_ok=True)
    try:
        places = write(project, pages)
        with open(os.path.join(project, "Docs.csproj"), "w", encoding="utf-8") as f:
            f.write("""<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Library</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        <!-- A fragment's variables are declared and may go unused, and its awaits may not come. -->
        <NoWarn>CS0168;CS0219;CS1998;CS8321;CS0162;CS8618;CS0649;CS0169;CS0414;CS8600;CS8602;CS8604;CS8625</NoWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="3DEngine" Version="0.1.0-*" />
    </ItemGroup>
</Project>
""")
        with open(os.path.join(project, "nuget.config"), "w", encoding="utf-8") as f:
            f.write(f"""<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="engine" value="{package}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="engine"><package pattern="3DEngine" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
""")
        restored = subprocess.run(["dotnet", "restore", project, "--force-evaluate"], capture_output=True, text=True)
        if restored.returncode != 0:
            print(restored.stdout + restored.stderr)
            sys.exit("the project of the guides' blocks did not restore")
        built = subprocess.run(["dotnet", "build", project, "--no-restore", "-v", "q", "--nologo", "-clp:NoSummary"],
                               capture_output=True, text=True)
        errors = {}
        for line in (built.stdout + built.stderr).splitlines():
            m = re.match(r"^(?P<file>[^(]+)\((?P<line>\d+),(?P<col>\d+)\): error (?P<code>\w+): (?P<message>.*?)(?: \[[^\[\]]*\.csproj\])?$", line.strip())
            if not m:
                continue
            name = os.path.basename(m.group("file"))
            page, first, lines, number = places.get(name, (None, 0, [], 0))
            if page is None:
                continue
            row = int(m.group("line")) - 1
            block_line = lines[row] if row < len(lines) else None
            # A scaffold or context line's error is said at the block's first line.
            at = first + (block_line if block_line is not None else 0)
            # A page of the repository by its path from the root, any other whole.
            where = os.path.relpath(page, ROOT) if page.startswith(ROOT + os.sep) else page
            errors[(where, at, m.group("code"), m.group("message"))] = number
        for (where, at, code, message), number in sorted(errors.items()):
            print(f"{where}:{at}: block {number}: {code} {message}")
            annotate("error", f"{where}, block {number}", [f"{code} {message}"])
        blocks_built = len(places)
        if errors:
            sys.exit(f"{len(errors)} error(s) in the guides' blocks, of {blocks_built} built")
        if built.returncode != 0:
            print(built.stdout + built.stderr)
            sys.exit("the project of the guides' blocks did not build")
        print(f"the guides' {blocks_built} blocks build on the package")
    finally:
        if not keep:
            shutil.rmtree(project, ignore_errors=True)


if __name__ == "__main__":
    main()

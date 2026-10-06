#!/usr/bin/env python3
"""Runs a workflow step's script in bash and, where it fails having said nothing, says what failed.

    build/step.py SCRIPT [--workflow FILE] [--sessions DIR]

The build workflow's examples job runs each of its steps through this, as the shell GitHub runs a
step's script in (`shell: python3 build/step.py {0}`). The script runs as GitHub's own bash runs
one, under `bash -e`, so the first command that fails ends it, and what it prints is passed on as it
comes. A step that fails and prints no `::error::` of its own is given one, which names the step,
the command that failed with its line and exit code, the step's last lines, and the last lines at a
warning or worse of each session log under build/sessions written while it ran, as the test page
names its causes (NORM.md, N 6.7). Before it, a command such as `status=$(./e3d command ...)` ended a
step with `Process completed with exit code 4` and nothing else, its error taken into the variable.

The step is named as the workflow names it, found by its script in the workflow file, which is the
one GITHUB_WORKFLOW_REF names or the one --workflow gives. It exits with the script's exit code.
"""

import argparse
import os
import re
import subprocess
import sys
import tempfile
import time
from collections import deque

sys.dont_write_bytecode = True
from page import annotate, fit, summarize

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SESSIONS = os.path.join(ROOT, "build", "sessions")
LAST_LINES = 6
LOG_LINES = 3
LEVEL = re.compile(r"\[(WARN |ERROR|FATAL)\]")

# Each command that fails is written to the file E3D_STEP_FAILED names, with its exit code and line,
# by a trap bash runs on an error, kept by functions and subshells (-E). The step's script is read in
# by the shell the trap is set in, so its lines are numbered as the workflow shows them.
PROLOGUE = """e3d_step_script=$1; shift
trap 'printf "%s\\t%s\\t%s\\n" "$?" "$LINENO" "$BASH_COMMAND" >> "$E3D_STEP_FAILED"' ERR
. "$e3d_step_script"
"""


def run(script):
    """Runs the script, printing what it prints, and returns its exit code, its last lines,
    whether it said an error of its own, and the commands that failed in it."""
    failed = tempfile.NamedTemporaryFile(prefix="e3d-step-", suffix=".txt", delete=False)
    failed.close()
    last = deque(maxlen=LAST_LINES)
    said = False
    try:
        environment = dict(os.environ, E3D_STEP_FAILED=failed.name)
        process = subprocess.Popen(["bash", "--noprofile", "--norc", "-eE", "-c", PROLOGUE, "step", script],
                                   stdout=subprocess.PIPE, stderr=subprocess.STDOUT, env=environment)
        for raw in process.stdout:
            line = raw.decode("utf-8", "replace").rstrip("\r\n")
            print(line, flush=True)
            said |= line.startswith("::error")
            if line.strip():
                last.append(line)
        code = process.wait()
        with open(failed.name, encoding="utf-8", errors="replace") as f:
            commands = [line.rstrip("\n").split("\t", 2) for line in f if line.count("\t") >= 2]
    finally:
        os.remove(failed.name)
    return code, list(last), said, commands


def step_name(script, workflow):
    """The step's name in the workflow whose script is the one given, or None where none is."""
    try:
        with open(workflow, encoding="utf-8") as f:
            lines = f.read().split("\n")
    except OSError:
        return None
    sought = script.strip()
    name = None
    for index, line in enumerate(lines):
        if m := re.match(r"^(\s*)- (\w[\w-]*):\s*(.*)$", line):
            # A step begins, by its name or by whatever key comes first in it.
            name = m.group(3).strip() if m.group(2) == "name" else None
            if m.group(2) != "run":
                continue
            line = line.replace("- run:", "  run:", 1)
        m = re.match(r"^(\s*)run:\s*(.*)$", line)
        if not m:
            continue
        indent, value = len(m.group(1)), m.group(2).strip()
        if value in ("|", "|-", "|+", ">", ">-"):
            block = []
            for following in lines[index + 1:]:
                if following.strip() and len(following) - len(following.lstrip()) <= indent:
                    break
                block.append(following)
            depth = min((len(b) - len(b.lstrip()) for b in block if b.strip()), default=0)
            text = "\n".join(b[depth:] for b in block)
        else:
            text = value
        if text.strip() == sought:
            return name or f"Run {sought.splitlines()[0]}"
    return None


def workflow_file(given):
    if given:
        return given
    # owner/repository/.github/workflows/build.yml@refs/heads/main
    ref = os.environ.get("GITHUB_WORKFLOW_REF", "").split("@")[0]
    repository = os.environ.get("GITHUB_REPOSITORY", "")
    if ref.startswith(repository + "/"):
        return os.path.join(ROOT, ref[len(repository) + 1:])
    return None


def session_lines(sessions, since):
    """The last lines at a warning or worse of each session log in the folder written since the time given."""
    found = []
    try:
        names = sorted(os.listdir(sessions))
    except OSError:
        return found
    for name in names:
        path = os.path.join(sessions, name)
        if not name.endswith(".log") or os.path.getmtime(path) < since:
            continue
        with open(path, encoding="utf-8", errors="replace") as f:
            logged = [re.sub(r"^\[[^]]*\] ", "", line.rstrip("\n"))[:300] for line in f if LEVEL.search(line)]
        found.append((name, logged[-LOG_LINES:]))
    return found


def failure(name, code, last, commands, logs):
    """The error's title and lines: what failed and with what code, the step's last lines, and the
    session logs' last lines at a warning or worse."""
    title = f"{name}: exit code {code}"
    # The command that ended the step is the last that failed with its code; one that failed inside
    # a substitution whose value was used does not end it.
    ending = next((c for c in reversed(commands) if c[0] == str(code)), None)
    lines = [f"`{ending[2].strip()}` on line {ending[1]} of the step ended with exit code {code}." if ending
             else f"The step ended with exit code {code} by its own exit, no command failing."]
    if last:
        lines += ["Its last lines:"] + ["    " + line for line in last]
    for log, logged in logs:
        lines.append(f"{log}, its last lines at a warning or worse:" if logged else f"{log} holds no line at a warning or worse.")
        lines += ["    " + line for line in logged]
    return title, fit(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("script")
    parser.add_argument("--workflow", help="the workflow file to name the step from, in place of GITHUB_WORKFLOW_REF's")
    parser.add_argument("--sessions", default=SESSIONS, help="the folder of the session logs, build/sessions where not given")
    args = parser.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    with open(args.script, encoding="utf-8", errors="replace") as f:
        text = f.read()

    started = time.time()
    code, last, said, commands = run(args.script)
    if code == 0 or said:
        return code

    workflow = workflow_file(args.workflow)
    name = (step_name(text, workflow) if workflow else None) or f"The step beginning `{text.strip().splitlines()[0] if text.strip() else ''}`"
    title, lines = failure(name, code, last, commands, session_lines(args.sessions, started))
    print(f"{title}\n" + "\n".join(lines), flush=True)
    annotate("error", title, lines)
    summarize([f"### {title}", ""] + [line if line.startswith("    ") else line + "  " for line in lines])
    return code


if __name__ == "__main__":
    sys.exit(main())

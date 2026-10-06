"""What the scripts a workflow runs share to say what happened where a reader can see it.

A run's log and its summary need a reader signed in to GitHub, and its annotations do not, so a
failure is said in an annotation as well as on the page, which is the job's summary (NORM.md, N 6.7).
build/test.py writes the tests' page with these, build/step.py the error of a step that fails with
nothing said, and build/raylib-bench/compare.py the shares it measured for the first time.
"""

import os

PAGE_LINES = 200
LINE_WIDTH = 240
# GitHub shows ten annotations of each kind from a step and leaves out the rest.
ANNOTATIONS = 10


def fit(lines):
    """The lines, each cut to LINE_WIDTH, and no more than PAGE_LINES of them."""
    lines = [line if len(line) <= LINE_WIDTH else line[:LINE_WIDTH - 1] + "…" for line in lines]
    if len(lines) > PAGE_LINES:
        lines = lines[:PAGE_LINES - 1] + [f"({len(lines) - PAGE_LINES + 1} lines more are left out)"]
    return lines


def escape(text, in_property=False):
    """Text as a workflow command carries it, a title's colons and commas escaped as well."""
    text = text.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
    return text.replace(":", "%3A").replace(",", "%2C") if in_property else text


def on_github():
    return os.environ.get("GITHUB_ACTIONS") == "true"


def annotate(kind, title, lines):
    """Prints an annotation of the kind given, error, warning or notice, under GitHub Actions."""
    if on_github():
        print(f"::{kind} title={escape(title, True)}::{escape(chr(10).join(lines))}", flush=True)


def summarize(lines):
    """Adds the lines to the job's summary under GitHub Actions."""
    summary = os.environ.get("GITHUB_STEP_SUMMARY") if on_github() else None
    if summary:
        with open(summary, "a", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n\n")

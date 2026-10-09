#!/usr/bin/env python3
"""Runs the engine's tests and writes a page of what happened, at most 200 lines.

    build/test.py [PART ...] [--parts] [--no-build]    # the suite whole, or parts of it by name
    build/test.py --read DIR                           # the page of results already in a folder
    build/test.py --digest DIR ...                     # one page from several systems' digests

The suite runs whole, as one process, held to a time and a memory. A process that ends by itself,
passing or failing, is read from its results file. One that is lost, by a crash, a hang, its time
or its memory, is said first on the page, with how far a test that prints its progress, as
`[leak test] app 37 of 100`, had got, and the minidump a test host that died left in
TestResults/dumps, with the stack of the thread it was written for where dotnet-dump, named by
E3D_DOTNET_DUMP or on the path, reads it, and the suite runs again in parts, each a process of its own under the same
limits, so a part that is lost costs only its own tests. A part is a name after
`Engine.Tests.` that holds thirty tests or more, as `Rendering`, and everything else is one more,
whose filter is the negation of the others, so no test falls between two parts.

The page has the run's counts, the lost processes, the failures by cause, the most frequent first,
and the lines logged as warnings or errors, or with no level, that the output repeated most, left
out when none repeats. It ends the log between two marking lines, and is written to
TestResults/digest.md and digest.json. Under GitHub Actions it is also the job's summary, with a
cause an error annotation. What the processes print goes to TestResults/output*.txt, and the log has
a line for each process. It exits 0 when every test passed and no process was lost.
"""

import argparse
import json
import os
import platform
import re
import shlex
import shutil
import signal
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from collections import Counter

sys.dont_write_bytecode = True
from page import ANNOTATIONS, LINE_WIDTH, PAGE_LINES, annotate, fit, summarize

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT = "3DEngine.Tests"
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
PREFIX = "Engine.Tests."

CAUSES_SHOWN = 10
FRAMES_SHOWN = 6
DUMP_FRAMES_SHOWN = 8
DUMP_THREADS_SHOWN = 4
MESSAGE_LINES = 5
TESTS_SHOWN = 4
REPEATED_SHOWN = 3
PART_SIZE = 30
BEGIN = "=" * 30 + " the page " + "=" * 30
END = "=" * 30 + " end of the page " + "=" * 30


# -- Running

class Process:
    """One run of the tests in a process of its own, and how it ended."""

    def __init__(self, label, filter_):
        self.label, self.filter = label, filter_
        self.name = "results" if label == "the suite" else "results-" + re.sub(r"\W+", "-", label).strip("-")
        self.output = self.name.replace("results", "output", 1) + ".txt"
        self.exit_code = None
        self.seconds = 0.0
        self.peak_mb = 0
        self.lost = None          # None, or "crash", "hang", "time" or "memory"
        self.running = []         # the tests it was in, where the blame collector says
        self.after = None         # the last test to end before it was lost
        self.last_lines = []
        self.progress = None      # the last line a test printed of how far it had got, as "[leak test] app 37 of 100"
        self.dumps = []           # the minidumps a lost test host left
        self.dump_lines = []      # what dotnet-dump read in the ones it left, a line each
        self.counts = Counter()

    def summary(self):
        how = LOSSES[self.lost] if self.lost else "ended"
        held = f", {self.peak_mb:,} MB at most" if self.peak_mb else ""
        tests = f"{self.counts['Passed']:,} passed, {self.counts['Failed']:,} failed, {self.counts['NotExecuted']:,} skipped, "
        return f"{self.label}: {tests}{how} after {duration(self.seconds)}{held}, exit code {self.exit_code}, its output in {self.output}"


LOSSES = {"time": "ended at its time limit", "memory": "ended at its memory limit", "hang": "lost to a test that hung", "crash": "lost to a crash"}


def run(dotnet, process, args, results):
    command = dotnet + ["test", args.project]
    if args.no_build:
        command.append("--no-build")
    if process.filter:
        command += ["--filter", process.filter]
    command += ["--results-directory", results,
                "--logger", f"trx;LogFileName={process.name}.trx",
                "--logger", "console;verbosity=normal",
                "--blame", "--blame-hang-timeout", f"{max(1, round(args.hang_minutes * 60))}s",
                "--blame-hang-dump-type", "none"]
    output_path = os.path.join(results, process.output)
    started = time.monotonic()
    with open(output_path, "w", encoding="utf-8", errors="replace") as output:
        options = {"start_new_session": True} if os.name != "nt" else {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP}
        # A test host that dies leaves a minidump of itself in the results, which the jobs upload
        # with them, so a crash in native code, a driver's, can be read where the page cannot say it.
        dumps = os.path.join(results, "dumps")
        os.makedirs(dumps, exist_ok=True)
        before = set(os.listdir(dumps))
        environment = dict(os.environ, DOTNET_DbgEnableMiniDump="1", DOTNET_DbgMiniDumpType="1",
                           DOTNET_DbgMiniDumpName=os.path.join(dumps, "%e-%p.dmp"))
        child = subprocess.Popen(command, stdout=output, stderr=subprocess.STDOUT, cwd=ROOT, env=environment, **options)
        limit_seconds = args.timeout_minutes * 60
        while child.poll() is None:
            time.sleep(0.25)
            held = largest_in_tree(child.pid)
            if held is not None:
                process.peak_mb = max(process.peak_mb, held)
            if time.monotonic() - started > limit_seconds:
                process.lost = "time"
            elif held is not None and held > args.memory_mb:
                process.lost = "memory"
            if process.lost:
                kill_tree(child)
                break
        process.exit_code = child.wait()
    process.seconds = time.monotonic() - started

    text = read_text(output_path)
    if process.lost is None:
        if "inactivity time of" in text:
            process.lost = "hang"
        elif "Test Run Aborted" in text or "test run was aborted" in text or not os.path.exists(os.path.join(results, process.name + ".trx")):
            process.lost = "crash"
    process.counts = Counter(outcome for outcome, _, _ in read_results(os.path.join(results, process.name + ".trx")).values())
    if process.lost:
        process.running = tests_running(text)
        process.after = last_ended(text)
        process.last_lines = last_lines(text)
        progress = re.findall(r"^\[[a-z][^\]]*\] .+$", text, re.M)
        process.progress = progress[-1].strip()[:LINE_WIDTH] if progress else None
        process.dumps = sorted(os.listdir(dumps))
        tool = dump_tool()
        for name in sorted(set(process.dumps) - before):
            if tool and name.endswith(".dmp"):
                process.dump_lines += read_dump(tool, os.path.join(dumps, name))
    return process


# -- A minidump, read where it was made

def dump_tool():
    """The command that reads a minidump, E3D_DOTNET_DUMP's or dotnet-dump on the path, or None."""
    named = os.environ.get("E3D_DOTNET_DUMP")
    if named:
        return shlex.split(named, posix=os.name != "nt")
    found = shutil.which("dotnet-dump")
    return [found] if found else None


def read_dump(tool, path):
    """
    What dotnet-dump reads in a minidump, written whole beside it as <dump>.txt, and the lines the
    page shows: the thread it was written for, the thread that faulted, its managed exception where
    it has one, and its frames with their modules, or where it runs no managed code, as a driver's
    thread, what each managed thread was in, a few frames each. A dump can only be read on the
    system that made it, which a macOS dump is not anywhere else, so the job reads its own.
    """
    accounts = {}
    for command in ("threads", "clrthreads", "pe", "clrstack -f", "clrstack -all -f"):
        try:
            accounts[command] = subprocess.run(tool + ["analyze", path, "-c", command, "-c", "exit"], capture_output=True, text=True,
                                               errors="replace", timeout=300, cwd=ROOT).stdout
        except (OSError, subprocess.TimeoutExpired) as error:
            return [f"dotnet-dump could not read `{os.path.basename(path)}`: {error}"[:LINE_WIDTH]]
    with open(path + ".txt", "w", encoding="utf-8") as f:
        for command, text in accounts.items():
            f.write(f"> {command}\n{text}\n")
    return [line[:LINE_WIDTH] for line in faulting(os.path.basename(path), accounts)]


def faulting(name, accounts):
    """The page's account of a dump from dotnet-dump's answers to the commands read_dump asks."""
    current = re.search(r"^\s*\*\s*(\d+)\s+0x([0-9A-Fa-f]+)", accounts["threads"], re.M)
    if not current:
        return [f"dotnet-dump read `{name}` and named no thread it was written for"]
    os_id = int(current.group(2), 16)
    managed = {int(m, 16) for m in re.findall(r"^\s*\d+\s+\d+\s+([0-9a-fA-F]+)\s+[0-9A-Fa-f]{8,}\s", accounts["clrthreads"], re.M)}
    runs = os_id in managed
    lines = [f"dotnet-dump read `{name}`, written for thread {current.group(1)}, OS id {os_id:#x}, "
             + ("a thread the runtime runs" if runs else "a thread the runtime does not run, as a driver's or a native library's")]
    kind = re.search(r"^Exception type:\s*(.+)$", accounts["pe"], re.M)
    if kind:
        message = re.search(r"^Message:\s*(.+)$", accounts["pe"], re.M)
        lines.append(kind.group(1).strip() + (f": {message.group(1).strip()}" if message else ""))
    frames = stack_frames(accounts["clrstack -f"])
    if frames:
        lines += ["at " + frame for frame in frames[:DUMP_FRAMES_SHOWN]]
        return lines
    lines.append("It holds no managed frames, and the threads that do were in")
    shown = 0
    for thread, block in re.findall(r"^OS Thread Id:\s*(0x[0-9A-Fa-f]+)[^\n]*\n(.*?)(?=^OS Thread Id:|\Z)", accounts["clrstack -all -f"], re.M | re.S):
        top = stack_frames(block)[:3]
        if not top or shown == DUMP_THREADS_SHOWN:
            continue
        shown += 1
        lines.append(f"  {thread}: " + " < ".join(top))
    return lines


def stack_frames(text):
    """The call sites of a stack dotnet-dump printed, its explicit frames left out and a source path cut to its file."""
    frames = []
    for line in text.splitlines():
        match = re.match(r"^[0-9A-Fa-f]{16}\s+(?:[0-9A-Fa-f]{16}\s+)?(.+?)\s*$", line)
        if not match or match.group(1).startswith("["):
            continue
        site = re.sub(r"\s*\[([^\]@]+) @ (\d+)\]$", lambda m: f" in {os.path.basename(m.group(1).replace(chr(92), '/'))}:{m.group(2)}", match.group(1))
        frames.append(re.sub(r" \+ \d+(?= in |$)", "", site))
    return frames


def last_lines(text):
    """What the process printed last before dotnet test's own account of the loss, and that account's reason."""
    lines = text.splitlines()
    epilogue = ("The active test run was aborted", "Data collector 'Blame'", "Results File:", "Test Run Aborted")
    end = next((i for i, line in enumerate(lines) if line.lstrip().startswith(epilogue)), len(lines))
    # Stack frames left out, since a crash prints dozens after the words that say what it was.
    before = [line for line in lines[:end] if line.strip() and not line.strip().startswith("at ")][-8:]
    reason = [line for line in lines[end:] if "Reason:" in line][:1]
    return [line[:LINE_WIDTH] for line in before + reason]


def tests_running(text):
    """The tests the blame collector names as running when the process was lost, or else the last ones to end."""
    match = re.search(r"The tests? running when the crash occurred:\s*\n(.*?)(?:\nThis test may|\nThese tests may|\Z)", text, re.S)
    if match:
        return [line.strip() for line in match.group(1).splitlines() if line.strip()][:TESTS_SHOWN]
    return []


def last_ended(text):
    ended = re.findall(r"^\s+(?:Passed|Failed|Skipped) (\S+)", text, re.M)
    return ended[-1] if ended else None


def list_tests(dotnet, args):
    command = dotnet + ["test", args.project, "--list-tests"] + (["--no-build"] if args.no_build else [])
    text = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, errors="replace").stdout
    names, listing = [], False
    for line in text.splitlines():
        if "The following Tests are available" in line:
            listing = True
        elif listing and line.startswith("    ") and line.strip():
            names.append(line.strip())
    return names


def parts_of(names):
    """A process for each name after Engine.Tests. that holds PART_SIZE tests or more, and one for the rest."""
    counts = Counter(segment(name) for name in names)
    large = sorted(name for name, count in counts.items() if count >= PART_SIZE and name)
    parts = [Process(name, f"FullyQualifiedName~{PREFIX}{name}.") for name in large]
    rest = "&".join(f"FullyQualifiedName!~{PREFIX}{name}." for name in large)
    parts.append(Process("everything else", rest or None))
    return parts


def segment(name):
    return name[len(PREFIX):].split(".")[0].split("(")[0] if name.startswith(PREFIX) else ""


# -- What a process holds

def largest_in_tree(pid):
    """The resident memory of the largest process in the tree under pid, in MB, or None where it cannot be read."""
    try:
        table = process_table()
    except Exception:
        return None
    if not table:
        return None
    tree, frontier = {pid}, [pid]
    while frontier:
        parent = frontier.pop()
        for child, (ppid, _) in table.items():
            if ppid == parent and child not in tree:
                tree.add(child)
                frontier.append(child)
    sizes = [table[p][1] for p in tree if p in table]
    return max(sizes) // (1024 * 1024) if sizes else None


def process_table():
    """Each process's parent and resident bytes."""
    if sys.platform.startswith("linux"):
        table = {}
        for entry in os.listdir("/proc"):
            if not entry.isdigit():
                continue
            try:
                with open(f"/proc/{entry}/status", encoding="ascii", errors="replace") as status:
                    fields = dict(line.split(":", 1) for line in status if ":" in line)
                table[int(entry)] = (int(fields["PPid"]), int(fields.get("VmRSS", "0 kB").split()[0]) * 1024)
            except (OSError, KeyError, ValueError):
                continue
        return table
    if sys.platform == "darwin":
        out = subprocess.run(["ps", "-A", "-o", "pid=,ppid=,rss="], capture_output=True, text=True).stdout
        return {int(p): (int(pp), int(rss) * 1024) for p, pp, rss in (line.split() for line in out.splitlines() if len(line.split()) == 3)}
    if os.name == "nt":
        return windows_process_table()
    return {}


def windows_process_table():
    import ctypes
    from ctypes import wintypes

    class Entry(ctypes.Structure):
        _fields_ = [("dwSize", wintypes.DWORD), ("cntUsage", wintypes.DWORD), ("th32ProcessID", wintypes.DWORD),
                    ("th32DefaultHeapID", ctypes.c_size_t), ("th32ModuleID", wintypes.DWORD), ("cntThreads", wintypes.DWORD),
                    ("th32ParentProcessID", wintypes.DWORD), ("pcPriClassBase", ctypes.c_long), ("dwFlags", wintypes.DWORD),
                    ("szExeFile", ctypes.c_wchar * 260)]

    class Counters(ctypes.Structure):
        _fields_ = [("cb", wintypes.DWORD), ("PageFaultCount", wintypes.DWORD), ("PeakWorkingSetSize", ctypes.c_size_t),
                    ("WorkingSetSize", ctypes.c_size_t), ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
                    ("QuotaPagedPoolUsage", ctypes.c_size_t), ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
                    ("QuotaNonPagedPoolUsage", ctypes.c_size_t), ("PagefileUsage", ctypes.c_size_t), ("PeakPagefileUsage", ctypes.c_size_t)]

    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    kernel32.OpenProcess.restype = wintypes.HANDLE
    snapshot = kernel32.CreateToolhelp32Snapshot(0x2, 0)
    parents = {}
    entry = Entry()
    entry.dwSize = ctypes.sizeof(Entry)
    more = kernel32.Process32FirstW(snapshot, ctypes.byref(entry))
    while more:
        parents[entry.th32ProcessID] = entry.th32ParentProcessID
        more = kernel32.Process32NextW(snapshot, ctypes.byref(entry))
    kernel32.CloseHandle(snapshot)

    table = {}
    for pid, ppid in parents.items():
        handle = kernel32.OpenProcess(0x1000 | 0x0010, False, pid)
        if not handle:
            continue
        counters = Counters()
        counters.cb = ctypes.sizeof(Counters)
        if kernel32.K32GetProcessMemoryInfo(handle, ctypes.byref(counters), counters.cb):
            table[pid] = (ppid, counters.WorkingSetSize)
        kernel32.CloseHandle(handle)
    return table


def kill_tree(child):
    if os.name == "nt":
        subprocess.run(["taskkill", "/T", "/F", "/PID", str(child.pid)], capture_output=True)
        return
    try:
        table = process_table()
    except Exception:
        table = {}
    tree, frontier = [child.pid], [child.pid]
    while frontier:
        parent = frontier.pop()
        for pid, (ppid, _) in table.items():
            if ppid == parent and pid not in tree:
                tree.append(pid)
                frontier.append(pid)
    try:
        os.killpg(child.pid, signal.SIGKILL)
    except OSError:
        pass
    for pid in tree:
        try:
            os.kill(pid, signal.SIGKILL)
        except OSError:
            pass


# -- Reading results

def read_text(path):
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            return f.read()
    except OSError:
        return ""


def read_results(path):
    """Each test in a results file, by name, as its outcome, the first line of its message and its stack."""
    try:
        run_ = ET.parse(path).getroot()
    except (OSError, ET.ParseError):
        return {}
    results = {}
    for result in run_.iterfind("t:Results/t:UnitTestResult", NS):
        message = result.findtext("t:Output/t:ErrorInfo/t:Message", default="", namespaces=NS)
        stack = result.findtext("t:Output/t:ErrorInfo/t:StackTrace", default="", namespaces=NS)
        results[result.get("testName", "?")] = (result.get("outcome", ""), message, stack)
    return results


def first_line(text):
    for line in (text or "").splitlines():
        if line.strip():
            return line.strip()
    return ""


def cause_of(message, stack):
    """The exception's type, the first line of its message with numbers and paths taken out, and the engine's first frame."""
    line = first_line(message)
    match = re.match(r"^([A-Za-z_][\w.`+]*(?:Exception|Error))\s*:\s*(.*)$", line)
    kind, text = (match.group(1), match.group(2)) if match else ("assertion", line)
    plain = re.sub(r"(?:[A-Za-z]:\\|/)[^\s'\",;)]+", "<path>", text)
    plain = re.sub(r"0x[0-9A-Fa-f]+|\d+(?:\.\d+)?", "#", plain)
    frames = [frame_of(l) for l in stack.splitlines() if l.strip().startswith("at Engine.")]
    engine = next((f.split("(")[0] for f in frames if not f.startswith(PREFIX)), "")
    return kind, plain[:200], engine, line, frames[:FRAMES_SHOWN], [l.strip() for l in message.splitlines() if l.strip()]


def frame_of(line):
    text = line.strip()[3:]
    match = re.match(r"^(.*?) in (.*):line (\d+)$", text)
    return f"{match.group(1)} in {os.path.basename(match.group(2).replace(chr(92), '/'))}:{match.group(3)}" if match else text


# A line the engine logged, after the seconds it was logged at, and its level.
LOGGED = re.compile(r"^(?:\[\s*[\d.]+s\]\s*)?\[(TRACE|DEBUG|INFO|WARN|ERROR|FATAL)\s*\]")


def repeated_lines(texts):
    """
    The lines the output repeated most, as one count for lines that differ only in their numbers.
    Only a line logged as a warning or an error counts, or one with no level, as an exception's
    message is, since a line logged below them repeats by design: each app's start logs the
    engine's banner, which filled the section with 2,190 lines of it where a system that throws in
    every frame was to be seen.
    """
    counts, first = Counter(), {}
    for text in texts:
        for line in text.splitlines():
            stripped = line.strip()
            if len(stripped) < 12 or stripped.startswith("at ") or re.match(r"^(Passed|Failed|Skipped) ", stripped) or stripped.startswith("[xUnit.net"):
                continue
            logged = LOGGED.match(stripped)
            if logged and logged.group(1) in ("TRACE", "DEBUG", "INFO"):
                continue
            key = re.sub(r"\d+", "#", re.sub(r"^\[\s*[\d.]+s\]\s*", "", stripped))
            counts[key] += 1
            first.setdefault(key, stripped)
    return [(first[key], count) for key, count in counts.most_common(REPEATED_SHOWN) if count > 1]


# -- The page

def digest(results_dir, processes, listed, seconds):
    """What the run comes to, as the page and its annotations read it."""
    parts_ran = any(p.label != "the suite" for p in processes)
    trx = [os.path.join(results_dir, p.name + ".trx") for p in processes if p.label != "the suite" or not parts_ran] if processes \
        else [os.path.join(results_dir, f) for f in sorted(os.listdir(results_dir)) if f.endswith(".trx")]
    outputs = [os.path.join(results_dir, p.output) for p in processes] if processes \
        else [os.path.join(results_dir, f) for f in sorted(os.listdir(results_dir)) if f.startswith("output") and f.endswith(".txt")]

    results = {}
    for path in trx:
        results.update(read_results(path))
    outcomes = Counter(outcome for outcome, _, _ in results.values())
    causes = {}
    for name, (outcome, message, stack) in sorted(results.items()):
        if outcome != "Failed":
            continue
        kind, plain, engine, line, frames, lines = cause_of(message, stack)
        key = f"{kind}|{plain}|{engine}"
        cause = causes.setdefault(key, {"key": key, "type": kind, "message": line, "lines": lines[:MESSAGE_LINES],
                                        "more_lines": max(0, len(lines) - MESSAGE_LINES), "frame": engine, "frames": frames,
                                        "count": 0, "tests": []})
        cause["count"] += 1
        cause["tests"].append(name)
    # A theory may be listed once by its method's name and report each case by its arguments, so a
    # listed name has a result where any case of its method has one.
    methods = {name.split("(")[0] for name in results}
    no_result = sum(1 for name in set(listed) if name not in results and name.split("(")[0] not in methods)

    return {
        "system": {"Darwin": "macOS"}.get(platform.system(), platform.system()),
        "commit": commit(),
        "passed": outcomes["Passed"], "failed": outcomes["Failed"], "skipped": outcomes["NotExecuted"], "no_result": no_result,
        "seconds": round(seconds), "peak_mb": max((p.peak_mb for p in processes), default=0),
        "processes": [{"label": p.label, "lost": p.lost, "seconds": round(p.seconds), "peak_mb": p.peak_mb, "exit_code": p.exit_code,
                       "running": p.running, "after": p.after, "last_lines": p.last_lines, "summary": p.summary(),
                       "progress": p.progress, "dumps": p.dumps, "dump_lines": p.dump_lines} for p in processes],
        "causes": sorted(causes.values(), key=lambda c: (-c["count"], c["key"])),
        "repeated": repeated_lines(read_text(path) for path in outputs),
    }


def head(d):
    return (f"Tests on {d['system']} at {d['commit']}: {d['passed']:,} passed, {d['failed']:,} failed, {d['skipped']:,} skipped, "
            f"{d['no_result']:,} without a result, in {duration(d['seconds'])}"
            + (f", {d['peak_mb']:,} MB at most" if d["peak_mb"] else ""))


def entry(cause):
    """A cause's message, its frames and its tests with a count of the rest, a line each."""
    shown = cause["tests"][:TESTS_SHOWN]
    more = len(cause["tests"]) - len(shown)
    message = cause.get("lines") or [cause["message"]]
    left = cause.get("more_lines", 0)
    return (message + ([f"({left:,} line{'s' if left != 1 else ''} more)"] if left else [])
            + [f"at {frame}" for frame in cause.get("frames", [])]
            + [", ".join(f"`{t}`" for t in shown) + (f" and {more:,} more" if more > 0 else "")])


def lost_entry(p):
    """A lost process's account, its tests and its last lines, a line each."""
    lines = [p["summary"]]
    if p["running"]:
        lines.append("In " + ", ".join(f"`{t}`" for t in p["running"]))
    elif p.get("after"):
        lines.append(f"After `{p['after']}`, the last test to end")
    if p.get("progress"):
        lines.append(f"The test had got as far as `{p['progress']}`")
    if p.get("dumps"):
        lines.append(f"It left the minidump `{'`, `'.join(p['dumps'])}` among the results, under `dumps`")
    return lines + p.get("dump_lines", []) + p["last_lines"]


def page(d):
    lines = [f"## {head(d)}", ""]
    for p in d["processes"]:
        if not p["lost"]:
            continue
        held = f" holding {p['peak_mb']:,} MB" if p["peak_mb"] else ""
        lines.append(f"### Lost: {p['label']}, {LOSSES[p['lost']]}, after {duration(p['seconds'])}{held}, exit code {p['exit_code']}")
        if p["running"]:
            lines.append("In " + ", ".join(f"`{t}`" for t in p["running"]))
        elif p.get("after"):
            lines.append(f"After `{p['after']}`, the last test to end")
        if p.get("progress"):
            lines.append(f"The test had got as far as `{p['progress']}`")
        if p.get("dumps"):
            lines.append(f"It left the minidump `{'`, `'.join(p['dumps'])}` among the results, under `dumps`")
        if p.get("dump_lines"):
            lines += ["    " + line for line in p["dump_lines"]]
        lines.append("Its last lines:")
        lines += ["    " + line for line in p["last_lines"]]
        lines.append("")
    if any(p["lost"] for p in d["processes"]):
        lines.append("The processes:")
        lines += [f"- {p['summary']}" for p in d["processes"]]
        lines.append("")

    causes = d["causes"]
    if causes:
        lines.append(f"### {d['failed']:,} failed, of {len(causes)} cause{'s' if len(causes) != 1 else ''}" + (f", the first {CAUSES_SHOWN} here" if len(causes) > CAUSES_SHOWN else ""))
        for cause in causes[:CAUSES_SHOWN]:
            lines.append(f"**{cause['count']:,} × {cause['type']}**" + (f" at `{cause['frame']}`" if cause["frame"] else ""))
            lines += ["    " + line for line in entry(cause)]
            lines.append("")

    if d["repeated"]:
        lines.append("### Repeated most in the output")
        lines += [f"- {count:,} × `{line}`" for line, count in d["repeated"]]
    return fit(lines)


def merged_page(digests):
    lines = ["## Tests by system", ""]
    lines += [f"- {head(d)}" for d in digests] or ["No system wrote a page, so each job ended before its tests did."]
    lines += [f"- Lost on {d['system']}: {p['summary']}" for d in digests for p in d["processes"] if p["lost"]]
    causes = {}
    for d in digests:
        for cause in d["causes"]:
            merged = causes.setdefault(cause["key"], dict(cause, count=0, tests=[], systems=[]))
            merged["count"] += cause["count"]
            merged["tests"] += [t for t in cause["tests"] if t not in merged["tests"]]
            merged["systems"].append(d["system"])
    ordered = sorted(causes.values(), key=lambda c: (-c["count"], c["key"]))
    if ordered:
        lines += ["", f"### Causes, the first {CAUSES_SHOWN} of {len(ordered)}" if len(ordered) > CAUSES_SHOWN else "### Causes"]
        for cause in ordered[:CAUSES_SHOWN]:
            lines.append(f"**{cause['count']:,} × {cause['type']}** on {', '.join(cause['systems'])}" + (f" at `{cause['frame']}`" if cause["frame"] else ""))
            lines += ["    " + line for line in entry(cause)]
    return fit(lines), ordered


def annotations(lost, causes, head_line, repeated):
    """
    The annotations, which anyone can read where a run's log and summary need signing in: an error
    for each lost process and each cause, ten at most, each with its whole entry of the page, and
    a notice with the page's head and the lines the output repeated most.
    """
    errors = [(f"Lost: {p['label']}", lost_entry(p)) for p in lost]
    errors += [(f"{c['count']:,} × {c['type']}" + (f" on {', '.join(c['systems'])}" if c.get("systems") else "")
                + (f" at {c['frame']}" if c["frame"] else ""), entry(c)) for c in causes]
    notice = [head_line] + [f"{count:,} × {line}" for line, count in repeated]
    return errors[:ANNOTATIONS], notice


def publish(lines, notes, results_dir=None, d=None):
    errors, notice = notes
    print(BEGIN)
    print("\n".join(lines))
    print(END)
    if results_dir is not None:
        with open(os.path.join(results_dir, "digest.md"), "w", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
        with open(os.path.join(results_dir, "digest.json"), "w", encoding="utf-8") as f:
            json.dump(d, f, indent=1)
    for title, message in errors:
        annotate("error", title, message)
    annotate("notice", "The tests", notice)
    summarize(lines)


def duration(seconds):
    seconds = round(seconds)
    return f"{seconds // 60} m {seconds % 60} s" if seconds >= 60 else f"{seconds} s"


def commit():
    if os.environ.get("GITHUB_SHA"):
        return os.environ["GITHUB_SHA"][:8]
    try:
        return subprocess.run(["git", "rev-parse", "--short=8", "HEAD"], cwd=ROOT, capture_output=True, text=True).stdout.strip() or "?"
    except OSError:
        return "?"


# -- Main

def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("parts", nargs="*", help="names after Engine.Tests. to run, each a process, as Rendering")
    parser.add_argument("--parts", dest="all_parts", action="store_true", help="run the suite in its parts, each a process")
    parser.add_argument("--no-build", action="store_true", help="pass --no-build to dotnet test")
    parser.add_argument("--read", metavar="DIR", help="write the page of the results already in DIR")
    parser.add_argument("--digest", metavar="DIR", nargs="+", help="one page from the digest.json in each DIR")
    parser.add_argument("--results", default=os.path.join(ROOT, PROJECT, "TestResults"), help="where results, output and the page go")
    parser.add_argument("--project", default=PROJECT)
    parser.add_argument("--dotnet", default="dotnet", help="the command that stands for dotnet")
    parser.add_argument("--timeout-minutes", type=float, default=40)
    parser.add_argument("--hang-minutes", type=float, default=5)
    parser.add_argument("--memory-mb", type=int, default=4096)
    args = parser.parse_args()
    # A Windows console's code page may lack what the page writes, so the log is UTF-8 everywhere.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    if args.digest:
        digests = []
        for folder in args.digest:
            path = folder if folder.endswith(".json") else os.path.join(folder, "digest.json")
            try:
                with open(path, encoding="utf-8") as f:
                    digests.append(json.load(f))
            except (OSError, ValueError):
                print(f"no digest at {path}")
        lines, causes = merged_page(digests)
        lost = [dict(p, label=f"{p['label']} on {d['system']}") for d in digests for p in d["processes"] if p["lost"]]
        repeated = [r for d in digests for r in d["repeated"]][:REPEATED_SHOWN]
        publish(lines, annotations(lost, causes, "; ".join(head(d) for d in digests) or lines[-1], repeated))
        return 0

    if args.read:
        d = digest(args.read, [], [], 0)
        publish(page(d), annotations([], d["causes"], head(d), d["repeated"]), args.read, d)
        return 0 if d["failed"] == 0 else 1

    results = os.path.abspath(args.results)
    os.makedirs(results, exist_ok=True)
    for name in os.listdir(results):
        if (name.startswith("results") and name.endswith(".trx")) or (name.startswith("output") and name.endswith(".txt")) or name.startswith("digest."):
            os.remove(os.path.join(results, name))
    dotnet = shlex.split(args.dotnet, posix=os.name != "nt")

    started = time.monotonic()
    processes, listed = [], []
    if args.parts:
        processes = [Process(name, f"FullyQualifiedName~{PREFIX}{name}.") for name in args.parts]
    elif args.all_parts:
        listed = list_tests(dotnet, args)
        processes = parts_of(listed)
    else:
        processes = [Process("the suite", None)]

    done = []
    for process in processes:
        done.append(run(dotnet, process, args, results))
        print(process.summary(), flush=True)
    if not args.parts and not args.all_parts and done[0].lost:
        # Run again in parts, each a process, so a part that is lost costs only its own tests.
        listed = list_tests(dotnet, args)
        for process in parts_of(listed):
            done.append(run(dotnet, process, args, results))
            print(process.summary(), flush=True)

    d = digest(results, done, listed, time.monotonic() - started)
    publish(page(d), annotations([p for p in d["processes"] if p["lost"]], d["causes"], head(d), d["repeated"]), results, d)
    return 0 if d["failed"] == 0 and d["no_result"] == 0 and not any(p.lost for p in done) else 1


if __name__ == "__main__":
    sys.exit(main())

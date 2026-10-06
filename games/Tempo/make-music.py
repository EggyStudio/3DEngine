#!/usr/bin/env python3
"""Writes Tempo's song as a ProTracker module and its chart of notes, from the same rows.

    games/Tempo/make-music.py

The song is four channels at speed 6 and 125 beats a minute, so a row is 0.12 seconds and a beat
four rows: a kick, a snare, a hi-hat and a bass that turns into a lead, each a sample synthesized
here, in four patterns played in an order of eight, about a minute. Its chart is a note for each
kick, snare, hat between beats and bass or lead note, in the four lanes the game draws, at the time
its row is played, so the game scores against what is heard. Both are written into resources/.
"""
import json
import math
import os
import random
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
RATE = 8287          # what a sample played at C-2, period 428, is heard at
ROW_SECONDS = 6 * 2.5 / 125

random.seed(20261007)


def clamp8(v):
    return max(-128, min(127, int(round(v))))


def kick():
    out, phase = [], 0.0
    for i in range(int(RATE * 0.3)):
        t = i / RATE
        freq = 45 + 120 * math.exp(-t * 18)
        phase += 2 * math.pi * freq / RATE
        out.append(clamp8(127 * math.sin(phase) * math.exp(-t * 9)))
    return out


def snare():
    out = []
    for i in range(int(RATE * 0.22)):
        t = i / RATE
        tone = math.sin(2 * math.pi * 185 * t) * math.exp(-t * 30)
        noise = (random.random() * 2 - 1) * math.exp(-t * 14)
        out.append(clamp8(110 * (0.45 * tone + 0.75 * noise)))
    return out


def hat():
    out, last = [], 0.0
    for i in range(int(RATE * 0.06)):
        t = i / RATE
        n = random.random() * 2 - 1
        high = n - last
        last = n
        out.append(clamp8(70 * high * math.exp(-t * 60)))
    return out


def saw(length=32):
    return [clamp8(100 * (2 * (i / length) - 1)) for i in range(length)]


def square(length=32):
    return [90 if i < length // 2 else -90 for i in range(length)]


# Name, data, volume, and the loop's start and length in samples, a sample played once looping none.
SAMPLES = [
    ("kick", kick(), 64, 0, 0),
    ("snare", snare(), 56, 0, 0),
    ("hat", hat(), 40, 0, 0),
    ("bass", saw(), 44, 0, 32),
    ("lead", square(), 34, 0, 32),
]

# ProTracker's periods, octaves 1 to 3, from C.
PERIODS = [856, 808, 762, 720, 678, 640, 604, 570, 538, 508, 480, 453,
           428, 404, 381, 360, 339, 320, 302, 285, 269, 254, 240, 226,
           214, 202, 190, 180, 170, 160, 151, 143, 135, 127, 120, 113]
NOTE = {n: i for i, n in enumerate(["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"])}


def period(name, octave):
    return PERIODS[(octave - 1) * 12 + NOTE[name]]


EMPTY = (0, 0, 0, 0)


def cell(sample, per, effect=0, param=0):
    return (sample, per, effect, param)


def pattern(kicks, snares, hats, line, line_sample):
    """64 rows of four channels. kicks, snares and hats are the rows they sound on, line the
    rows of the fourth channel, each a note name and octave or None to stop it."""
    rows = [[EMPTY] * 4 for _ in range(64)]
    for r in kicks:
        rows[r][0] = cell(1, period("C", 2))
    for r in snares:
        rows[r][1] = cell(2, period("C", 2))
    for r in hats:
        rows[r][2] = cell(3, period("C", 3))
    for r, note in line.items():
        rows[r][3] = cell(line_sample, period(*note)) if note else cell(0, 0, 0xC, 0)
    return rows


beats = list(range(0, 64, 4))
offbeat_hats = list(range(2, 64, 4))
eighth_hats = list(range(0, 64, 2))
bassline = {0: ("A", 1), 6: None, 8: ("A", 1), 12: ("C", 2), 16: ("D", 2), 22: None, 24: ("D", 2), 28: ("E", 2),
            32: ("F", 1), 38: None, 40: ("F", 1), 44: ("G", 1), 48: ("E", 1), 54: None, 56: ("E", 1), 60: ("G#", 1)}
melody = {0: ("A", 3), 4: ("C", 3), 8: ("E", 3), 12: ("A", 3), 16: ("G", 3), 20: ("F", 3), 24: ("E", 3), 30: None,
          32: ("F", 3), 36: ("A", 3), 40: ("C", 3), 44: ("F", 3), 48: ("E", 3), 52: ("D", 3), 56: ("B", 2), 60: ("G#", 2)}

PATTERNS = [
    # An intro of hats, then the kick from halfway.
    pattern(list(range(32, 64, 4)), [], eighth_hats, {}, 4),
    # The groove: the kick on each beat, the snare on two and four, the hats and the bass.
    pattern(beats, [4, 12, 20, 28, 36, 44, 52, 60], eighth_hats, bassline, 4),
    # The lead over it.
    pattern(beats, [4, 12, 20, 28, 36, 44, 52, 60], offbeat_hats, melody, 5),
    # A break, the snare rolling into the last bar.
    pattern([0, 16, 32, 48], [4, 12, 20, 28, 36, 44, 48, 50, 52, 54, 56, 58, 60, 62], offbeat_hats, {0: ("A", 1), 32: ("F", 1), 62: None}, 4),
]
ORDER = [0, 1, 1, 2, 2, 3, 1, 2]


def write_module(path):
    out = bytearray()
    out += b"Tempo".ljust(20, b"\0")
    for i in range(31):
        if i < len(SAMPLES):
            name, data, volume, loop_start, loop_length = SAMPLES[i]
            if len(data) % 2: data.append(0)
            out += name.encode().ljust(22, b"\0")
            out += struct.pack(">HBBHH", len(data) // 2, 0, volume, loop_start // 2, max(1, loop_length // 2))
        else:
            out += b"\0" * 22 + struct.pack(">HBBHH", 0, 0, 0, 0, 1)
    out += bytes([len(ORDER), 127])
    out += bytes(ORDER + [0] * (128 - len(ORDER)))
    out += b"M.K."
    for rows in PATTERNS:
        for row in rows:
            for sample, per, effect, param in row:
                out += bytes([(sample & 0xF0) | ((per >> 8) & 0x0F), per & 0xFF, ((sample & 0x0F) << 4) | effect, param])
    for _, data, _, _, _ in SAMPLES:
        out += bytes(b & 0xFF for b in data)
    with open(path, "wb") as f:
        f.write(out)


def write_chart(path):
    """A note in lane 0 for each kick, 1 for each snare, 2 for each hat between beats, and 3 for
    each note of the bass or the lead, at the time its row is heard."""
    notes = []
    for o, p in enumerate(ORDER):
        for r, row in enumerate(PATTERNS[p]):
            time = round((o * 64 + r) * ROW_SECONDS, 4)
            if row[0][0]: notes.append({"time": time, "lane": 0})
            if row[1][0]: notes.append({"time": time, "lane": 1})
            if row[2][0] and r % 4 == 2: notes.append({"time": time, "lane": 2})
            if row[3][0]: notes.append({"time": time, "lane": 3})
    chart = {"title": "Tempo", "bpm": 125, "length": round(len(ORDER) * 64 * ROW_SECONDS, 4), "notes": notes}
    with open(path, "w") as f:
        json.dump(chart, f, indent=1)
    return len(notes)


os.makedirs(os.path.join(HERE, "resources"), exist_ok=True)
write_module(os.path.join(HERE, "resources", "song.mod"))
count = write_chart(os.path.join(HERE, "resources", "chart.json"))
print(f"wrote resources/song.mod and resources/chart.json, {count} notes over {len(ORDER) * 64 * ROW_SECONDS:.1f} seconds")

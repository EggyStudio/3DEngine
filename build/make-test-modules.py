#!/usr/bin/env python3
"""Writes 3DEngine.Tests/Platform/Audio/tone.xm and tone.mod, two tracker modules the tracker tests
play, so what they sound like is known. Each plays at speed 6 and 125 beats a minute, a row of six
ticks of 20 milliseconds, and its one sample is a square wave of 32 frames, 100 and -100 of 128,
looped.

tone.xm has two channels and linear frequencies. Its first pattern, of 16 rows, plays C-4 on the
first channel at row 0, which its sample plays at 8,363 Hz, a tone of 261.3 Hz, sets the volume
column to half at row 4 and lets the note go at row 8, which with no volume envelope cuts it. Its
second pattern, of 8 rows, jumps back to the first order at row 3, so the song is 16 and 4 rows long
before it comes round.

tone.mod is a ProTracker module of four channels. Its one pattern, of 64 rows, plays period 428,
C-4 at 8,287 Hz, a tone of 259 Hz, on the first channel, which is panned left, at row 0, and sets
its volume to 0 with C00 at row 32."""
import os, struct

here = os.path.join(os.path.dirname(__file__), "..", "3DEngine.Tests", "Platform", "Audio")
square = [100] * 16 + [-100] * 16


def xm():
    def pattern(rows, cells):
        """A pattern of two channels, each cell a (note, instrument, volume, effect, parameter) or
        empty, written packed where it is empty and whole where it is not."""
        data = b""
        for row in range(rows):
            for channel in range(2):
                cell = cells.get((row, channel))
                data += bytes(cell) if cell else b"\x80"
        return struct.pack("<IBHH", 9, 0, rows, len(data)) + data

    orders = bytes([0, 1]) + bytes(254)
    header = struct.pack("<IHHHHHHHH", 276, 2, 0, 2, 2, 1, 1, 6, 125) + orders
    first = pattern(16, {(0, 0): (49, 1, 0, 0, 0), (4, 0): (0, 0, 0x30, 0, 0), (8, 0): (97, 0, 0, 0, 0)})
    second = pattern(8, {(3, 1): (0, 0, 0, 0xB, 0)})

    instrument = struct.pack("<I22sBHI", 263, b"square", 0, 1, 40) + bytes(96) + bytes(96)
    instrument += bytes(14) + struct.pack("<H", 0)
    instrument += bytes(263 - len(instrument))
    sample_header = struct.pack("<IIIBbBBbB22s", len(square), 0, len(square), 64, 0, 1, 128, 0, 0, b"square")
    deltas, last = b"", 0
    for value in square:
        deltas += bytes([(value - last) & 0xFF])
        last = value

    data = (b"Extended Module: " + b"tone".ljust(20, b"\0") + b"\x1a" + b"make-test-modules".ljust(20, b"\0")
            + struct.pack("<H", 0x0104) + header + first + second + instrument + sample_header + deltas)
    with open(os.path.join(here, "tone.xm"), "wb") as f:
        f.write(data)
    print("tone.xm", len(data), "bytes")


def mod():
    def sample(name, words, volume, loop_start, loop_words):
        return name.ljust(22, b"\0") + struct.pack(">HBBHH", words, 0, volume, loop_start, loop_words)

    samples = sample(b"square", len(square) // 2, 64, 0, len(square) // 2) + sample(b"", 0, 0, 0, 1) * 30
    orders = bytes([1, 127]) + bytes(128)
    cells = bytearray(64 * 4 * 4)
    def put(row, channel, sample_number, period, effect, parameter):
        at = (row * 4 + channel) * 4
        cells[at:at + 4] = bytes([(sample_number & 0xF0) | (period >> 8), period & 0xFF, ((sample_number & 0x0F) << 4) | effect, parameter])
    put(0, 0, 1, 428, 0, 0)
    put(32, 0, 0, 0, 0xC, 0)

    data = b"tone".ljust(20, b"\0") + samples + orders + b"M.K." + bytes(cells) + bytes((v & 0xFF) for v in square)
    with open(os.path.join(here, "tone.mod"), "wb") as f:
        f.write(data)
    print("tone.mod", len(data), "bytes")


xm()
mod()

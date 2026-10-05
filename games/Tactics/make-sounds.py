#!/usr/bin/env python3
"""Makes Tactics' sounds under resources/ as WAV files, synthesized here. Needs only Python."""
import math, os, random, struct, wave

here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "resources")
RATE = 22050
rng = random.Random(3)

def wav(name, samples):
    with wave.open(os.path.join(here, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32000)) for s in samples))

def tone(freq, seconds, decay, volume=0.5):
    return [volume * math.sin(math.tau * freq * i / RATE) * math.exp(-i / RATE * decay) for i in range(int(RATE * seconds))]

wav("select", tone(660, 0.12, 25))
wav("step", [(rng.random() * 2 - 1) * math.exp(-i / RATE * 50) * 0.35 for i in range(int(RATE * 0.08))])
wav("hit", [((rng.random() * 2 - 1) * 0.6 + math.sin(math.tau * 140 * i / RATE)) * math.exp(-i / RATE * 14) * 0.5 for i in range(int(RATE * 0.3))])
wav("fall", [math.sin(math.tau * (300 - 200 * i / (RATE * 0.6)) * i / RATE) * math.exp(-i / RATE * 4) * 0.5 for i in range(int(RATE * 0.6))])
wav("win", sum((tone(f, 0.25, 6, 0.4) for f in (523, 659, 784, 1046)), []))

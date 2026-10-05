# Writes Rally's sounds, synthesized, into resources. Run again after changing one.
import math, os, random, struct, wave

here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "resources")
RATE = 22050

def write(name, samples):
    with wave.open(os.path.join(here, name), "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 30000)) for s in samples))

def tone(freq, seconds, volume=0.5, shape=lambda t: 1):
    n = int(RATE * seconds); phase = 0; out = []
    for k in range(n):
        t = k / n; f = freq(t) if callable(freq) else freq
        phase += 2 * math.pi * f / RATE
        out.append(volume * shape(t) * (math.sin(phase) + 0.3 * math.sin(2 * phase)))
    return out

random.seed(5)
decay = lambda t: (1 - t) ** 2

# An engine's drone, a second long and looping cleanly: a low fundamental, its firing pulses and
# a little roughness, every frequency a whole number of cycles in the second.
engine = []
for k in range(RATE):
    t = k / RATE
    base = math.sin(2 * math.pi * 55 * t) * 0.35 + math.sin(2 * math.pi * 110 * t) * 0.25
    pulse = (math.sin(2 * math.pi * 220 * t) > 0.6) * 0.15
    rough = math.sin(2 * math.pi * 330 * t + math.sin(2 * math.pi * 7 * t) * 2) * 0.08
    engine.append(base + pulse + rough)
write("engine.wav", engine)

# Tyres sliding on gravel, noise thinned toward the end.
write("skid.wav", [random.uniform(-1, 1) * 0.3 * (1 - (k / 6000)) ** 0.5 for k in range(6000)])
write("checkpoint.wav", tone(880, 0.08, 0.4, decay) + tone(1320, 0.2, 0.4, decay))
write("beep.wav", tone(660, 0.15, 0.4, decay))
write("go.wav", tone(990, 0.4, 0.45, decay))
write("lap.wav", sum((tone(f, 0.12, 0.4, decay) for f in (523, 659, 784)), []) + tone(1047, 0.45, 0.4, decay))
write("finish.wav", sum((tone(f, 0.16, 0.45, decay) for f in (392, 523, 659, 784)), []) + tone(1047, 0.8, 0.45, decay))
print("Rally's sounds written to", here)

# Writes Sumo's sounds, synthesized, into resources/sounds. Run again after changing one.
import math, os, random, struct, wave

here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "resources", "sounds")
RATE = 22050

def write(name, samples):
    with wave.open(os.path.join(here, name), "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 30000)) for s in samples))

def tone(freq, seconds, volume=0.5, shape=lambda t: 1, partials=((1, 1), (2, 0.3))):
    n = int(RATE * seconds); phase = 0; out = []
    for k in range(n):
        t = k / n; f = freq(t) if callable(freq) else freq
        phase += 2 * math.pi * f / RATE
        out.append(volume * shape(t) * sum(a * math.sin(m * phase) for m, a in partials))
    return out

def noise(seconds, volume, shape):
    n = int(RATE * seconds); last = 0; out = []
    for k in range(n):
        # Smoothed, so the noise is a rush and not a hiss.
        last = last * 0.7 + random.uniform(-1, 1) * 0.3
        out.append(last * volume * shape(k / n))
    return out

def mix(*parts):
    length = max(len(p) for p in parts)
    return [sum(p[k] for p in parts if k < len(p)) for k in range(length)]

random.seed(5)
decay = lambda t: (1 - t) ** 3
# Two marbles meeting, a low knock with a little grit.
write("bump.wav", mix(tone(lambda t: 140 - 60 * t, 0.18, 0.7, decay), noise(0.06, 0.6, decay)))
# A dash, a rush of air rising and falling.
write("dash.wav", noise(0.3, 1.6, lambda t: math.sin(math.pi * t) ** 2))
# A marble going over the edge, a whistle falling away.
write("fall.wav", tone(lambda t: 900 - 650 * t, 0.7, 0.35, lambda t: 1 - t, ((1, 1),)))
# A point, a gong of partials that do not line up.
write("point.wav", tone(110, 1.6, 0.5, lambda t: (1 - t) ** 2, ((1, 1), (2.76, 0.5), (5.4, 0.25), (8.9, 0.12))))
# The count before a round, and its last beat higher.
write("tick.wav", tone(660, 0.12, 0.4, decay))
write("go.wav", tone(990, 0.3, 0.45, decay))
# A match won, three notes up and a long one.
write("win.wav", sum((tone(f, 0.16, 0.4, decay) for f in (523, 659, 784)), []) + tone(1047, 0.8, 0.4, lambda t: (1 - t) ** 2))
print("Sumo's sounds written to", here)

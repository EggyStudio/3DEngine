# Writes Tones's drums, synthesized, into resources, at half the rate the game plays at, which it
# converts them to as it reads them. Run again after changing one.
import math, os, random, struct, wave

here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "resources")
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

random.seed(41)
# A kick, a falling thump.
write("kick.wav", tone(lambda t: 120 * (1 - t) + 45, 0.35, 0.95, lambda t: (1 - t) ** 3, ((1, 1),)))
# A snare, a short tone under a burst of noise.
write("snare.wav", mix(tone(190, 0.12, 0.4, lambda t: (1 - t) ** 4, ((1, 1),)), noise(0.2, 1.4, lambda t: (1 - t) ** 3)))
# A hat, a short hiss, unsmoothed so it is bright.
write("hat.wav", [random.uniform(-1, 1) * 0.35 * (1 - k / (RATE * 0.06)) ** 4 for k in range(int(RATE * 0.06))])
print("Tones's drums written to", here)

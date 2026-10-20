# Writes Siege's sounds, synthesized, into resources/sounds. Run again after changing one.
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

random.seed(23)
decay = lambda t: (1 - t) ** 3
# The arm let go, a creak of the frame and a rush of air.
write("throw.wav", mix(tone(lambda t: 70 + 30 * t, 0.25, 0.4, decay, ((1, 1), (2.3, 0.4))), noise(0.45, 1.1, lambda t: math.sin(math.pi * t) ** 2)))
# A stone striking stone, a dull knock.
write("hit.wav", mix(tone(lambda t: 120 - 50 * t, 0.18, 0.7, decay, ((1, 1), (1.6, 0.5), (2.9, 0.2))), noise(0.06, 0.8, decay)))
# A weld giving way, a sharp crack.
write("crack.wav", mix(noise(0.07, 1.4, lambda t: (1 - t) ** 6), tone(lambda t: 1200 - 800 * t, 0.05, 0.3, decay)))
# A bomb bursting, a low boom and a long rush.
write("boom.wav", mix(tone(lambda t: 60 - 30 * t, 0.9, 0.9, lambda t: (1 - t) ** 2, ((1, 1), (1.5, 0.4))), noise(1.0, 1.6, lambda t: (1 - t) ** 2.5)))
# The next shot loaded, a click.
write("load.wav", mix(tone(1400, 0.03, 0.4, decay), noise(0.02, 0.5, decay)))
# The banner down, a fanfare.
write("win.wav", sum((tone(f, 0.16, 0.4, decay) for f in (392, 523, 659)), []) + tone(784, 0.9, 0.45, lambda t: (1 - t) ** 2))
print("Siege's sounds written to", here)

# Writes Pinball's sounds, synthesized, into resources/sounds. Run again after changing one.
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

random.seed(7)
decay = lambda t: (1 - t) ** 3
# A flipper coming up, a short clack of a coil.
write("flip.wav", mix(tone(lambda t: 220 - 120 * t, 0.07, 0.6, decay), noise(0.04, 0.7, decay)))
# A bumper's kick, a bright knock with a ring after it.
write("bumper.wav", mix(tone(lambda t: 520 - 180 * t, 0.22, 0.5, lambda t: (1 - t) ** 2, ((1, 1), (2.4, 0.35))), noise(0.03, 0.5, decay)))
# A sling's kick, shorter and lower than a bumper's.
write("sling.wav", mix(tone(lambda t: 300 - 100 * t, 0.1, 0.5, decay), noise(0.03, 0.4, decay)))
# The plunger let go, a spring's twang falling.
write("launch.wav", mix(tone(lambda t: 160 + 40 * math.sin(t * 40) - 60 * t, 0.35, 0.45, lambda t: (1 - t) ** 2), noise(0.08, 0.5, decay)))
# A ball lost, a low tone falling away.
write("drain.wav", tone(lambda t: 330 - 230 * t, 0.9, 0.4, lambda t: 1 - t, ((1, 1), (0.5, 0.4))))
# The table tilted, a buzz.
write("tilt.wav", tone(110, 0.8, 0.35, lambda t: 1 - t, ((1, 1), (3, 0.6), (5, 0.4), (7, 0.3))))
# A game begun, three notes up.
write("start.wav", sum((tone(f, 0.14, 0.4, decay) for f in (392, 523, 659)), []) + tone(784, 0.5, 0.4, lambda t: (1 - t) ** 2))
print("Pinball's sounds written to", here)

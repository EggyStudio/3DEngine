# Writes Tumble's sounds, synthesized, into resources/sounds. Run again after changing one.
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

random.seed(31)
decay = lambda t: (1 - t) ** 3
# A cannon firing, a low thump and a puff.
write("fire.wav", mix(tone(lambda t: 90 - 40 * t, 0.25, 0.8, decay), noise(0.3, 1.0, lambda t: (1 - t) ** 4)))
# The robot struck, a metal clang.
write("hit.wav", mix(tone(lambda t: 420 - 120 * t, 0.35, 0.5, decay, ((1, 1), (2.76, 0.6), (5.4, 0.3))), noise(0.05, 0.9, decay)))
# Up again, two notes rising.
write("up.wav", tone(392, 0.12, 0.4, decay) + tone(587, 0.3, 0.4, decay))
# A punch, a short rush of air.
write("punch.wav", noise(0.16, 1.2, lambda t: math.sin(math.pi * t) * (1 - t)))
# A star taken, a bright chime.
write("star.wav", mix(tone(1319, 0.5, 0.35, lambda t: (1 - t) ** 2), tone(1976, 0.4, 0.2, lambda t: (1 - t) ** 3)))
print("Tumble's sounds written to", here)

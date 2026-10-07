# Writes Jelly's sounds, synthesized, into resources/sounds. Run again after changing one.
import math, os, random, struct, wave

here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "resources", "sounds")
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
        out.append(volume * shape(t) * (math.sin(phase) + 0.25 * math.sin(2 * phase)))
    return out

def noise(seconds, volume, shape):
    n = int(RATE * seconds); last = 0; out = []
    for k in range(n):
        last = last * 0.6 + random.uniform(-1, 1) * 0.4
        out.append(last * volume * shape(k / n))
    return out

def mix(*parts):
    length = max(len(p) for p in parts)
    return [sum(p[k] for p in parts if k < len(p)) for k in range(length)]

random.seed(8)
decay = lambda t: (1 - t) ** 2
# A jump, a wobbling rise, and a landing, a soft wet thud.
write("jump.wav", tone(lambda t: 300 + 500 * t + 40 * math.sin(t * 60), 0.2, 0.35, decay))
write("land.wav", mix(tone(lambda t: 120 - 50 * t, 0.12, 0.5, decay), noise(0.05, 0.3, decay)))
# A coin, two bright notes.
write("coin.wav", tone(988, 0.07, 0.3, decay) + tone(1319, 0.2, 0.3, decay))
# A crash, a splat and a falling note.
write("crash.wav", mix(noise(0.35, 0.8, lambda t: (1 - t) ** 3), tone(lambda t: 400 - 300 * t, 0.6, 0.35, lambda t: 1 - t)))
print("Jelly's sounds written to", here)

# Writes Swarm's sounds, synthesized, into resources/sounds. Run again after changing one.
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
        out.append(volume * shape(t) * (math.sin(phase) + 0.3 * math.sin(2 * phase)))
    return out

def noise(seconds, volume, power):
    n = int(RATE * seconds)
    return [random.uniform(-1, 1) * volume * (1 - k / n) ** power for k in range(n)]

random.seed(11)
decay = lambda t: (1 - t) ** 2
write("shoot.wav", tone(lambda t: 1400 - 900 * t, 0.06, 0.25, decay))
write("hit.wav", [a + b for a, b in zip(noise(0.08, 0.35, 3), tone(220, 0.08, 0.3, decay))])
write("pop.wav", [a + b for a, b in zip(noise(0.22, 0.4, 2), tone(lambda t: 500 - 380 * t, 0.22, 0.4, decay))])
write("hurt.wav", tone(lambda t: 260 - 140 * t, 0.25, 0.5, lambda t: 1 - t))
write("wave.wav", tone(330, 0.18, 0.4, decay) + tone(440, 0.18, 0.4, decay) + tone(660, 0.45, 0.4, decay))
write("over.wav", sum((tone(f, 0.22, 0.45, decay) for f in (392, 330, 262)), []) + tone(196, 0.7, 0.45, decay))
print("Swarm's sounds written to", here)

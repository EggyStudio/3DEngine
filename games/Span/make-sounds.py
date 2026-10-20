# Writes Span's sounds, synthesized, into resources/sounds. Run again after changing one.
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

random.seed(11)
decay = lambda t: (1 - t) ** 3
# A plank laid, a knock of wood.
write("place.wav", mix(tone(lambda t: 180 - 40 * t, 0.12, 0.6, decay, ((1, 1), (2.7, 0.3))), noise(0.03, 0.4, decay)))
# A rope hung, a soft rising whip.
write("rope.wav", noise(0.25, 1.2, lambda t: math.sin(math.pi * t) ** 2 * (1 - t)))
# The load sent, two notes up.
write("go.wav", tone(523, 0.12, 0.4, decay) + tone(784, 0.25, 0.4, decay))
# Wood straining, a slow low warble.
write("creak.wav", tone(lambda t: 90 + 25 * math.sin(t * 30), 0.5, 0.35, lambda t: math.sin(math.pi * t), ((1, 1), (2, 0.5), (3.1, 0.3))))
# A joint snapping, a crack.
write("snap.wav", mix(noise(0.08, 1.4, lambda t: (1 - t) ** 6), tone(lambda t: 900 - 600 * t, 0.06, 0.4, decay)))
# Across, three notes up and a long one.
write("crossed.wav", sum((tone(f, 0.14, 0.4, decay) for f in (523, 659, 784)), []) + tone(1047, 0.7, 0.4, lambda t: (1 - t) ** 2))
# The load fallen, a whistle falling and a thud.
write("fell.wav", tone(lambda t: 800 - 600 * t, 0.6, 0.3, lambda t: 1 - t, ((1, 1),)) + tone(70, 0.3, 0.7, decay))
print("Span's sounds written to", here)

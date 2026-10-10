namespace Engine.Game;

/// <summary>Ken Perlin's improved gradient noise in two and three dimensions, from a seed.</summary>
/// <remarks>Read only once made, so the workers that generate columns share one.</remarks>
public sealed class Noise
{
    private readonly byte[] _permutation = new byte[512];

    public Noise(int seed)
    {
        var order = new byte[256];
        for (int i = 0; i < 256; i++) order[i] = (byte)i;
        new Random(seed).Shuffle(order);
        for (int i = 0; i < 512; i++) _permutation[i] = order[i & 255];
    }

    /// <summary>The noise at a point, from about -1 to 1.</summary>
    public float At(float x, float y, float z)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y), zi = (int)MathF.Floor(z);
        x -= xi;
        y -= yi;
        z -= zi;
        xi &= 255;
        yi &= 255;
        zi &= 255;
        float u = Fade(x), v = Fade(y), w = Fade(z);
        var p = _permutation;
        int a = p[xi] + yi, aa = p[a] + zi, ab = p[a + 1] + zi;
        int b = p[xi + 1] + yi, ba = p[b] + zi, bb = p[b + 1] + zi;
        return Lerp(w,
            Lerp(v, Lerp(u, Grad(p[aa], x, y, z), Grad(p[ba], x - 1, y, z)),
                Lerp(u, Grad(p[ab], x, y - 1, z), Grad(p[bb], x - 1, y - 1, z))),
            Lerp(v, Lerp(u, Grad(p[aa + 1], x, y, z - 1), Grad(p[ba + 1], x - 1, y, z - 1)),
                Lerp(u, Grad(p[ab + 1], x, y - 1, z - 1), Grad(p[bb + 1], x - 1, y - 1, z - 1))));
    }

    /// <summary>The noise on a plane, from about -1 to 1.</summary>
    public float At(float x, float z) => At(x, 0.5f, z);

    /// <summary>Octaves of the noise on a plane, each twice as fine and half as strong, from about -1 to 1.</summary>
    public float Fractal(float x, float z, int octaves)
    {
        float sum = 0, amplitude = 1, total = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += At(x, z) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            x *= 2;
            z *= 2;
        }
        return sum / total;
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);

    private static float Lerp(float t, float a, float b) => a + t * (b - a);

    private static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : h is 12 or 14 ? x : z;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }
}

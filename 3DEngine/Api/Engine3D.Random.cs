using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // -- Random values, from raylib's own generator, so a seed gives the values raylib gives for it.

    private static readonly object RandomGate = new();

    // xoshiro128**'s four words, as rprand.h, raylib's generator, starts them before any seed.
    private static readonly uint[] RandomState = [0x96ea83c1, 0x218b21e5, 0xaa91febd, 0x976414d4];

    /// <summary>
    /// Sets the seed of the generator <see cref="GetRandomValue"/>, <see cref="LoadRandomSequence"/>
    /// and the noise images draw from, so the same seed gives the same values in the same order.
    /// </summary>
    /// <remarks>
    /// The generator is raylib's, xoshiro128** started from the seed by SplitMix64, so a seed gives
    /// the values raylib gives for it. <see cref="InitWindow"/> seeds it from the clock, as raylib's
    /// does, or from <c>--seed N</c> or <c>E3D_SEED</c>, which <c>build/raylib-bench/compare.py</c>
    /// gives both programs of a pair, so a program that sets no seed is measured on raylib's values.
    /// </remarks>
    public static void SetRandomSeed(uint seed)
    {
        lock (RandomGate)
        {
            ulong state = seed;
            RandomState[0] = (uint)SplitMix64(ref state);
            RandomState[1] = (uint)(SplitMix64(ref state) >> 32);
            RandomState[2] = (uint)SplitMix64(ref state);
            RandomState[3] = (uint)(SplitMix64(ref state) >> 32);
        }
    }

    /// <summary>A random whole number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
    /// <remarks>The bounds may come in either order, as raylib takes them.</remarks>
    public static int GetRandomValue(int min, int max)
    {
        if (min > max) (min, max) = (max, min);
        lock (RandomGate) return NextRandom(min, max);
    }

    /// <summary>
    /// <paramref name="count"/> different whole numbers from <paramref name="min"/> to
    /// <paramref name="max"/>, both included, in random order, or an empty array when the range
    /// holds fewer than that.
    /// </summary>
    /// <remarks>
    /// Drawn as raylib draws them, drawing again where a value comes twice, so a seed gives
    /// raylib's sequence for bounds in order.
    /// </remarks>
    public static int[] LoadRandomSequence(int count, int min, int max)
    {
        if (min > max) (min, max) = (max, min);
        if (count <= 0 || count > (long)max - min + 1) return [];

        var values = new int[count];
        var seen = new HashSet<int>();
        lock (RandomGate)
            for (int i = 0; i < count;)
            {
                var value = NextRandom(min, max);
                if (seen.Add(value)) values[i++] = value;
            }
        return values;
    }

    // rprand's value in a range, the next word modulo the range's size, in the unsigned arithmetic
    // C reckons it in. The whole range of an int, whose size a word cannot hold, takes the word.
    private static int NextRandom(int min, int max)
    {
        var size = unchecked((uint)(max - min) + 1);
        var word = NextRandomWord();
        return unchecked((int)((size == 0 ? word : word % size) + (uint)min));
    }

    // xoshiro128**'s next word.
    private static uint NextRandomWord()
    {
        var s = RandomState;
        var result = unchecked(BitOperations.RotateLeft(s[1] * 5, 7) * 9);
        var t = s[1] << 9;
        s[2] ^= s[0];
        s[3] ^= s[1];
        s[1] ^= s[2];
        s[0] ^= s[3];
        s[2] ^= t;
        s[3] = BitOperations.RotateLeft(s[3], 11);
        return result;
    }

    private static ulong SplitMix64(ref ulong state)
    {
        unchecked
        {
            var z = state += 0x9e3779b97f4a7c15;
            z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9;
            z = (z ^ (z >> 27)) * 0x94d049bb133111eb;
            return z ^ (z >> 31);
        }
    }
}

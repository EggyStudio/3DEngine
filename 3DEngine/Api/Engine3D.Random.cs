namespace Engine;

public static partial class Engine3D
{
    // -- Random values, from one generator a seed can fix, so a run of a program can be repeated.

    private static readonly object RandomGate = new();
    private static Random _random = new();

    /// <summary>
    /// Sets the seed of the generator <see cref="GetRandomValue"/>, <see cref="LoadRandomSequence"/>
    /// and the noise images draw from, so the same seed gives the same values in the same order.
    /// </summary>
    public static void SetRandomSeed(uint seed)
    {
        lock (RandomGate) _random = new Random(unchecked((int)seed));
    }

    /// <summary>A random whole number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
    /// <remarks>The bounds may come in either order, as raylib takes them.</remarks>
    public static int GetRandomValue(int min, int max)
    {
        if (min > max) (min, max) = (max, min);
        lock (RandomGate) return (int)_random.NextInt64(min, (long)max + 1);
    }

    /// <summary>
    /// <paramref name="count"/> different whole numbers from <paramref name="min"/> to
    /// <paramref name="max"/>, both included, in random order, or an empty array when the range
    /// holds fewer than that.
    /// </summary>
    public static int[] LoadRandomSequence(int count, int min, int max)
    {
        if (min > max) (min, max) = (max, min);
        var range = (long)max - min + 1;
        if (count <= 0 || count > range) return [];

        // A shuffle of the range's first values, stopped after count, for a range small enough to
        // hold. A larger one draws until it has count different values.
        var values = new int[count];
        lock (RandomGate)
        {
            if (range <= Math.Max(count * 4L, 1024))
            {
                var all = new int[range];
                for (int i = 0; i < all.Length; i++) all[i] = min + i;
                for (int i = 0; i < count; i++)
                {
                    var j = (int)_random.NextInt64(i, range);
                    (all[i], all[j]) = (all[j], all[i]);
                }
                Array.Copy(all, values, count);
            }
            else
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < count;)
                {
                    var value = (int)_random.NextInt64(min, (long)max + 1);
                    if (seen.Add(value)) values[i++] = value;
                }
            }
        }
        return values;
    }

    // A float from 0 up to 1 and a whole number below a bound, for the noise images, from the seeded generator.
    private static float RandomSingle()
    {
        lock (RandomGate) return _random.NextSingle();
    }

    private static int RandomBelow(int bound)
    {
        lock (RandomGate) return _random.Next(bound);
    }
}

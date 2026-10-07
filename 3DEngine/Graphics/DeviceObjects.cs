namespace Engine;

/// <summary>
/// How many Vulkan objects of each kind the process holds, over every device it has made, counted
/// where each is made and where each is destroyed, which a test reads after apps have closed to
/// tell what outlives them from what a driver keeps of its own.
/// </summary>
internal static class DeviceObjects
{
    /// <summary>The kinds counted, those an app's passes make many of.</summary>
    internal enum Kind
    {
        Image,
        Buffer,
        Memory,
        Pipeline,
        DescriptorPool,
        DescriptorSet,
    }

    private static readonly long[] Counts = new long[Enum.GetValues<Kind>().Length];

    internal static void Made(Kind kind, long count = 1) => Interlocked.Add(ref Counts[(int)kind], count);

    internal static void Gone(Kind kind, long count = 1) => Interlocked.Add(ref Counts[(int)kind], -count);

    /// <summary>How many of each kind are alive now.</summary>
    internal static IReadOnlyDictionary<Kind, long> Now() =>
        Enum.GetValues<Kind>().ToDictionary(kind => kind, kind => Interlocked.Read(ref Counts[(int)kind]));

    /// <summary>The kinds of which more are alive than in <paramref name="before"/>, with how many more, or null where none grew.</summary>
    internal static string? GrownSince(IReadOnlyDictionary<Kind, long> before)
    {
        var grown = Now().Where(entry => entry.Value > before.GetValueOrDefault(entry.Key))
            .Select(entry => $"{entry.Value - before.GetValueOrDefault(entry.Key)} {entry.Key}").ToArray();
        return grown.Length == 0 ? null : string.Join(", ", grown);
    }
}

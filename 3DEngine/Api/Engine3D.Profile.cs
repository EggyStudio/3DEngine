namespace Engine;

public static partial class Engine3D
{
    /// <summary>Sets a number of the program's own in the frame profile, which <c>e3d command profile</c> reports, as a stress test's count.</summary>
    public static void SetProfileValue(string name, double value)
    {
        if (World.TryGetResource<FrameProfile>(out var profile)) profile.Set(name, value);
    }

    /// <summary>A measured average from the frame profile in milliseconds, as <c>"work"</c>, <c>"frame"</c> or <c>"gpu.models"</c>, or 0 before it was measured.</summary>
    public static double GetProfileAverage(string name) =>
        World.TryGetResource<FrameProfile>(out var profile) ? profile.Average(name) : 0;

    private static void Profile(string name, TimeSpan elapsed)
    {
        if (World.TryGetResource<FrameProfile>(out var profile)) profile.Add(name, elapsed.TotalMilliseconds);
    }
}

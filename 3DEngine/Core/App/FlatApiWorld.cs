namespace Engine;

/// <summary>
/// The world the flat API runs against, kept by the flat API wherever it takes an app or lets one go,
/// which the scene spawner asks before it hands a model's reference to an animated model the flat
/// API's systems draw.
/// </summary>
internal static class FlatApiWorld
{
    /// <summary>The world of the app the flat API runs against, or null where it runs against none.</summary>
    internal static World? Current { get; set; }

    /// <summary>Whether the flat API runs against <paramref name="world"/>.</summary>
    internal static bool Holds(World world) => Current is { } held && ReferenceEquals(held, world);
}

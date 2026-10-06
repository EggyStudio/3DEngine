using System.Text.Json;

namespace Engine.Tests.Scenes;

/// <summary>
/// Summit's own components, which its level names, registered by the tests that read the level so
/// it is read whole as the game reads it, where without them its orbs, its start and its exit are
/// skipped with a warning each.
/// </summary>
public static class SummitComponents
{
    /// <summary>An orb to collect, a trigger in the level.</summary>
    public struct Orb;

    /// <summary>The trigger in the house that ends the run once every orb is collected.</summary>
    public struct Exit;

    /// <summary>Where the player starts, on the island.</summary>
    public struct Start;

    /// <summary>Registers the three, which hold no fields, under the names the level gives them.</summary>
    public static void Register()
    {
        Add<Orb>("Orb");
        Add<Exit>("Exit");
        Add<Start>("Start");
    }

    private static void Add<T>(string name) where T : struct =>
        SceneComponents.Add(new SceneCodec<T>(name, static (Utf8JsonWriter _, in T _, SceneWriteContext _) => { }, static (_, _) => default));
}

using System.Globalization;
using System.Numerics;
using Engine;

namespace FeatureTest;

/// <summary>
/// The feature test's console commands, which ./e3d sends as it sends any game's. They run between
/// frames, so each read gives what the last frame wrote and each request is carried out at the next.
/// </summary>
public static class FeatureCommands
{
    internal static readonly Dictionary<string, string> Statuses = new(StringComparer.OrdinalIgnoreCase);
    internal static string Where = "";

    // What was asked for and not yet done, the zone and spot to go to, a place to be put at, or
    // flight turned on or off.
    internal static (Zone Zone, string? Spot)? Go;
    internal static (Vector3 Feet, float Facing)? Put;
    internal static bool? Fly;

    [Command("feature.status", "Each zone's status, a line each, or one zone's: feature.status [zone]")]
    internal static string Status(string zone = "")
    {
        if (zone.Length == 0) return string.Join("\n", Statuses.Select(pair => $"{pair.Key}: {pair.Value}"));
        return Zones.Named(zone) is { } found && Statuses.TryGetValue(found.Name, out var line)
            ? $"{found.Name}: {line}"
            : $"there is no zone called {zone}, only {string.Join(", ", Zones.All.Select(z => z.Name))}";
    }

    [Command("feature.zone", "Puts the player at a zone's start facing its middle, or at one of its spots, the station tried afresh: feature.zone <zone> [spot]")]
    internal static string Teleport(string zone, string spot = "")
    {
        if (Zones.Named(zone) is not { } found) return $"there is no zone called {zone}, only {string.Join(", ", Zones.All.Select(z => z.Name))}";
        if (spot.Length > 0 && !found.Spots.ContainsKey(spot))
            return found.Spots.Count == 0 ? $"{found.Name} has no spots" : $"{found.Name} has no spot called {spot}, only {string.Join(", ", found.Spots.Keys)}";
        Go = (found, spot.Length > 0 ? spot : null);
        return spot.Length > 0 ? $"to {found.Name}, at its {spot}" : $"to {found.Name}";
    }

    [Command("player", "Where the player's feet are, whether it is grounded, in the air or flying, whether it crouches, the zone it is in and the bearing it faces")]
    internal static string Player() => Where;

    [Command("player.put", "Puts the player's feet at a point at rest, facing a bearing in degrees from north, 90 east: player.put <x> <y> <z> [facing]")]
    internal static string PutAt(float x, float y, float z, float facing = 0)
    {
        Put = (new Vector3(x, y, z), facing);
        return string.Create(CultureInfo.InvariantCulture, $"to {x} {y} {z}, facing {facing}");
    }

    [Command("player.fly", "Turns flying on or off: player.fly <on|off>")]
    internal static string Flight(string state)
    {
        if (state is not ("on" or "off")) return $"flying is on or off, not {state}";
        Fly = state == "on";
        return $"flying {state}";
    }

    /// <summary>Carries out what was asked since the last frame.</summary>
    internal static void Apply(Player player)
    {
        if (Go is var (zone, spot))
        {
            zone.Arrive(spot);
            var (feet, facing) = spot is null ? (zone.Start, zone.Facing) : zone.Spots[spot];
            player.Put(feet, facing);
            Go = null;
        }
        if (Put is var (at, bearing))
        {
            player.Put(at, bearing);
            Put = null;
        }
        if (Fly is { } on)
        {
            player.Fly(on);
            Fly = null;
        }
    }

    /// <summary>Writes what the frame found, for the commands to read until the next.</summary>
    internal static void Publish(Player player, Zone? here)
    {
        foreach (var zone in Zones.All) Statuses[zone.Name] = zone.Status();
        Where = player.Line(here);
    }
}

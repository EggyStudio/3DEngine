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
    internal static string Where = "", Costs = "";
    internal static int Frames;
    internal static Panel? Admin;

    // What was asked for and not yet done, the zone and spot to go to, a place to be put at, or
    // flight turned on or off.
    internal static (Zone Zone, string? Spot)? Go;
    internal static (Vector3 Feet, float Facing)? Put;
    internal static bool? Fly;
    internal static bool CrashAsked;

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

    [Command("feature.toggles", "Every feature the panel turns on and off, by group, the engine's and each zone's")]
    internal static string Toggles() =>
        string.Join("\n", Groups().Select(group => $"{group.Name}: {string.Join(", ", group.Toggles)}"));

    [Command("feature.toggle", "Turns a feature on or off, as the panel does: feature.toggle <engine|zone> <feature> <on|off>")]
    internal static string Switch(string group, string feature, string state)
    {
        if (state is not ("on" or "off")) return $"a feature is on or off, not {state}";
        if (Groups().FirstOrDefault(g => g.Name.Equals(group, StringComparison.OrdinalIgnoreCase)) is not { Name: not null } found)
            return $"there is no group called {group}, only {string.Join(", ", Groups().Select(g => g.Name))}";
        if (found.Toggles.FirstOrDefault(t => t.Key.Equals(feature, StringComparison.OrdinalIgnoreCase)) is not { } toggle)
            return $"{found.Name} has no feature called {feature}, only {string.Join(", ", found.Toggles.Select(t => t.Key))}";
        toggle.Set(state == "on");
        return $"{found.Name}: {toggle}";
    }

    [Command("feature.panel", "Opens or closes the admin panel, as F1 does: feature.panel <on|off>")]
    internal static string ShowPanel(string state)
    {
        if (state is not ("on" or "off") || Admin is null) return $"the panel is on or off, not {state}";
        Admin.Open = state == "on";
        return $"the panel {(Admin.Open ? "open" : "closed")}";
    }

    [Command("feature.costs", "The frame's costs the panel shows, each averaged by the profile, and whether the panel is open and how many frames it was drawn in")]
    internal static string Cost() => Costs;

    [Command("feature.crash", "Throws an exception nothing catches at the next frame, which ends the program and writes its crash file beside it")]
    internal static string Fail()
    {
        CrashAsked = true;
        return "throwing at the next frame";
    }

    // The engine's switches and each zone's toggles, by their group's name.
    private static IEnumerable<(string Name, IReadOnlyList<Toggle> Toggles)> Groups() =>
        Zones.All.Select(zone => (zone.Name, zone.Toggles)).Prepend(("Engine", Admin?.Engine ?? []));

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
        Frames++;
        Costs = $"{Panel.Costs()}, the panel {(Admin?.Open == true ? "open" : "closed")} and drawn in {Admin?.Drawn ?? 0} frames";
    }
}

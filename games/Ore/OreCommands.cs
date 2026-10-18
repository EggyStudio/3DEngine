using System.Numerics;
using Engine;

/// <summary>What <c>./e3d</c> asks of Ore, and what it sets for the next frame to take up.</summary>
public static class OreCommands
{
    internal static string Status = "";
    internal static bool Autopilot, Night;
    // Where to put the player, x and z, and the way to face in degrees from north in the middle,
    // which is NaN where none was given.
    internal static Vector3? Warp;

    [Command("ore.status", "The screen, where the player's feet are, x, y and z, whether they stand on ground, the jumps and how high the last rose, the light's quality and whether it is day or night")]
    internal static string Report() => Status;

    [Command("ore.autopilot", "Walks from point to point round the landscape, jumping up each block it meets: ore.autopilot <on>")]
    internal static string Pilot(bool on)
    {
        Autopilot = on;
        return on ? "the autopilot walks" : "the player walks";
    }

    [Command("ore.warp", "Puts the player on top of the column at x and z, facing a way in degrees from north when given, starting play from the title: ore.warp <x> <z> [facing]")]
    internal static string Move(float x, float z, float facing = float.NaN)
    {
        Warp = new Vector3(x, facing, z);
        return $"the player stands on the column at {x} {z}";
    }

    [Command("ore.night", "Night, the sun down and the ore the only light, or day: ore.night <on>")]
    internal static string Dark(bool on)
    {
        Night = on;
        return on ? "night, the ore the only light" : "day, under the sun";
    }
}

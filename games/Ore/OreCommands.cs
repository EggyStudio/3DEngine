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
    // The turn asked, in radians a frame, to the right where above 0, and the frames left of it.
    internal static (float Radians, int Frames) Turning;
    // The walk asked, in units a frame the way the player faces, and the frames left of it.
    internal static (float Step, int Frames) Walking;

    [Command("ore.status", "The screen, where the player's feet are, x, y and z, whether they stand on ground, the jumps and how high the last rose, the light's quality, whether it is day or night, and the camera's eye and the point it looks at, each x,y,z")]
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

    [Command("ore.turn", "Turns the player about where they stand by an angle in degrees a frame, to the right where above 0, for a count of frames, as build/flicker.py's --turn reads a recording: ore.turn <degrees> <frames>")]
    internal static string Turn(float degrees, int frames)
    {
        Turning = (float.DegreesToRadians(degrees), Math.Max(0, frames));
        return $"turning {degrees} degrees a frame for {Turning.Frames} frames";
    }

    [Command("ore.walk", "Moves the player the way they face by a step in units a frame for a count of frames, standing on each column it comes to, the same each run, as two runs are read against each other: ore.walk <step> <frames>")]
    internal static string Walk(float step, int frames)
    {
        Walking = (step, Math.Max(0, frames));
        return $"walking {step} units a frame for {Walking.Frames} frames";
    }

    [Command("ore.night", "Night, the sun down and the ore the only light, or day: ore.night <on>")]
    internal static string Dark(bool on)
    {
        Night = on;
        return on ? "night, the ore the only light" : "day, under the sun";
    }
}

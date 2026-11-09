using System.Globalization;

namespace Engine;

/// <summary>The command that reads and sets the hour of the day sky a program runs, so a script takes its pictures at the hours it names.</summary>
internal static class SkyCommands
{
    [Command("sky.time", "Sets the hour of the day sky the program runs, in hours from its first midnight, or with nothing says where it stands, answering the time, the hour of the day, the day, the sun's height in degrees and the moon's phase: sky.time [<hours>]")]
    internal static string Time(string line)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hours = 0f;
        if (words.Length > 1 || words.Length == 1 && !(float.TryParse(words[0], NumberStyles.Float, CultureInfo.InvariantCulture, out hours) && float.IsFinite(hours)))
            return Refuse("BAD_ARGUMENT", "sky.time takes a number of hours from the day sky's first midnight, or nothing to read it.");
        if (!ConsoleHost.World!.TryGetResource<DaySky>(out var sky))
            return Refuse("NO_DAY_SKY", "The program runs no day sky, which SetSkyTime or SetSkyDay starts.");

        if (words.Length == 1)
        {
            sky.Hours = hours;
            sky.Changed();
        }
        return Say(sky);
    }

    /// <summary>Where a day sky stands, as the command answers it, a line of names and numbers a script splits.</summary>
    internal static string Say(DaySky sky)
    {
        // Rounded and added to 0, so a sun on the horizon reads as 0.0 rather than -0.0.
        var height = MathF.Round(float.RadiansToDegrees(MathF.Asin(Math.Clamp(DaySky.SunToward(sky.Hour, sky.Day.Tilt).Y, -1, 1))), 1) + 0f;
        return string.Create(CultureInfo.InvariantCulture,
            $"time {sky.Hours:0.00} hour {sky.Hour:0.00} day {sky.Days} sun {height:0.0} moon {DaySky.MoonPhase(sky.Days, sky.Hour)}");
    }

    private static string Refuse(string code, string message)
    {
        ConsoleHost.Fail(code, message);
        return message;
    }
}

using System.Text;

namespace Engine;

/// <summary>The command that says whether the program is live and which files it watches, and turns it on or off.</summary>
internal static class LiveCommands
{
    [Command("live", "Whether the program loads again the files it loaded by name as they are saved, each file it watches a line with what it was loaded into and its last reload, or turns that on or off: live [on|off]")]
    internal static string Live(string state = "")
    {
        var world = ConsoleHost.World!;
        if (!world.TryGetResource<LiveFiles>(out var live))
            return Refuse("NO_FLAT_API", "The program runs no flat API, whose loads by name the watch follows.");
        if (state.Length > 0)
        {
            if (state is not ("on" or "off")) return Refuse("BAD_ARGUMENT", "live takes on or off, or nothing to read it.");
            var on = state == "on";
            live.Turn(on);
            if (world.TryGetResource<AssetServer>(out var server))
            {
                if (on) server.EnableWatching();
                else server.DisableWatching();
            }
        }
        return Say(live, world);
    }

    /// <summary>
    /// Whether the program is live and the asset server watching, then a line for each file, its
    /// kind, its id and its path, and where it was loaded again, how many times, the last frame and
    /// whether that reload failed and why.
    /// </summary>
    internal static string Say(LiveFiles live, World world)
    {
        var text = new StringBuilder(live.On ? "live" : "not live");
        if (world.TryGetResource<AssetServer>(out var server))
            text.Append(server.WatchForChanges ? ", the asset server watching" : ", the asset server not watching");
        foreach (var entry in live.Entries)
        {
            text.Append($"\n{entry.Kind} {entry.Id} {entry.Path}");
            if (entry.LastFrame is { } frame)
                text.Append($" reloads {entry.Reloads} frame {frame} ").Append(entry.LastError is { } error ? "failed " + error : "ok");
        }
        return text.ToString();
    }

    private static string Refuse(string code, string message)
    {
        ConsoleHost.Fail(code, message);
        return message;
    }
}

using System.Globalization;
using System.Text;

namespace Engine;

/// <summary>
/// The command that lists the engine's caches in the user's cache folder and clears them, so a
/// person who wonders whether an old cache keeps a picture old can see and empty it.
/// </summary>
/// <remarks>
/// Neither cache can serve an old shader: the compiled shaders are kept by the hash of their source,
/// their entry point, their stage and the compiler's version, and the driver checks the pipelines'
/// file against its own version and the GPU. What they do is grow, every version of a shader a
/// program compiled kept beside the rest under the engine's version, which a build in development
/// does not change.
/// </remarks>
internal static class CacheCommands
{
    [Command("cache", "Lists the engine's caches in the user's cache folder, the compiled shaders and the driver's pipelines by the engine's version, the pipelines by program, and the scene packs, each with its size, or clears them: with clear alone the shaders and the pipelines, which a run compiles again by itself, or one of them named, and with clear scenes the fetched scene packs, which ./e3d scenes fetch downloads again: cache [clear [shaders|pipelines|scenes]]")]
    internal static string Cache(string line)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return List();
        if (words[0] != "clear" || words.Length > 2 || words.Length == 2 && words[1] is not ("shaders" or "pipelines" or "scenes"))
            return Refuse("BAD_ARGUMENT", "cache takes nothing to list the caches, or clear alone for the shaders and the pipelines, or clear and one of shaders, pipelines or scenes.");
        // The scene packs are downloads, cleared only where named, so no clear of what the machine
        // rebuilds by itself takes a pack that must be fetched again.
        var part = words.Length == 2 ? words[1] : "";
        var cleared = new List<string>();
        if (part == "")
            cleared.Add("what a run compiles again by itself:");
        if (part is "" or "shaders")
            cleared.Add(Clear(Path.Combine(UserCache.Folder, "shaders"), "the compiled shaders"));
        if (part is "" or "pipelines")
        {
            // The running program's device holds the pipelines it read and would write them back
            // when it is let go, so it is told to write none this run.
            if (ConsoleHost.World is { } world && world.TryGetResource<Renderer>(out var renderer) && renderer.Context.Graphics is GraphicsDevice device)
                device.DropPipelineCache();
            cleared.Add(Clear(Path.Combine(UserCache.Folder, "pipelines"), "the driver's pipelines"));
        }
        if (part == "scenes")
            cleared.Add(Clear(Path.Combine(UserCache.Folder, "scenes"), "the fetched scene packs") + ", which ./e3d scenes fetch downloads again");
        return string.Join("\n", cleared);
    }

    // Each cache folder under the engine's, a line a version or a program, with its size and files.
    private static string List()
    {
        var text = new StringBuilder($"the engine's caches in {UserCache.Folder}, this engine's version {UserCache.Version}");
        foreach (var (kind, named) in new[] { ("shaders", "compiled shaders"), ("pipelines", "driver's pipelines") })
        {
            var folder = Path.Combine(UserCache.Folder, kind);
            if (!Directory.Exists(folder))
            {
                text.Append($"\n{named}: none");
                continue;
            }
            foreach (var version in Directory.GetDirectories(folder).Order(StringComparer.Ordinal))
            {
                text.Append($"\n{named}, version {Path.GetFileName(version)}: {Size(version)}");
                if (kind == "pipelines")
                    foreach (var file in Directory.GetFiles(version, "*.bin").Order(StringComparer.Ordinal))
                        text.Append($"\n  {Path.GetFileNameWithoutExtension(file)}: {Megabytes(new FileInfo(file).Length)}");
            }
        }
        var scenes = ScenePacks.Folder;
        if (!Directory.Exists(scenes))
            text.Append("\nscene packs: none");
        else
            foreach (var pack in Directory.GetDirectories(scenes).Order(StringComparer.Ordinal))
                text.Append($"\nscene pack {Path.GetFileName(pack)}: {Size(pack)}");
        return text.ToString();
    }

    // Deletes a cache folder and says what it held.
    private static string Clear(string folder, string named)
    {
        if (!Directory.Exists(folder))
            return $"{named}: none to clear";
        var held = Size(folder);
        try
        {
            Directory.Delete(folder, recursive: true);
            return $"{named} cleared, {held}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"{named} not all cleared, {ex.Message}";
        }
    }

    private static string Size(string folder)
    {
        var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
        return $"{Megabytes(files.Sum(file => new FileInfo(file).Length))} in {files.Length} files";
    }

    private static string Megabytes(long bytes) => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1048576.0:0.0} MB");

    private static string Refuse(string code, string message)
    {
        ConsoleHost.Fail(code, message);
        return message;
    }
}

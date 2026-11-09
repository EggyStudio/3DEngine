namespace Engine;

/// <summary>The console commands that save the world's entities to a scene file and spawn them from one.</summary>
internal static class SceneCommands
{
    [Command("scene.save", "Writes every entity not spawned from a model to a scene file: scene.save <path>")]
    internal static string SceneSave(string path)
    {
        var full = Path.GetFullPath(path);
        SceneFile.Save(ConsoleHost.Ecs, full);
        return $"saved {full}";
    }

    [Command("scene.load", "Spawns the entities of a scene file beside what is there: scene.load <path>")]
    internal static string SceneLoad(string path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
        {
            ConsoleHost.Fail("NOT_FOUND", $"There is no file '{full}'.");
            return "not loaded";
        }
        try
        {
            return $"spawned {SceneFile.Load(ConsoleHost.World!, full).Count} entities";
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
        {
            ConsoleHost.Fail("BAD_FILE", ex.Message);
            return "not loaded";
        }
    }
}

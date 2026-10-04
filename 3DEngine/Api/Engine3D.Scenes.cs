namespace Engine;

public static partial class Engine3D
{
    /// <summary>
    /// Loads a scene file into the ECS, found as models and sounds are, beside the program or in the
    /// working directory, and returns the entities it spawned.
    /// </summary>
    /// <remarks>
    /// A scene file is JSON of entities and their components by name (ARCHITECTURE.md). A program's
    /// own component types are written and read when they are marked <c>[SceneComponent]</c>. The
    /// entities come back as handles, since a level is kept for as long as it is played, and a
    /// handle to an entity despawned since is refused rather than reaching one that reused its id.
    /// </remarks>
    /// <returns>The entities, or none when the file is missing or not a scene file, with the reason in the log.</returns>
    public static IReadOnlyList<Entity> LoadScene(string fileName)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadScene: '{fileName}' was not found beside the program or in the working directory.");
            return [];
        }

        try
        {
            var spawned = SceneFile.Load(World, path);
            // The bodies its Colliders and RigidBodies describe, at once rather than next frame.
            PhysicsBodies.Run(World);
            var ecs = Res<EcsWorld>();
            return spawned.Select(ecs.Handle).ToArray();
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or IOException)
        {
            ApiLogger.Warn($"LoadScene: '{fileName}' could not be read: {ex.Message}");
            return [];
        }
    }

    /// <summary>Saves every entity of the ECS that a scene did not spawn, or only <paramref name="entities"/>, to a scene file.</summary>
    /// <remarks>A handle to an entity despawned since it was taken is left out.</remarks>
    public static void SaveScene(string fileName, IEnumerable<Entity>? entities = null)
    {
        var ecs = Res<EcsWorld>();
        SceneFile.Save(ecs, fileName, entities?.Select(e => ecs.TryResolve(e, out var id) ? id : 0).Where(id => id != 0));
    }
}

namespace Engine;

/// <summary>A model file read by the reader its extension names, which the flat API's models and animations and the edit meshes' import take.</summary>
internal static class ModelFiles
{
    // The flat API's category, which these lines were written under before they moved here.
    private static readonly ILogger Logger = Log.Category("Engine.Api");

    // A model file as Assimp reads it, or null with the reason logged. MagicaVoxel's files,
    // Inter-Quake Models and Model 3D files are read here, as raylib reads them itself, where
    // Assimp reads an IQM's mesh without its skeleton and the Assimp carried reads no M3D.
    internal static Scene? ReadScene(string fileName, string caller) =>
        Read(fileName, caller, (data, path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".vox" => VoxModelReader.Read(data, Path.GetFileNameWithoutExtension(path)),
            ".iqm" => IqmModelReader.Read(data, Path.GetFileNameWithoutExtension(path)),
            ".m3d" => M3dModelReader.Read(data, Path.GetFileNameWithoutExtension(path)),
            _ => new AssimpModelReader().ReadFile(path, new SceneImportSettings()),
        });

    internal delegate T Reader<T>(ReadOnlySpan<byte> data, string path);

    // What a reader makes of a model file found beside the program or in the working directory, or
    // null with the reason logged.
    internal static T? Read<T>(string fileName, string caller, Reader<T> read) where T : class
    {
        var path = ProgramFiles.Resolve(fileName);
        if (path is null)
        {
            Logger.Warn($"{caller}: '{fileName}' was not found beside the program or in the working directory.");
            return null;
        }

        try
        {
            // Assimp reads the file itself, so only the readers of this engine's own are given its bytes.
            var data = Path.GetExtension(path).ToLowerInvariant() is ".vox" or ".iqm" or ".m3d" ? File.ReadAllBytes(path) : [];
            return read(data, path);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Assimp.AssimpException)
        {
            Logger.Warn($"{caller}: '{fileName}' could not be read. {ex.Message}");
            return null;
        }
    }
}

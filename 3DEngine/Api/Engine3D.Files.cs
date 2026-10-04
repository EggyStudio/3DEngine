namespace Engine;

public static partial class Engine3D
{
    // -- Files, as raylib's, for saves and settings

    /// <summary>The folder the program runs from, ending in a separator, where its resources are.</summary>
    public static string GetApplicationDirectory() =>
        AppContext.BaseDirectory.EndsWith(Path.DirectorySeparatorChar) ? AppContext.BaseDirectory : AppContext.BaseDirectory + Path.DirectorySeparatorChar;

    /// <summary>Whether a file exists, beside the program or in the working directory.</summary>
    public static bool FileExists(string fileName) => ResolveFile(fileName) is not null;

    /// <summary>A text file's contents, found beside the program or in the working directory, or null when there is none.</summary>
    public static string? LoadFileText(string fileName)
    {
        var path = ResolveFile(fileName);
        try
        {
            return path is null ? null : File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            ApiLogger.Warn($"LoadFileText: '{fileName}' could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Writes text to a file, replacing what it held, making its folder when it has none. A relative
    /// name is beside the program, where <see cref="LoadFileText"/> finds it again.
    /// </summary>
    /// <returns>Whether it was written, with the reason in the log when not.</returns>
    public static bool SaveFileText(string fileName, string text)
    {
        var path = Path.IsPathRooted(fileName) ? fileName : Path.Combine(AppContext.BaseDirectory, fileName);
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
            File.WriteAllText(path, text);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ApiLogger.Warn($"SaveFileText: '{fileName}' could not be written: {ex.Message}");
            return false;
        }
    }

    /// <summary>Whether files have been dropped on the window since the program last unloaded them.</summary>
    public static bool IsFileDropped() => TryRes<Input>(out var input) && input.DroppedFiles.Count > 0;

    /// <summary>The paths of the files dropped on the window, in the order they arrived.</summary>
    /// <remarks>They are kept until <see cref="UnloadDroppedFiles"/>, so a drop is not lost to a frame that did not ask.</remarks>
    public static string[] LoadDroppedFiles() => TryRes<Input>(out var input) ? [.. input.DroppedFiles] : [];

    /// <summary>Forgets the dropped files, so <see cref="IsFileDropped"/> answers for the next drop.</summary>
    public static void UnloadDroppedFiles()
    {
        if (TryRes<Input>(out var input)) input.ClearDroppedFiles();
    }
}

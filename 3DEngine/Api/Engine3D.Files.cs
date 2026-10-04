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

    /// <summary>A file's bytes, found beside the program or in the working directory, or null when there is none.</summary>
    public static byte[]? LoadFileData(string fileName)
    {
        var path = ResolveFile(fileName);
        try
        {
            return path is null ? null : File.ReadAllBytes(path);
        }
        catch (IOException ex)
        {
            ApiLogger.Warn($"LoadFileData: '{fileName}' could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Writes bytes to a file, replacing what it held, making its folder when it has none. A
    /// relative name is beside the program, where <see cref="LoadFileData"/> finds it again.
    /// </summary>
    /// <returns>Whether it was written, with the reason in the log when not.</returns>
    public static bool SaveFileData(string fileName, ReadOnlySpan<byte> data)
    {
        var path = Path.IsPathRooted(fileName) ? fileName : Path.Combine(AppContext.BaseDirectory, fileName);
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
            using var file = File.Create(path);
            file.Write(data);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ApiLogger.Warn($"SaveFileData: '{fileName}' could not be written: {ex.Message}");
            return false;
        }
    }

    private static readonly ILogger ProgramLogger = Log.Category("Program");

    /// <summary>Writes a line to the engine's log, under the category <c>Program</c>, as raylib's <c>TraceLog</c> does.</summary>
    /// <remarks>The line reaches the log file at every level, and the console at <see cref="SetTraceLogLevel"/>'s level and above.</remarks>
    public static void TraceLog(LogLevel level, string text) => ProgramLogger.Log(level, text);

    /// <summary>
    /// Hands every line that reaches the console's level to <paramref name="callback"/> as well,
    /// with its level, as a game showing the log in its own window does, or stops with null.
    /// </summary>
    public static void SetTraceLogCallback(Action<LogLevel, string>? callback) => LogConfig.Callback = callback;

    /// <summary>Sets the least level a line needs to reach the console, Info to begin with. The log file keeps every line.</summary>
    public static void SetTraceLogLevel(LogLevel level) => LogConfig.ConsoleMinimumLevel = level;

    /// <summary>Opens a web address in the desktop's browser, as a credits screen's link does.</summary>
    /// <remarks>Only <c>http</c> and <c>https</c> addresses are opened, so a string from a save or a server cannot start a program.</remarks>
    public static void OpenURL(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            ApiLogger.Warn($"OpenURL: '{url}' is not an http or https address, so it is not opened.");
            return;
        }
        if (!SDL3.SDL.OpenURL(uri.AbsoluteUri)) ApiLogger.Warn($"OpenURL: '{url}' could not be opened: {SDL3.SDL.GetError()}");
    }

    /// <summary>Holds the program for a number of seconds, as a loading screen's pause does.</summary>
    public static void WaitTime(double seconds)
    {
        if (seconds > 0) Thread.Sleep(TimeSpan.FromSeconds(seconds));
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

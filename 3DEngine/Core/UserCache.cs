using System.Reflection;

namespace Engine;

/// <summary>
/// The engine's folder in the user's cache folder, where what one run makes for the next is kept
/// apart from the program's own folder, which a build cleans and a program deleted and built again
/// starts without, as the scene packs are and the compiled shaders and pipelines.
/// </summary>
internal static class UserCache
{
    private static string? _folder;

    /// <summary>
    /// <c>3DEngine</c> in the user's cache folder, <c>XDG_CACHE_HOME</c> or <c>~/.cache</c> on
    /// Linux, <c>~/Library/Caches</c> on macOS and the local application data on Windows, unless
    /// this is set, as a test sets it to a folder of its own. Set to <see langword="null"/> to go
    /// back to the default.
    /// </summary>
    public static string Folder
    {
        get => _folder ?? Path.Combine(OwnFolder(), "3DEngine");
        set => _folder = value;
    }

    /// <summary>
    /// The engine's version, without the commit a build of it names after a plus, as <c>5.2.326</c>
    /// or <c>0.1.0-preview.1</c>, which what a cache keeps for one version of the engine is kept
    /// under, so a program built again from the same engine finds it.
    /// </summary>
    public static string Version { get; } =
        typeof(UserCache).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";

    /// <summary>The folder of <paramref name="kind"/> for this version of the engine, as <c>shaders/5.2.326</c>.</summary>
    public static string Versioned(string kind) => Path.Combine(Folder, kind, Version);

    /// <summary>
    /// Deletes the folders of <paramref name="kind"/> kept for versions of the engine other than
    /// this one whose newest file no run wrote or read for <paramref name="after"/>, and those that
    /// hold no file, since each package of the engine keeps its own and a program run on one
    /// package after another left the folders of every one before it.
    /// </summary>
    internal static void SweepVersions(string kind, TimeSpan after)
    {
        var folder = Path.Combine(Folder, kind);
        if (!Directory.Exists(folder)) return;
        var before = DateTime.UtcNow - after;
        foreach (var version in Directory.EnumerateDirectories(folder).ToArray())
            try
            {
                if (Path.GetFileName(version) == Version) continue;
                var files = Directory.EnumerateFiles(version, "*", SearchOption.AllDirectories).ToArray();
                if (files.Length == 0 || files.Max(File.GetLastWriteTimeUtc) < before)
                    Directory.Delete(version, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
    }

    private static string OwnFolder()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
        if (OperatingSystem.IsMacOS()) return Path.Combine(home, "Library", "Caches");
        return Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } cache ? cache : Path.Combine(home, ".cache");
    }
}

namespace Engine;

/// <summary>Where a program's files are found, which the flat API, the data assets and the loaders read them by.</summary>
internal static class ProgramFiles
{
    // Finds a file the way a program run from anywhere expects, as given (relative to the working
    // directory), then beside the program, then under source/ beside it, where content items land.
    internal static string? Resolve(string fileName)
    {
        if (File.Exists(fileName)) return fileName;
        foreach (var root in new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "source") })
        {
            var candidate = Path.Combine(root, fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}

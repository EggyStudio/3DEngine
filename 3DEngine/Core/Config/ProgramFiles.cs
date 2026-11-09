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

    /// <summary>
    /// The folder of the project the program was built from, where it runs from that project's
    /// <c>bin/&lt;configuration&gt;/&lt;framework&gt;</c> and a project file there is the program's own,
    /// by its file name or the assembly name it sets, as a game in development runs, or null for a
    /// program run from anywhere else, as a shipped one is.
    /// </summary>
    /// <remarks>
    /// The project file is matched to the program by name, so a test host run from a test project's
    /// build folder, whose project builds no program of that name, is not taken for one.
    /// </remarks>
    internal static string? OwnProject(string programFolder, string? program)
    {
        if (string.IsNullOrEmpty(program)) return null;
        var project = Path.GetFullPath(Path.Combine(programFolder, "..", "..", ".."));
        if (!Directory.Exists(project)) return null;
        foreach (var file in Directory.EnumerateFiles(project, "*.csproj"))
        {
            if (Path.GetFileNameWithoutExtension(file).Equals(program, StringComparison.OrdinalIgnoreCase)) return project;
            var named = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(file), "<AssemblyName>([^<]+)</AssemblyName>");
            if (named.Success && named.Groups[1].Value.Trim().Equals(program, StringComparison.OrdinalIgnoreCase)) return project;
        }
        return null;
    }

    /// <summary>The project the running program was built from, as <see cref="OwnProject(string, string)"/> finds it, or null.</summary>
    internal static string? OwnProject() => OwnProject(AppContext.BaseDirectory, System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name);
}

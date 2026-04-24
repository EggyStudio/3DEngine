namespace Editor.Shell;

/// <summary>Result of a shell script compilation attempt.</summary>
/// <remarks>
/// Produced by <see cref="ShellCompiler"/> after each compilation cycle. Contains
/// success/failure status, error details with source locations, warnings, and the list
/// of files that were compiled.
/// </remarks>
/// <seealso cref="ShellCompiler"/>
/// <seealso cref="ShellCompilationError"/>
public sealed class ShellCompilationResult
{
    /// <summary>Whether the compilation succeeded without errors.</summary>
    public bool Success { get; set; }

    /// <summary>Human-readable summary message (e.g. "Compiled 3 file(s) successfully (gen 2).").</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>File names (without path) that were included in the compilation.</summary>
    public string[] Files { get; set; } = [];

    /// <summary>Compilation errors with source file location information.</summary>
    public List<ShellCompilationError> Errors { get; set; } = [];

    /// <summary>Non-fatal warnings (e.g. builder instantiation failures).</summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>A single compilation error with location info.</summary>
/// <seealso cref="ShellCompilationResult"/>
public sealed class ShellCompilationError
{
    /// <summary>Source file name (without path) where the error occurred.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Error message from the compiler diagnostic.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>1-based line number in the source file.</summary>
    public int Line { get; set; }

    /// <summary>1-based column number in the source file.</summary>
    public int Column { get; set; }
}

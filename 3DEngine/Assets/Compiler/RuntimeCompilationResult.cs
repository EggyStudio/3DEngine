namespace Engine.Files.Compiler;

/// <summary>Outcome of a single <see cref="RuntimeAssemblyCompiler{TResult}"/> compilation cycle.</summary>
/// <remarks>
/// Subclassed by domain-specific result types (such as <c>BehaviorCompilationResult</c>) which can add post-load metadata.
/// </remarks>
public class RuntimeCompilationResult
{
    /// <summary>Whether the compilation produced a loadable assembly without errors.</summary>
    public bool Success { get; set; }

    /// <summary>Human-readable summary message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>File names (without path) that were included in the compilation.</summary>
    public string[] Files { get; set; } = [];

    /// <summary>Compilation errors with source file location info.</summary>
    public List<RuntimeCompilationError> Errors { get; set; } = [];

    /// <summary>Non-fatal warnings (e.g. type-discovery instantiation failures).</summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>A single Roslyn / build diagnostic with source location.</summary>
public class RuntimeCompilationError
{
    /// <summary>Source file name (without path) where the error occurred.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Diagnostic message text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>1-based line number in the source file (0 = unknown).</summary>
    public int Line { get; set; }

    /// <summary>1-based column number in the source file (0 = unknown).</summary>
    public int Column { get; set; }
}
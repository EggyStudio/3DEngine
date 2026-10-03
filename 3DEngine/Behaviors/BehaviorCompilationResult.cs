namespace Engine;

/// <summary>Result of a behavior-script compilation cycle.</summary>
/// <remarks>
/// Adds <see cref="RegisteredCount"/> on top of the shared <see cref="Engine.Files.Compiler.RuntimeCompilationResult"/>
/// so callers can report how many behavior systems were registered for the new generation.
/// </remarks>
public sealed class BehaviorCompilationResult : Engine.Files.Compiler.RuntimeCompilationResult
{
    /// <summary>Number of <c>[Behavior]</c>-derived systems registered into the <see cref="App"/> for this generation.</summary>
    public int RegisteredCount { get; set; }
}
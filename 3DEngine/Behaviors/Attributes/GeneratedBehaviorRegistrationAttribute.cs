namespace Engine;

/// <summary>
/// Marks the method the generator writes to register an assembly's behaviors, by which
/// <see cref="RuntimeBehaviorCompiler"/> finds it in a script compiled while the app runs.
/// </summary>
/// <remarks>
/// Public because the generated code is in the game's assembly, and hidden from an editor's
/// completion because a program never writes it.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class GeneratedBehaviorRegistrationAttribute : Attribute;

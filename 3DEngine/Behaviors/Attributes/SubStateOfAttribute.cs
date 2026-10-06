namespace Engine;

/// <summary>
/// Declares an enum a state machine that exists only while another is in a value, as
/// <see cref="App.AddSubState{TSub, TParent}"/> adds one, entered at <see cref="Initial"/> or at
/// its first member each time the parent enters <see cref="WhileIn"/>.
/// </summary>
/// <example>
/// <code>
/// [SubStateOf(Screen.Playing)]
/// public enum Pause { Running, Paused }
/// </code>
/// </example>
/// <param name="whileIn">The value of the parent state the sub-state exists in.</param>
[AttributeUsage(AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
public sealed class SubStateOfAttribute(object whileIn) : Attribute
{
    /// <summary>The value of the parent state the sub-state exists in.</summary>
    public object WhileIn { get; } = whileIn;

    /// <summary>The value the sub-state starts at, or null for its first member.</summary>
    public object? Initial { get; set; }
}

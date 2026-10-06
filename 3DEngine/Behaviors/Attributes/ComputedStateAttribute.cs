namespace Engine;

/// <summary>
/// Declares a state machine computed from another by the static method it is on, as
/// <see cref="App.AddComputedState{TComputed, TSource}"/> adds one. The method takes the source
/// state's enum and returns the computed one's, nullable, with null for no state at all.
/// </summary>
/// <example>
/// <code>
/// [ComputedState]
/// public static InGame? FromScreen(Screen screen) => screen is Screen.Playing or Screen.Paused ? InGame.Yes : null;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ComputedStateAttribute : Attribute;

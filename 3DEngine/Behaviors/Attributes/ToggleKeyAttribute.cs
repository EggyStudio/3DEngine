namespace Engine;

/// <summary>
/// Binds a keyboard shortcut directly to a system, toggling it on/off without any boilerplate method.
/// Each press of the specified <c>key</c> (with optional <c>modifier</c> held) flips the
/// enabled state. The system starts enabled unless <c>DefaultEnabled = false</c> is set.
/// </summary>
/// <remarks>
/// Toggle state is managed via <see cref="SystemToggleRegistry"/> in <see cref="BehaviorConditions"/>.
/// </remarks>
/// <example>
/// <code>
/// [OnRender]
/// [ToggleKey(Key.F3)]                          // F3 alone
/// [ToggleKey(Key.F3, DefaultEnabled = false)]  // F3, default off
/// [ToggleKey(Key.F3, KeyModifier.Ctrl)]        // Ctrl + F3
/// public static void Draw(BehaviorContext ctx) { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ToggleKeyAttribute : Attribute
{
    /// <summary>The keyboard key that toggles this system.</summary>
    public Key Key { get; }

    /// <summary>Optional modifier keys that must be held when pressing <see cref="Key"/>.</summary>
    public KeyModifier Modifier { get; }

    /// <summary>Initial enabled state before the first toggle. Defaults to <c>true</c>.</summary>
    public bool DefaultEnabled { get; init; } = true;

    /// <summary>Creates a new <see cref="ToggleKeyAttribute"/> binding the specified key (with optional modifier) to toggle this system.</summary>
    /// <param name="key">The keyboard key that toggles the system.</param>
    /// <param name="modifier">Optional modifier keys that must be held when pressing <paramref name="key"/>.</param>
    public ToggleKeyAttribute(Key key, KeyModifier modifier = KeyModifier.None)
    {
        Key = key;
        Modifier = modifier;
    }
}

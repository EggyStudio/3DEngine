namespace Engine;

/// <summary>Optional modifier keys that must be held when a toggle key is pressed.</summary>
[Flags]
public enum KeyModifier
{
    /// <summary>No modifier key required.</summary>
    None  = 0,
    /// <summary>Left or right Ctrl must be held.</summary>
    Ctrl  = 1 << 0,
    /// <summary>Left or right Shift must be held.</summary>
    Shift = 1 << 1,
    /// <summary>Left or right Alt must be held.</summary>
    Alt   = 1 << 2,
}

/// <summary>
/// World resource that stores per-system keyboard toggle states, keyed by system descriptor name.
/// Managed automatically when <see cref="ToggleKeyAttribute"/> is used.
/// </summary>
/// <seealso cref="ToggleKeyAttribute"/>
/// <seealso cref="BehaviorConditions"/>
public sealed class SystemToggleRegistry
{
    private readonly Dictionary<string, bool> _states = new();

    /// <summary>Returns the current enabled state for <paramref name="id"/>, defaulting to <paramref name="defaultEnabled"/> if unset.</summary>
    /// <param name="id">The unique system identifier (typically the descriptor name).</param>
    /// <param name="defaultEnabled">The default state when no toggle has been recorded for <paramref name="id"/>.</param>
    /// <returns><c>true</c> if the system is enabled; <c>false</c> if disabled.</returns>
    public bool Get(string id, bool defaultEnabled = true)
        => _states.TryGetValue(id, out var v) ? v : defaultEnabled;

    /// <summary>Flips the enabled state for <paramref name="id"/>.</summary>
    /// <param name="id">The unique system identifier.</param>
    /// <param name="defaultEnabled">The default state used if no toggle has been recorded yet.</param>
    public void Flip(string id, bool defaultEnabled = true)
        => _states[id] = !Get(id, defaultEnabled);
}

/// <summary>
/// Factory methods for the most common system run conditions.
/// Pass the returned <see cref="Func{World,Boolean}"/> to
/// <see cref="SystemDescriptor.RunIf"/> or use in condition methods with <see cref="RunIfAttribute"/>.
/// </summary>
/// <example>
/// Manual registration with a resource condition:
/// <code>
/// app.AddSystem(Stage.Update, MySystem,
///     BehaviorConditions.ResourceIs&lt;GameState&gt;(s => s.IsPlaying));
/// </code>
///
/// Manual registration with a keyboard toggle:
/// <code>
/// app.AddSystem(Stage.Update, MySystem,
///     BehaviorConditions.KeyToggle&lt;MyBehavior&gt;(Key.F3));
/// </code>
/// </example>
/// <seealso cref="RunIfAttribute"/>
/// <seealso cref="ToggleKeyAttribute"/>
/// <seealso cref="SystemToggleRegistry"/>
public static class BehaviorConditions
{
    // -- Resource conditions --

    /// <summary>
    /// Passes when resource <typeparamref name="T"/> is present in the world.
    /// </summary>
    /// <typeparam name="T">The resource type to check for.</typeparam>
    /// <returns>A condition delegate suitable for <see cref="SystemDescriptor.RunIf"/>.</returns>
    public static Func<World, bool> HasResource<T>() where T : notnull
        => static world => world.ContainsResource<T>();

    /// <summary>
    /// Passes when resource <typeparamref name="T"/> exists <em>and</em> satisfies <paramref name="predicate"/>.
    /// </summary>
    /// <typeparam name="T">The resource type to check.</typeparam>
    /// <param name="predicate">A function that must return <c>true</c> for the system to run.</param>
    /// <returns>A condition delegate suitable for <see cref="SystemDescriptor.RunIf"/>.</returns>
    public static Func<World, bool> ResourceIs<T>(Func<T, bool> predicate) where T : notnull
        => world => world.TryGetResource<T>(out var r) && predicate(r);

    // -- Component / entity conditions --

    /// <summary>
    /// Passes when at least one entity currently has component <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The component type to check for.</typeparam>
    /// <returns>A condition delegate suitable for <see cref="SystemDescriptor.RunIf"/>.</returns>
    public static Func<World, bool> AnyWithComponent<T>()
        => static world => world.Resource<EcsWorld>().Count<T>() > 0;

    // -- Keyboard toggle --

    /// <summary>
    /// Returns a stateful toggle condition for manual registration.
    /// <typeparamref name="TTag"/> acts as the unique identity; use the behavior struct as the tag.
    /// </summary>
    /// <typeparam name="TTag">A type used as the unique key for the toggle state.</typeparam>
    /// <param name="key">The keyboard key that toggles the system.</param>
    /// <param name="modifier">Optional modifier keys that must be held.</param>
    /// <param name="defaultEnabled">Initial enabled state before the first toggle.</param>
    /// <returns>A condition delegate suitable for <see cref="SystemDescriptor.RunIf"/>.</returns>
    public static Func<World, bool> KeyToggle<TTag>(Key key, KeyModifier modifier = KeyModifier.None, bool defaultEnabled = true) where TTag : notnull
        => KeyToggle(typeof(TTag).FullName!, key, modifier, defaultEnabled);

    /// <summary>
    /// String-keyed overload used by source-generated systems. Prefer the generic overload in user code.
    /// </summary>
    /// <param name="systemId">Unique string identifier for the toggle state.</param>
    /// <param name="key">The keyboard key that toggles the system.</param>
    /// <param name="modifier">Optional modifier keys that must be held.</param>
    /// <param name="defaultEnabled">Initial enabled state before the first toggle.</param>
    /// <returns>A condition delegate suitable for <see cref="SystemDescriptor.RunIf"/>.</returns>
    public static Func<World, bool> KeyToggle(string systemId, Key key, KeyModifier modifier = KeyModifier.None, bool defaultEnabled = true)
    {
        return world =>
        {
            var reg = world.GetOrInsertResource<SystemToggleRegistry>(new SystemToggleRegistry());
            if (world.TryResource<Input>() is { } input && input.KeyPressed(key) && ModifiersHeld(input, modifier))
                reg.Flip(systemId, defaultEnabled);
            return reg.Get(systemId, defaultEnabled);
        };
    }

    /// <summary>Checks whether the required modifier keys are currently held.</summary>
    /// <param name="input">The current <see cref="Input"/> state.</param>
    /// <param name="modifier">The modifier flags to check.</param>
    /// <returns><c>true</c> if all required modifiers are held; otherwise <c>false</c>.</returns>
    internal static bool ModifiersHeld(Input input, KeyModifier modifier)
    {
        if (modifier == KeyModifier.None) return true;
        if ((modifier & KeyModifier.Ctrl)  != 0 && !input.KeyDown(Key.LCtrl)  && !input.KeyDown(Key.RCtrl))  return false;
        if ((modifier & KeyModifier.Shift) != 0 && !input.KeyDown(Key.LShift) && !input.KeyDown(Key.RShift)) return false;
        if ((modifier & KeyModifier.Alt)   != 0 && !input.KeyDown(Key.LAlt)   && !input.KeyDown(Key.RAlt))   return false;
        return true;
    }
}

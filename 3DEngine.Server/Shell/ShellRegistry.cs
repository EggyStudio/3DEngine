namespace Editor.Shell;

/// <summary>
/// Central registry holding the current <see cref="ShellDescriptor"/>.
/// Observable - fires <see cref="Changed"/> whenever the descriptor tree is
/// rebuilt (e.g., after a script hot-reload). Thread-safe for concurrent reads
/// and atomic swaps.
/// </summary>
/// <remarks>
/// <para>
/// The registry acts as the bridge between the C# scripting layer (which produces descriptors)
/// and the Blazor rendering layer (which consumes them). The <see cref="Update"/> method is called
/// by the <c>ShellCompiler</c> after a successful compilation, and the <see cref="Changed"/> event
/// triggers Blazor's <c>StateHasChanged</c> to re-render the UI.
/// </para>
/// <para>
/// Thread safety: reads via <see cref="Current"/> and <see cref="Version"/> are guarded by a
/// lock, and <see cref="Update"/> atomically swaps the descriptor and increments the version
/// before firing the event outside the lock.
/// </para>
/// </remarks>
/// <seealso cref="ShellDescriptor"/>
public sealed class ShellRegistry
{
    private readonly Lock _lock = new();
    private ShellDescriptor _current = new();
    private int _version;

    /// <summary>Fired when the shell descriptor is replaced (hot-reload).</summary>
    public event Action? Changed;

    /// <summary>Monotonically increasing version; bumped on every swap.</summary>
    public int Version { get { lock (_lock) return _version; } }

    /// <summary>Current shell descriptor snapshot.</summary>
    public ShellDescriptor Current
    {
        get { lock (_lock) return _current; }
    }

    /// <summary>
    /// Atomically replaces the shell descriptor and fires <see cref="Changed"/>.
    /// Called by the script compiler after a successful rebuild.
    /// </summary>
    /// <param name="descriptor">The new shell descriptor. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    public void Update(ShellDescriptor descriptor)
    {
        lock (_lock)
        {
            _current = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _version++;
        }
        Changed?.Invoke();
    }
}

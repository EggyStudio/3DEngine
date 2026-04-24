using Editor.Shell;
using Microsoft.AspNetCore.Components;

namespace Editor.Server.Shared;

/// <summary>
/// Blazor layout component that reads the current <see cref="ShellDescriptor"/> from the
/// <see cref="ShellRegistry"/> and re-renders whenever the descriptor changes (hot-reload).
/// </summary>
/// <remarks>
/// Subscribes to <see cref="ShellRegistry.Changed"/> on initialization and
/// calls <see cref="ComponentBase.StateHasChanged"/> via <see cref="ComponentBase.InvokeAsync(Action)"/>
/// to safely trigger a Blazor re-render from the compiler's background thread.
/// </remarks>
/// <seealso cref="ShellRegistry"/>
/// <seealso cref="ShellDescriptor"/>
public partial class ShellLayout : LayoutComponentBase, IDisposable
{
    [Inject] private ShellRegistry Registry { get; set; } = null!;

    /// <summary>Gets the current shell descriptor from the registry.</summary>
    private ShellDescriptor Descriptor => Registry.Current;

    /// <summary>Subscribes to shell change notifications on initialization.</summary>
    protected override void OnInitialized()
    {
        Registry.Changed += OnShellChanged;
    }

    /// <summary>Handles shell changes by triggering a Blazor re-render on the synchronization context.</summary>
    private void OnShellChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    /// <summary>Unsubscribes from shell change notifications.</summary>
    public void Dispose()
    {
        Registry.Changed -= OnShellChanged;
    }
}

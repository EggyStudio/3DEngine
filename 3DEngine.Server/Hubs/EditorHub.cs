using Editor.Shell;
using Microsoft.AspNetCore.SignalR;

namespace Editor.Server.Hubs;

// ── SignalR Contracts ────────────────────────────────────────────────────

/// <summary>Client-to-server calls available on the editor hub.</summary>
/// <seealso cref="EditorHub"/>
/// <seealso cref="IEditorClient"/>
public interface IEditorHub
{
    /// <summary>Selects an entity in the editor; broadcasts to other clients.</summary>
    /// <param name="entityId">The ECS entity id to select.</param>
    Task SelectEntity(int entityId);

    /// <summary>Clears the current entity selection; broadcasts to other clients.</summary>
    Task DeselectEntity();

    /// <summary>Queues a component field edit for the engine to process.</summary>
    /// <param name="entityId">Target entity id.</param>
    /// <param name="componentType">Fully qualified component type name.</param>
    /// <param name="fieldName">Field or property name to update.</param>
    /// <param name="jsonValue">New value serialized as JSON.</param>
    Task UpdateComponentField(int entityId, string componentType, string fieldName, string jsonValue);

    /// <summary>Queues a named command for the engine to execute.</summary>
    /// <param name="commandId">Unique command identifier (e.g. <c>"save-scene"</c>).</param>
    /// <param name="jsonArgs">Optional JSON-serialized arguments for the command.</param>
    Task ExecuteCommand(string commandId, string? jsonArgs = null);

    /// <summary>Returns the current shell descriptor version number.</summary>
    /// <returns>The monotonically increasing version from <see cref="ShellRegistry.Version"/>.</returns>
    Task<int> GetShellVersion();
}

/// <summary>Server-to-client callbacks pushed to connected editor clients.</summary>
/// <seealso cref="EditorHub"/>
/// <seealso cref="IEditorHub"/>
public interface IEditorClient
{
    /// <summary>Notifies clients that an entity was selected.</summary>
    /// <param name="entityId">The selected entity id.</param>
    Task OnEntitySelected(int entityId);

    /// <summary>Notifies clients that the entity selection was cleared.</summary>
    Task OnEntityDeselected();

    /// <summary>Notifies clients that the shell descriptor has been updated (hot-reload).</summary>
    /// <param name="version">The new shell descriptor version.</param>
    Task OnShellChanged(int version);
}

// ── Hub ─────────────────────────────────────────────────────────────────

/// <summary>
/// SignalR hub bridging the Blazor editor UI with the engine runtime.
/// Handles entity selection, component field editing, command execution,
/// and shell version queries.
/// </summary>
/// <remarks>
/// <para>
/// The hub is strongly typed via <see cref="IEditorClient"/> for server-to-client callbacks
/// and implements <see cref="IEditorHub"/> for client-to-server calls.
/// </para>
/// <para>
/// Pending field edits and commands are enqueued in <see cref="EditorState"/> for the engine
/// to drain on its update loop, avoiding cross-thread mutation of ECS data.
/// </para>
/// </remarks>
/// <seealso cref="EditorState"/>
/// <seealso cref="ShellRegistry"/>
public sealed class EditorHub : Hub<IEditorClient>, IEditorHub
{
    private readonly ShellRegistry _registry;
    private readonly EditorState _state;

    /// <summary>Creates a new <see cref="EditorHub"/> with injected services.</summary>
    /// <param name="registry">The shell registry for version queries.</param>
    /// <param name="state">Shared mutable state for pending edits and commands.</param>
    public EditorHub(ShellRegistry registry, EditorState state)
    {
        _registry = registry;
        _state = state;
    }

    /// <inheritdoc />
    public Task SelectEntity(int entityId)
    {
        _state.SelectedEntityId = entityId;
        return Clients.Others.OnEntitySelected(entityId);
    }

    /// <inheritdoc />
    public Task DeselectEntity()
    {
        _state.SelectedEntityId = null;
        return Clients.Others.OnEntityDeselected();
    }

    /// <inheritdoc />
    public Task UpdateComponentField(int entityId, string componentType, string fieldName, string jsonValue)
    {
        _state.PendingFieldEdits.Enqueue(new FieldEdit(entityId, componentType, fieldName, jsonValue));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ExecuteCommand(string commandId, string? jsonArgs = null)
    {
        _state.PendingCommands.Enqueue(new EditorCommand(commandId, jsonArgs));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> GetShellVersion() => Task.FromResult(_registry.Version);

    /// <summary>Pushes the current shell version to the newly connected client.</summary>
    public override Task OnConnectedAsync()
    {
        // Push current shell state to the newly connected client
        return Clients.Caller.OnShellChanged(_registry.Version);
    }
}

/// <summary>
/// Shared mutable state between the SignalR hub and the engine bridge.
/// Thread-safe queues for pending edits and commands.
/// </summary>
/// <remarks>
/// <para>
/// The engine's update loop drains <see cref="PendingFieldEdits"/> and <see cref="PendingCommands"/>
/// each frame, applying changes within the ECS world's thread-safe boundaries.
/// </para>
/// <para>
/// <see cref="SelectedEntityId"/> is a simple volatile int? - the hub writes it from SignalR threads,
/// and the engine reads it on the main thread. This is safe for single-writer/single-reader scenarios.
/// </para>
/// </remarks>
/// <seealso cref="EditorHub"/>
/// <seealso cref="FieldEdit"/>
/// <seealso cref="EditorCommand"/>
public sealed class EditorState
{
    /// <summary>Currently selected entity id, or <see langword="null"/> if nothing is selected.</summary>
    public int? SelectedEntityId { get; set; }

    /// <summary>Thread-safe queue of pending component field edits from the UI.</summary>
    public System.Collections.Concurrent.ConcurrentQueue<FieldEdit> PendingFieldEdits { get; } = new();

    /// <summary>Thread-safe queue of pending commands from the UI.</summary>
    public System.Collections.Concurrent.ConcurrentQueue<EditorCommand> PendingCommands { get; } = new();
}

/// <summary>Represents a pending component field edit from the editor UI.</summary>
/// <param name="EntityId">Target entity id.</param>
/// <param name="ComponentType">Fully qualified component type name.</param>
/// <param name="FieldName">Field or property name to update.</param>
/// <param name="JsonValue">New value serialized as JSON.</param>
public sealed record FieldEdit(int EntityId, string ComponentType, string FieldName, string JsonValue);

/// <summary>Represents a pending editor command.</summary>
/// <param name="CommandId">Unique command identifier.</param>
/// <param name="JsonArgs">Optional JSON-serialized arguments.</param>
public sealed record EditorCommand(string CommandId, string? JsonArgs);

namespace Editor.Shell;

/// <summary>
/// Entry point for editor shell scripts. Classes marked with <see cref="EditorShellAttribute"/>
/// implementing this interface are discovered at runtime and their <see cref="Build"/> method
/// is called to populate the <see cref="ShellDescriptor"/>.
/// </summary>
/// <example><code>
/// [EditorShell]
/// public class MyShell : IEditorShellBuilder
/// {
///     public int Order => 10;
///     public void Build(IShellBuilder shell)
///     {
///         shell.Panel("inspector", "Inspector", DockZone.Right, p => p
///             .Icon(Icon.From(Lucide.Settings))
///             .Content(c => c.Text(null, "Hello from a script!")));
///     }
/// }
/// </code></example>
/// <seealso cref="EditorShellAttribute"/>
/// <seealso cref="IShellBuilder"/>
/// <seealso cref="ShellDescriptor"/>
public interface IEditorShellBuilder
{
    /// <summary>Priority for ordering multiple shell builders. Lower values run first.</summary>
    int Order => 0;

    /// <summary>Declaratively builds the editor shell layout.</summary>
    /// <param name="shell">The top-level shell builder providing panel and metadata registration.</param>
    void Build(IShellBuilder shell);
}

/// <summary>Top-level shell builder. Provides access to panels and metadata.</summary>
/// <seealso cref="IEditorShellBuilder"/>
/// <seealso cref="ShellDescriptor"/>
/// <seealso cref="IPanelBuilder"/>
public interface IShellBuilder
{
    /// <summary>Registers a dockable panel with a configuration callback.</summary>
    /// <param name="id">Unique panel identifier.</param>
    /// <param name="title">Display title for the panel header / tab.</param>
    /// <param name="zone">Default dock zone for initial placement.</param>
    /// <param name="configure">Callback to configure the panel via <see cref="IPanelBuilder"/>.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IShellBuilder Panel(string id, string title, DockZone zone, Action<IPanelBuilder> configure);

    /// <summary>Registers a dockable panel with a widget key (simple shorthand).</summary>
    /// <param name="id">Unique panel identifier.</param>
    /// <param name="title">Display title for the panel header / tab.</param>
    /// <param name="zone">Default dock zone for initial placement.</param>
    /// <param name="widgetKey">Key into the widget registry for the panel's content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IShellBuilder Panel(string id, string title, DockZone zone, string widgetKey);

    /// <summary>Attaches arbitrary metadata to the shell descriptor.</summary>
    /// <param name="key">Metadata key.</param>
    /// <param name="value">Metadata value.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IShellBuilder Meta(string key, object value);
}


/// <summary>Root builder that assembles a <see cref="PanelDescriptor"/>.</summary>
/// <remarks>
/// Concrete implementation of <see cref="IPanelBuilder"/>. Mutates the provided
/// <see cref="PanelDescriptor"/> in place as fluent methods are called.
/// </remarks>
internal sealed class PanelBuilder(PanelDescriptor desc) : IPanelBuilder
{
    /// <inheritdoc />
    public IPanelBuilder Widget(string widgetKey) { desc.WidgetKey = widgetKey; return this; }

    /// <inheritdoc />
    public IPanelBuilder Content(Action<IContentBuilder> configure)
    {
        var cb = new ContentBuilder();
        configure(cb);
        desc.Content = cb.Build();
        return this;
    }

    /// <inheritdoc />
    public IPanelBuilder Icon(string? icon) { desc.Icon = icon; return this; }

    /// <inheritdoc />
    public IPanelBuilder TabGroup(string groupId, int order = 0)
    {
        desc.TabGroupId = groupId;
        desc.TabOrder = order;
        return this;
    }

    /// <inheritdoc />
    public IPanelBuilder InitialSize(float fraction) { desc.InitialSize = Math.Clamp(fraction, 0.05f, 0.95f); return this; }

    /// <inheritdoc />
    public IPanelBuilder Closeable(bool closeable = true) { desc.Closeable = closeable; return this; }

    /// <inheritdoc />
    public IPanelBuilder Visible(bool visible = true) { desc.Visible = visible; return this; }

    /// <inheritdoc />
    public IPanelBuilder Route(string route) { desc.Route = route; return this; }
}

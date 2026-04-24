namespace Editor.Shell;

/// <summary>Builder for configuring a panel's content and behavior.</summary>
public interface IPanelBuilder
{
    /// <summary>Sets the widget key for the panel content.</summary>
    IPanelBuilder Widget(string widgetKey);

    /// <summary>Builds panel content from the element content builder API.</summary>
    IPanelBuilder Content(Action<IContentBuilder> configure);

    /// <summary>Sets the icon shown in the panel tab.</summary>
    IPanelBuilder Icon(string? icon);

    /// <summary>Adds this panel as a tab in the specified tab group.</summary>
    IPanelBuilder TabGroup(string groupId, int order = 0);

    /// <summary>Sets the initial size fraction (0..1) of the parent dock area.</summary>
    IPanelBuilder InitialSize(float fraction);

    /// <summary>Whether the panel can be closed.</summary>
    IPanelBuilder Closeable(bool closeable = true);

    /// <summary>Whether the panel starts visible.</summary>
    IPanelBuilder Visible(bool visible = true);

    /// <summary>Assigns a URL route to this panel (e.g. "/showcase/buttons"). The host resolves routes dynamically.</summary>
    IPanelBuilder Route(string route);
}

/// <summary>Root builder that assembles a <see cref="ShellDescriptor"/>.</summary>
/// <remarks>
/// Concrete implementation of <see cref="IShellBuilder"/>. Accumulates panel descriptors
/// and metadata. Call <see cref="Build"/> to produce the final <see cref="ShellDescriptor"/>.
/// </remarks>
/// <seealso cref="IShellBuilder"/>
/// <seealso cref="ShellDescriptor"/>
public sealed class ShellBuilder : IShellBuilder
{
    private readonly ShellDescriptor _desc = new();

    /// <summary>Returns the assembled <see cref="ShellDescriptor"/>.</summary>
    /// <returns>The complete shell descriptor with all registered panels and metadata.</returns>
    public ShellDescriptor Build() => _desc;

    /// <inheritdoc />
    public IShellBuilder Panel(string id, string title, DockZone zone, Action<IPanelBuilder> configure)
    {
        var panel = new PanelDescriptor { Id = id, Title = title, DefaultZone = zone };
        var b = new PanelBuilder(panel);
        configure(b);
        _desc.Panels.Add(panel);
        return this;
    }

    /// <inheritdoc />
    public IShellBuilder Panel(string id, string title, DockZone zone, string widgetKey)
    {
        _desc.Panels.Add(new PanelDescriptor
        {
            Id = id,
            Title = title,
            DefaultZone = zone,
            WidgetKey = widgetKey
        });
        return this;
    }

    /// <inheritdoc />
    public IShellBuilder Meta(string key, object value)
    {
        _desc.Metadata[key] = value;
        return this;
    }
}

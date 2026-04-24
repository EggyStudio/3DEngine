namespace Editor.Shell;

// ── Shell Discovery ─────────────────────────────────────────────────────

/// <summary>
/// Marks a class implementing <see cref="IEditorShellBuilder"/> for discovery
/// by the runtime script compiler. The builder's <c>Build()</c> method is called
/// to produce a <see cref="ShellDescriptor"/> tree that drives the editor UI.
/// </summary>
/// <seealso cref="IEditorShellBuilder"/>
/// <seealso cref="ShellDescriptor"/>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EditorShellAttribute : Attribute;

// ── Blazor Panel Discovery ──────────────────────────────────────────────

/// <summary>
/// Marks a native Blazor component (<c>.razor</c> file) as an editor panel.
/// The compiler discovers types annotated with this attribute and creates a
/// <see cref="PanelDescriptor"/> with <see cref="PanelDescriptor.ComponentType"/>
/// set to the component's <see cref="Type"/>, enabling <c>DynamicComponent</c> rendering.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute in a <c>.razor</c> file via the <c>@attribute</c> directive:
/// </para>
/// <code>
/// @attribute [EditorPanel("my-panel", "My Panel", DockZone.Right)]
/// </code>
/// <para>
/// The component is compiled at runtime alongside <c>.cs</c> scripts and benefits
/// from full Blazor features: <c>@onclick</c>, <c>@bind</c>, <c>@inject</c>,
/// component parameters, lifecycle methods, and CSS isolation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// @attribute [EditorPanel("inspector", "Inspector", DockZone.Right, Icon = "settings", Route = "/inspector")]
///
/// @inject ShellRegistry Registry
///
/// &lt;h3&gt;Inspector Panel&lt;/h3&gt;
/// &lt;p&gt;Selected entity: @EntityId&lt;/p&gt;
///
/// @code {
///     [Parameter] public int? EntityId { get; set; }
/// }
/// </code>
/// </example>
/// <seealso cref="EditorShellAttribute"/>
/// <seealso cref="PanelDescriptor"/>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EditorPanelAttribute : Attribute
{
    /// <summary>Unique panel identifier (used for tab grouping and persistence).</summary>
    public string Id { get; }

    /// <summary>Display title shown in the panel header / tab.</summary>
    public string Title { get; }

    /// <summary>Default dock zone for initial placement.</summary>
    public DockZone Zone { get; }

    /// <summary>Optional icon name displayed alongside the title (e.g. <c>"settings"</c>).</summary>
    public string? Icon { get; set; }

    /// <summary>Optional URL route for this panel (e.g. <c>"/inspector"</c>).</summary>
    public string? Route { get; set; }

    /// <summary>Optional tab group identifier. When set, the panel appears as a tab in the group.</summary>
    public string? TabGroup { get; set; }

    /// <summary>Sort order within a tab group. Lower values appear first.</summary>
    public int TabOrder { get; set; }

    /// <summary>Initial size as a fraction (0..1) of the parent dock area. Defaults to 0.25.</summary>
    public float InitialSize { get; set; } = 0.25f;

    /// <summary>Whether the panel can be closed by the user. Defaults to <see langword="true"/>.</summary>
    public bool Closeable { get; set; } = true;

    /// <summary>Whether the panel is visible on creation. Defaults to <see langword="true"/>.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Priority for ordering when multiple panels compete for the same position. Lower values win.</summary>
    public int Order { get; set; }

    /// <summary>Creates a new <see cref="EditorPanelAttribute"/> with the specified panel metadata.</summary>
    /// <param name="id">Unique panel identifier.</param>
    /// <param name="title">Display title for the panel header / tab.</param>
    /// <param name="zone">Default dock zone for initial placement. Defaults to <see cref="DockZone.Center"/>.</param>
    public EditorPanelAttribute(string id, string title, DockZone zone = DockZone.Center)
    {
        Id = id;
        Title = title;
        Zone = zone;
    }
}

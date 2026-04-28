namespace Editor.Shell;

// -- Dock & Layout Enums --

/// <summary>Hint for the default dock zone of a panel.</summary>
/// <remarks>
/// The dock zone determines the initial placement of a panel in the editor layout.
/// The Blazor-side dock layout component reads this value to position panels at startup.
/// Panels can be moved by the user at runtime via drag-and-drop.
/// </remarks>
/// <seealso cref="PanelDescriptor"/>
public enum DockZone
{
    /// <summary>Docked to the top edge (e.g. toolbars, menubars).</summary>
    Top,

    /// <summary>Docked to the left edge (e.g. scene tree, asset browser).</summary>
    Left,

    /// <summary>Docked to the right edge (e.g. inspector, properties panel).</summary>
    Right,

    /// <summary>Docked to the bottom edge (e.g. console, log output).</summary>
    Bottom,

    /// <summary>Center workspace area (e.g. viewport, main editor canvas).</summary>
    Center,

    /// <summary>Floating window, not docked to any edge.</summary>
    Float,
}

// -- Root Descriptor --

/// <summary>
/// Complete description of the editor shell UI.
/// Produced by <see cref="IEditorShellBuilder"/> and consumed by the Blazor renderer.
/// </summary>
/// <remarks>
/// <para>
/// The shell descriptor is a Blazor-independent POCO tree that fully describes the editor layout.
/// It is built on the C# scripting side by <see cref="IEditorShellBuilder"/> implementations and
/// pushed into the <see cref="ShellRegistry"/> where the Blazor renderer reads it.
/// </para>
/// <para>
/// When the script compiler performs a hot-reload, a new <see cref="ShellDescriptor"/> is built
/// and atomically swapped into the registry, triggering a UI refresh.
/// </para>
/// </remarks>
/// <seealso cref="ShellRegistry"/>
/// <seealso cref="IEditorShellBuilder"/>
/// <seealso cref="PanelDescriptor"/>
public sealed class ShellDescriptor
{
    /// <summary>All dockable panels registered in the shell.</summary>
    public List<PanelDescriptor> Panels { get; set; } = [];

    /// <summary>User-defined metadata (arbitrary key-value pairs attached by shell builders).</summary>
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// <summary>
    /// CSS snippets collected from <c>.css</c> files in the scripts directory.
    /// Injected into the page at render time via a <c>&lt;style&gt;</c> block.
    /// </summary>
    public List<string> CustomCss { get; set; } = [];
}

// -- Panels (Dockable) --

/// <summary>
/// Describes a dockable panel in the editor layout.
/// </summary>
/// <remarks>
/// <para>
/// Panels are the primary building blocks of the editor UI. Each panel occupies a dock zone
/// and renders either a widget (by key) or an <see cref="Element"/> tree built from the
/// content builder API. The <see cref="Content"/> property takes priority over <see cref="WidgetKey"/>.
/// </para>
/// <para>
/// Panels can be grouped into tab groups using <see cref="TabGroupId"/>, allowing multiple
/// panels to share the same dock position with tabbed navigation.
/// </para>
/// </remarks>
/// <seealso cref="ShellDescriptor"/>
/// <seealso cref="DockZone"/>
/// <seealso cref="IPanelBuilder"/>
public sealed class PanelDescriptor
{
    /// <summary>Unique identifier for this panel (used for tab grouping and persistence).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display title shown in the panel header / tab.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional icon name displayed alongside the title.</summary>
    public string? Icon { get; set; }

    /// <summary>Default dock zone for initial placement. Defaults to <see cref="DockZone.Left"/>.</summary>
    public DockZone DefaultZone { get; set; } = DockZone.Left;

    /// <summary>If set, this panel is a tab inside the panel with matching <see cref="TabGroupId"/>.</summary>
    public string? TabGroupId { get; set; }

    /// <summary>Sort order within a tab group. Lower values appear first.</summary>
    public int TabOrder { get; set; }

    /// <summary>Key into the widget registry for the panel's content.</summary>
    public string WidgetKey { get; set; } = string.Empty;

    /// <summary>Element tree built from the content builder API. Takes priority over <see cref="WidgetKey"/>.</summary>
    public List<Element>? Content { get; set; }

    /// <summary>
    /// When set, the panel renders a native Blazor component via <c>DynamicComponent</c>.
    /// Takes priority over both <see cref="Content"/> and <see cref="WidgetKey"/>.
    /// Populated automatically for <c>.razor</c> files annotated with <see cref="EditorPanelAttribute"/>.
    /// </summary>
    public Type? ComponentType { get; set; }

    /// <summary>Initial size as a fraction (0..1) of the parent dock area. Defaults to 0.25.</summary>
    public float InitialSize { get; set; } = 0.25f;

    /// <summary>Whether the panel can be closed by the user. Defaults to <see langword="true"/>.</summary>
    public bool Closeable { get; set; } = true;

    /// <summary>Whether the panel is visible on creation. Defaults to <see langword="true"/>.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// When set, this panel's content is served at the given URL route (e.g. "/showcase/buttons").
    /// A generic catch-all page in the host resolves the route to this panel at runtime.
    /// </summary>
    public string? Route { get; set; }
}

// -- Content-level Descriptors --

/// <summary>Describes a horizontal menubar with top-level menus (rendered inside content).</summary>
/// <seealso cref="IMenubarBuilder"/>
/// <seealso cref="MenubarMenuDescriptor"/>
public sealed class MenubarDescriptor
{
    /// <summary>Ordered list of top-level menus in the menubar.</summary>
    public List<MenubarMenuDescriptor> Menus { get; set; } = [];
}

/// <summary>A single top-level menu in the menubar.</summary>
/// <seealso cref="MenubarDescriptor"/>
public sealed class MenubarMenuDescriptor
{
    /// <summary>Menu label displayed in the menubar.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Menu items as an element tree (menu-item, menu-separator, menu-sub, etc.).</summary>
    public List<Element> Items { get; set; } = [];
}

/// <summary>Describes a navigation menu with grouped links (rendered inside content).</summary>
/// <seealso cref="INavigationMenuBuilder"/>
/// <seealso cref="NavMenuGroupDescriptor"/>
public sealed class NavigationMenuDescriptor
{
    /// <summary>Ordered list of navigation groups.</summary>
    public List<NavMenuGroupDescriptor> Groups { get; set; } = [];
}

/// <summary>A group of navigation items, optionally with a title.</summary>
/// <seealso cref="NavigationMenuDescriptor"/>
/// <seealso cref="NavMenuItemDescriptor"/>
public sealed class NavMenuGroupDescriptor
{
    /// <summary>Optional group heading. <see langword="null"/> for the default (ungrouped) group.</summary>
    public string? Title { get; set; }

    /// <summary>Navigation items in this group.</summary>
    public List<NavMenuItemDescriptor> Items { get; set; } = [];
}

/// <summary>A single navigation menu item with optional description and icon.</summary>
/// <seealso cref="NavMenuGroupDescriptor"/>
public sealed class NavMenuItemDescriptor
{
    /// <summary>Display text for the navigation link.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Navigation URL. Defaults to <c>"#"</c>.</summary>
    public string Href { get; set; } = "#";

    /// <summary>Optional description shown below the label.</summary>
    public string? Description { get; set; }

    /// <summary>Optional icon name.</summary>
    public string? Icon { get; set; }
}
namespace Editor.Shell;

/// <summary>
/// A single UI element in the content tree. Tag determines the component type,
/// Props carry configuration, Children form the tree, and event handlers are first-class.
/// </summary>
/// <remarks>
/// <para>
/// Elements form a Blazor-independent virtual DOM tree. The <c>Tag</c> value is mapped to
/// a BlazorBlueprint component by the <c>ElementRenderer</c> Razor component at render time.
/// </para>
/// <para>Standard tags include layout primitives (<c>"div"</c>, <c>"row"</c>, <c>"column"</c>, <c>"grid"</c>),
/// text nodes (<c>"text"</c>, <c>"h1"</c>–<c>"h6"</c>, <c>"p"</c>, <c>"label"</c>),
/// interactive controls (<c>"button"</c>, <c>"input"</c>, <c>"checkbox"</c>, <c>"select"</c>),
/// and composite components (<c>"card"</c>, <c>"dialog"</c>, <c>"tabs"</c>, <c>"accordion"</c>).</para>
/// </remarks>
/// <seealso cref="IContentBuilder"/>
/// <seealso cref="ContentBuilder"/>
public sealed class Element
{
    /// <summary>Component type tag (e.g. <c>"button"</c>, <c>"card"</c>, <c>"div"</c>). Defaults to <c>"div"</c>.</summary>
    public string Tag { get; set; } = "div";

    /// <summary>Optional Tailwind CSS classes applied to the element's root.</summary>
    public string? Css { get; set; }

    /// <summary>Primary text content (label, heading text, button text, etc.).</summary>
    public string? Text { get; set; }

    /// <summary>Optional identifier used for label-input association (<c>for</c> attribute) or DOM targeting.</summary>
    public string? Id { get; set; }

    /// <summary>Arbitrary key-value properties forwarded to the Blazor component (variant, icon, disabled, etc.).</summary>
    public Dictionary<string, object?> Props { get; set; } = [];

    /// <summary>Child elements forming the subtree under this node.</summary>
    public List<Element> Children { get; set; } = [];

    // Events

    /// <summary>Click event handler, used by buttons, menu items, tree items, and alert dialog confirm actions.</summary>
    public Action? OnClick { get; set; }

    /// <summary>Boolean toggle handler for checkboxes, switches, and toggle buttons.</summary>
    public Action<bool>? OnToggle { get; set; }

    /// <summary>Text input handler for text fields, textareas, selects, radio groups, and comboboxes.</summary>
    public Action<string>? OnInput { get; set; }

    /// <summary>Numeric value change handler for sliders and numeric inputs.</summary>
    public Action<double>? OnValueChanged { get; set; }

    /// <summary>Index change handler for pagination and similar indexed components.</summary>
    public Action<int>? OnIndexChanged { get; set; }

    /// <summary>Creates a default element with tag <c>"div"</c>.</summary>
    public Element()
    {
    }

    /// <summary>Creates an element with the specified tag.</summary>
    /// <param name="tag">The component type tag (e.g. <c>"button"</c>, <c>"card"</c>).</param>
    public Element(string tag) => Tag = tag;

    /// <summary>Creates an element with the specified tag and text content.</summary>
    /// <param name="tag">The component type tag.</param>
    /// <param name="text">Primary text content for the element.</param>
    public Element(string tag, string? text)
    {
        Tag = tag;
        Text = text;
    }
}

/// <summary>Fluent builder for composing UI element trees. No Blazor dependency.</summary>
/// <remarks>
/// <para>
/// The content builder provides a strongly-typed, discoverable API for editor scripts to define
/// UI without any Blazor dependency. Each method appends one or more <see cref="Element"/> nodes
/// to an internal list, which is later rendered by the Blazor-side <c>ElementRenderer</c>.
/// </para>
/// <para>All methods return <see cref="IContentBuilder"/> for fluent chaining. Container methods
/// (<see cref="Div"/>, <see cref="Row"/>, <see cref="Card"/>, etc.) accept an
/// <see cref="Action{IContentBuilder}"/> callback to define nested children.</para>
/// </remarks>
/// <example><code>
/// builder
///   .Heading(null, 2, "Settings")
///   .Card(null, card => card
///       .Title("Display")
///       .Content(c => c
///           .FieldRow("Resolution", ctrl => ctrl.Select(null,
///               new[] { ("1080p", "1920×1080"), ("4k", "3840×2160") }))
///           .FieldRow("VSync", ctrl => ctrl.Switch(null, initial: true))));
/// </code></example>
/// <seealso cref="Element"/>
/// <seealso cref="ContentBuilder"/>
/// <seealso cref="IPanelBuilder"/>
public interface IContentBuilder
{
    // -- Text & Display --

    /// <summary>Adds a plain text span.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="text">The text content to display.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Text(string? css, string text);

    /// <summary>Adds a heading element (<c>h1</c>–<c>h6</c>).</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="level">Heading level (1–6), clamped to valid range.</param>
    /// <param name="text">The heading text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Heading(string? css, int level, string text);

    /// <summary>Adds a paragraph element.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="text">The paragraph text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Paragraph(string? css, string text);

    /// <summary>Adds a label element, optionally associated with an input by <paramref name="forId"/>.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="text">The label text.</param>
    /// <param name="forId">Optional <c>id</c> of the target input element.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Label(string? css, string text, string? forId = null);

    /// <summary>Adds an icon element from the configured icon set.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="icon">Icon name (use <see cref="Icon.From(Lucide)"/> for type-safe resolution).</param>
    /// <param name="size">Icon size in pixels. Defaults to 16.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Icon(string? css, string? icon, int size = 16);

    /// <summary>Adds a badge element with optional visual variant.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="text">Badge text.</param>
    /// <param name="variant">Visual variant (<c>"default"</c>, <c>"secondary"</c>, <c>"outline"</c>, <c>"destructive"</c>).</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Badge(string? css, string text, string? variant = null);

    /// <summary>Adds an inline code element.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="text">The code text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Code(string? css, string text);

    /// <summary>Adds an avatar element with optional image source or text fallback.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="fallback">Fallback text (e.g. initials) shown when no image is available.</param>
    /// <param name="src">Image URL for the avatar.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Avatar(string? css, string? fallback = null, string? src = null);

    /// <summary>Adds a determinate progress bar.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="value">Current progress value.</param>
    /// <param name="max">Maximum value (defaults to 100).</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Progress(string? css, int value, int max = 100);

    /// <summary>Adds a skeleton loading placeholder.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="width">CSS width (e.g. <c>"100px"</c>, <c>"50%"</c>).</param>
    /// <param name="height">CSS height.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Skeleton(string? css, string? width = null, string? height = null);

    /// <summary>Adds a spinner loading indicator.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="size">Spinner size in pixels. Defaults to 16.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Spinner(string? css, int size = 16);

    /// <summary>Adds a keyboard shortcut display element.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="keys">The key combination text (e.g. <c>"Ctrl+S"</c>).</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Kbd(string? css, string keys);

    // -- Interactive --

    /// <summary>Adds a button with optional click handler, variant, icon, and state.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="label">Button label text.</param>
    /// <param name="onClick">Click event handler.</param>
    /// <param name="variant">Visual variant (<c>"default"</c>, <c>"secondary"</c>, <c>"outline"</c>, <c>"ghost"</c>, <c>"destructive"</c>, <c>"link"</c>).</param>
    /// <param name="icon">Optional icon name shown alongside the label.</param>
    /// <param name="disabled">Whether the button is disabled.</param>
    /// <param name="loading">Whether the button shows a loading spinner.</param>
    /// <param name="href">Optional URL; turns the button into a link.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Button(string? css, string label, Action? onClick = null, string? variant = null,
        string? icon = null,
        bool disabled = false, bool loading = false, string? href = null);

    /// <summary>Adds a single-line text input.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="placeholder">Placeholder text shown when empty.</param>
    /// <param name="value">Initial value.</param>
    /// <param name="onChanged">Callback invoked when the text changes.</param>
    /// <param name="id">Optional element id for label association.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Input(string? css, string? placeholder = null, string? value = null,
        Action<string>? onChanged = null, string? id = null);

    /// <summary>Adds a multi-line text area.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="placeholder">Placeholder text shown when empty.</param>
    /// <param name="value">Initial value.</param>
    /// <param name="onChanged">Callback invoked when the text changes.</param>
    /// <param name="id">Optional element id for label association.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Textarea(string? css, string? placeholder = null, string? value = null,
        Action<string>? onChanged = null, string? id = null);

    /// <summary>Adds a checkbox with optional label.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="label">Optional label shown next to the checkbox.</param>
    /// <param name="initial">Initial checked state.</param>
    /// <param name="onChanged">Callback invoked when the checked state changes.</param>
    /// <param name="id">Optional element id for label association.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Checkbox(string? css, string? label = null, bool initial = false, Action<bool>? onChanged = null,
        string? id = null);

    /// <summary>Adds a toggle switch with optional label.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="label">Optional label shown next to the switch.</param>
    /// <param name="initial">Initial on/off state.</param>
    /// <param name="onChanged">Callback invoked when the switch state changes.</param>
    /// <param name="id">Optional element id for label association.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Switch(string? css, string? label = null, bool initial = false, Action<bool>? onChanged = null,
        string? id = null);

    /// <summary>Adds a dropdown select control.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="options">Array of <c>(Value, Label)</c> tuples defining the available options.</param>
    /// <param name="placeholder">Placeholder text when no option is selected.</param>
    /// <param name="selected">Initially selected value.</param>
    /// <param name="onChanged">Callback invoked with the selected value when changed.</param>
    /// <param name="id">Optional element id for label association.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Select(string? css, (string Value, string Label)[] options, string? placeholder = null,
        string? selected = null,
        Action<string>? onChanged = null, string? id = null);

    /// <summary>Adds a radio button group.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="options">Array of <c>(Value, Label)</c> tuples for each radio option.</param>
    /// <param name="selected">Initially selected value.</param>
    /// <param name="onChanged">Callback invoked with the selected value when changed.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder RadioGroup(string? css, (string Value, string Label)[] options, string? selected = null,
        Action<string>? onChanged = null);

    /// <summary>Adds a range slider control.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="value">Initial value.</param>
    /// <param name="min">Minimum value.</param>
    /// <param name="max">Maximum value.</param>
    /// <param name="step">Step increment.</param>
    /// <param name="onChanged">Callback invoked with the new value when the slider moves.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Slider(string? css, double value = 0, double min = 0, double max = 100, double step = 1,
        Action<double>? onChanged = null);

    /// <summary>Adds a numeric input with optional bounds and step.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="value">Initial value.</param>
    /// <param name="min">Optional minimum value.</param>
    /// <param name="max">Optional maximum value.</param>
    /// <param name="step">Step increment.</param>
    /// <param name="onChanged">Callback invoked with the new value when changed.</param>
    /// <param name="id">Optional element id for label association.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder NumericInput(string? css, double value = 0, double? min = null, double? max = null, double step = 1,
        Action<double>? onChanged = null, string? id = null);

    /// <summary>Adds a searchable combobox (autocomplete dropdown).</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="options">Array of <c>(Value, Label)</c> tuples for the options.</param>
    /// <param name="placeholder">Placeholder text when empty.</param>
    /// <param name="selected">Initially selected value.</param>
    /// <param name="onChanged">Callback invoked with the selected value when changed.</param>
    /// <param name="id">Optional element id for label association.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Combobox(string? css, (string Value, string Label)[] options, string? placeholder = null,
        string? selected = null, Action<string>? onChanged = null, string? id = null);

    /// <summary>Adds a toggle button (pressable on/off).</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="label">Optional label text.</param>
    /// <param name="pressed">Initial pressed state.</param>
    /// <param name="onChanged">Callback invoked when the pressed state changes.</param>
    /// <param name="variant">Visual variant (<c>"default"</c>, <c>"outline"</c>).</param>
    /// <param name="icon">Optional icon name.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Toggle(string? css, string? label = null, bool pressed = false, Action<bool>? onChanged = null,
        string? variant = null, string? icon = null);

    /// <summary>Adds a group of exclusive toggle buttons.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="options">Array of <c>(Value, Label)</c> tuples for each toggle option.</param>
    /// <param name="selected">Initially selected value.</param>
    /// <param name="onChanged">Callback invoked with the selected value when changed.</param>
    /// <param name="variant">Visual variant applied to all toggles in the group.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder ToggleGroup(string? css, (string Value, string Label)[] options, string? selected = null,
        Action<string>? onChanged = null, string? variant = null);

    // -- Layout --

    /// <summary>Adds a horizontal separator line.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Separator(string? css = null);

    /// <summary>Adds a flexible spacer that fills available space in a flex container.</summary>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Spacer();

    /// <summary>Adds a generic container (<c>div</c>) with child elements.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="children">Callback to build the child element tree.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Div(string? css, Action<IContentBuilder> children);

    /// <summary>Adds a horizontal flex row container.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="children">Callback to build the child element tree.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Row(string? css, Action<IContentBuilder> children);

    /// <summary>Adds a vertical flex column container.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="children">Callback to build the child element tree.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Column(string? css, Action<IContentBuilder> children);

    /// <summary>Adds a CSS grid container.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="columns">Number of grid columns.</param>
    /// <param name="children">Callback to build the child element tree.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Grid(string? css, int columns, Action<IContentBuilder> children);

    /// <summary>Adds a scrollable area with overflow handling.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="children">Callback to build the scrollable content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder ScrollArea(string? css, Action<IContentBuilder> children);

    /// <summary>Adds an accordion (expandable sections) component.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="configure">Callback to add accordion items via <see cref="IAccordionBuilder"/>.</param>
    /// <param name="type">Accordion type (<c>"single"</c> or <c>"multiple"</c>). Null for default.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Accordion(string? css, Action<IAccordionBuilder> configure, string? type = null);

    /// <summary>Adds a fixed aspect-ratio container.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="ratio">The width-to-height ratio (e.g. <c>16.0 / 9.0</c>).</param>
    /// <param name="content">Callback to build the content inside the ratio container.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder AspectRatio(string? css, double ratio, Action<IContentBuilder> content);

    // -- Cards --

    /// <summary>Adds a card component with structured header, content, and footer sections.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="configure">Callback to configure the card via <see cref="ICardBuilder"/>.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Card(string? css, Action<ICardBuilder> configure);

    // -- Feedback --

    /// <summary>Adds an alert banner with optional title, description, and visual variant.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="title">Alert title text.</param>
    /// <param name="description">Alert description text.</param>
    /// <param name="variant">Visual variant (<c>"default"</c>, <c>"danger"</c>, <c>"info"</c>, <c>"success"</c>, <c>"warning"</c>).</param>
    /// <param name="icon">Optional icon name.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Alert(string? css, string? title = null, string? description = null, string? variant = null,
        string? icon = null);

    /// <summary>Adds a toast notification element.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="title">Toast title (required).</param>
    /// <param name="description">Optional description text.</param>
    /// <param name="variant">Visual variant (<c>"default"</c>, <c>"destructive"</c>).</param>
    /// <param name="icon">Optional icon name.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Toast(string? css, string title, string? description = null, string? variant = null,
        string? icon = null);

    // -- Links --

    /// <summary>Adds a hyperlink element.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="text">Link display text.</param>
    /// <param name="href">Target URL.</param>
    /// <param name="icon">Optional icon name.</param>
    /// <param name="description">Optional description shown below the link text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Link(string? css, string text, string href, string? icon = null, string? description = null);

    // -- Navigation --

    /// <summary>Adds a breadcrumb navigation bar.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="configure">Callback to add breadcrumb items via <see cref="IBreadcrumbBuilder"/>.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Breadcrumb(string? css, Action<IBreadcrumbBuilder> configure);

    /// <summary>Adds a pagination control.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="currentPage">The currently active page (1-based).</param>
    /// <param name="totalPages">Total number of pages.</param>
    /// <param name="onPageChanged">Callback invoked with the new page number when the user navigates.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Pagination(string? css, int currentPage, int totalPages, Action<int>? onPageChanged = null);

    // -- Overlays & Popups --

    /// <summary>Wraps a trigger element with a tooltip.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="trigger">Callback to build the trigger element(s).</param>
    /// <param name="tip">Tooltip text.</param>
    /// <param name="side">Tooltip placement side (<c>"top"</c>, <c>"bottom"</c>, <c>"left"</c>, <c>"right"</c>).</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Tooltip(string? css, Action<IContentBuilder> trigger, string tip, string? side = null);

    /// <summary>Adds a dropdown menu triggered by a button.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="triggerLabel">Label for the trigger button.</param>
    /// <param name="configure">Callback to build menu items via <see cref="IDropdownMenuBuilder"/>.</param>
    /// <param name="triggerVariant">Visual variant for the trigger button.</param>
    /// <param name="triggerIcon">Optional icon for the trigger button.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder DropdownMenu(string? css, string triggerLabel, Action<IDropdownMenuBuilder> configure,
        string? triggerVariant = null, string? triggerIcon = null);

    /// <summary>Adds a context menu (right-click) that wraps a trigger element.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="trigger">Callback to build the trigger element(s).</param>
    /// <param name="configure">Callback to build context menu items via <see cref="IDropdownMenuBuilder"/>.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder ContextMenu(string? css, Action<IContentBuilder> trigger, Action<IDropdownMenuBuilder> configure);

    /// <summary>Adds a popover triggered by clicking a trigger element.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="trigger">Callback to build the trigger element(s).</param>
    /// <param name="content">Callback to build the popover content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Popover(string? css, Action<IContentBuilder> trigger, Action<IContentBuilder> content);

    /// <summary>Adds a hover card that appears when hovering over a trigger element.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="trigger">Callback to build the trigger element(s).</param>
    /// <param name="content">Callback to build the hover card content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder HoverCard(string? css, Action<IContentBuilder> trigger, Action<IContentBuilder> content);

    /// <summary>Adds a drawer (sliding panel) triggered by a button.</summary>
    /// <param name="triggerLabel">Label for the trigger button.</param>
    /// <param name="configure">Callback to configure the drawer via <see cref="IDrawerBuilder"/>.</param>
    /// <param name="direction">Slide direction (<c>"left"</c>, <c>"right"</c>, <c>"top"</c>, <c>"bottom"</c>).</param>
    /// <param name="triggerVariant">Visual variant for the trigger button.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Drawer(string triggerLabel, Action<IDrawerBuilder> configure, string? direction = null,
        string? triggerVariant = null);

    /// <summary>Adds a sheet (side panel overlay) triggered by a button.</summary>
    /// <param name="triggerLabel">Label for the trigger button.</param>
    /// <param name="configure">Callback to configure the sheet via <see cref="ISheetBuilder"/>.</param>
    /// <param name="side">Sheet side (<c>"left"</c>, <c>"right"</c>, <c>"top"</c>, <c>"bottom"</c>).</param>
    /// <param name="triggerVariant">Visual variant for the trigger button.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Sheet(string triggerLabel, Action<ISheetBuilder> configure, string? side = null,
        string? triggerVariant = null);

    // -- Complex Components --

    /// <summary>Adds a tabbed component.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="configure">Callback to add tabs via <see cref="ITabsBuilder"/>.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Tabs(string? css, Action<ITabsBuilder> configure);

    /// <summary>Adds a collapsible section with a title and togglable content.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="title">Section title displayed in the collapsible header.</param>
    /// <param name="content">Callback to build the collapsible content.</param>
    /// <param name="expanded">Whether the section starts expanded.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Collapsible(string? css, string title, Action<IContentBuilder> content, bool expanded = false);

    /// <summary>Adds a modal dialog triggered by a button.</summary>
    /// <param name="triggerLabel">Label for the trigger button.</param>
    /// <param name="configure">Callback to configure the dialog via <see cref="IDialogBuilder"/>.</param>
    /// <param name="triggerVariant">Visual variant for the trigger button.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Dialog(string triggerLabel, Action<IDialogBuilder> configure, string? triggerVariant = null);

    /// <summary>Adds a confirmation dialog with cancel/confirm actions.</summary>
    /// <param name="triggerLabel">Label for the trigger button.</param>
    /// <param name="configure">Callback to configure the alert dialog via <see cref="IAlertDialogBuilder"/>.</param>
    /// <param name="triggerVariant">Visual variant for the trigger button.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder AlertDialog(string triggerLabel, Action<IAlertDialogBuilder> configure,
        string? triggerVariant = null);

    // -- Menubar & Navigation Menu --

    /// <summary>Adds a horizontal menubar with dropdown menus.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="configure">Callback to configure the menubar via <see cref="IMenubarBuilder"/>.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder Menubar(string? css, Action<IMenubarBuilder> configure);

    /// <summary>Adds a navigation menu with grouped links.</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <param name="configure">Callback to configure the menu via <see cref="INavigationMenuBuilder"/>.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder NavigationMenu(string? css, Action<INavigationMenuBuilder> configure);

    // -- Editor-specific --

    /// <summary>Adds a hierarchical tree item (used in scene tree / asset browser).</summary>
    /// <param name="label">Display text for the tree item.</param>
    /// <param name="icon">Optional icon name.</param>
    /// <param name="selected">Whether this item appears selected.</param>
    /// <param name="expanded">Whether child items are visible.</param>
    /// <param name="onClick">Callback when the item is clicked.</param>
    /// <param name="children">Optional callback to build child tree items.</param>
    /// <param name="iconColor">Optional Tailwind text color class for the icon (e.g. <c>"text-blue-400"</c>).</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder TreeItem(string label, string? icon = null, bool selected = false, bool expanded = true,
        Action? onClick = null, Action<IContentBuilder>? children = null, string? iconColor = null);

    /// <summary>Adds a labeled form field row (label on the left, control on the right).</summary>
    /// <param name="label">Field label text.</param>
    /// <param name="control">Callback to build the form control (input, select, switch, etc.).</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder FieldRow(string label, Action<IContentBuilder> control);

    /// <summary>Adds a centered empty-state placeholder with optional icon, title, and description.</summary>
    /// <param name="icon">Optional icon name.</param>
    /// <param name="title">Optional title text.</param>
    /// <param name="description">Optional description text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder EmptyState(string? icon = null, string? title = null, string? description = null);

    /// <summary>Adds a structured log entry row (used in console/log panels).</summary>
    /// <param name="time">Timestamp string.</param>
    /// <param name="level">Log level (e.g. <c>"Info"</c>, <c>"Warn"</c>, <c>"Error"</c>).</param>
    /// <param name="category">Log category / source name.</param>
    /// <param name="message">Log message text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder LogEntry(string time, string level, string category, string message);

    /// <summary>Renders a dark/light mode toggle button (ghost icon button).</summary>
    /// <param name="css">Optional Tailwind CSS classes.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IContentBuilder DarkModeToggle(string? css = null);
}

/// <summary>Sub-builder for card structure (header, title, description, content, footer).</summary>
/// <seealso cref="IContentBuilder.Card"/>
public interface ICardBuilder
{
    /// <summary>Sets the card title text (added to the card header).</summary>
    /// <param name="text">Title text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ICardBuilder Title(string text);

    /// <summary>Sets the card description text (added to the card header below the title).</summary>
    /// <param name="text">Description text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ICardBuilder Description(string text);

    /// <summary>Adds custom content to the card header section.</summary>
    /// <param name="content">Callback to build header content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ICardBuilder Header(Action<IContentBuilder> content);

    /// <summary>Sets the main body content of the card.</summary>
    /// <param name="content">Callback to build body content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ICardBuilder Content(Action<IContentBuilder> content);

    /// <summary>Sets the footer content of the card (typically action buttons).</summary>
    /// <param name="content">Callback to build footer content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ICardBuilder Footer(Action<IContentBuilder> content);

    /// <summary>Overrides the card's CSS classes.</summary>
    /// <param name="css">Tailwind CSS classes.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ICardBuilder Css(string css);
}

/// <summary>Sub-builder for tab components.</summary>
/// <seealso cref="IContentBuilder.Tabs"/>
public interface ITabsBuilder
{
    /// <summary>Adds a tab with a label and content panel.</summary>
    /// <param name="label">Tab label text.</param>
    /// <param name="content">Callback to build the tab panel content.</param>
    /// <param name="icon">Optional icon name shown in the tab header.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ITabsBuilder Tab(string label, Action<IContentBuilder> content, string? icon = null);
}

/// <summary>Sub-builder for dialog components.</summary>
/// <seealso cref="IContentBuilder.Dialog"/>
public interface IDialogBuilder
{
    /// <summary>Sets the dialog title.</summary>
    /// <param name="text">Title text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDialogBuilder Title(string text);

    /// <summary>Sets the dialog description shown below the title.</summary>
    /// <param name="text">Description text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDialogBuilder Description(string text);

    /// <summary>Sets the main dialog body content.</summary>
    /// <param name="content">Callback to build the dialog content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDialogBuilder Content(Action<IContentBuilder> content);

    /// <summary>Sets the dialog footer content (typically action buttons).</summary>
    /// <param name="content">Callback to build the footer content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDialogBuilder Footer(Action<IContentBuilder> content);
}

/// <summary>Sub-builder for alert dialog (confirmation) components.</summary>
/// <remarks>
/// Alert dialogs are modal confirmation prompts that block interaction until the user
/// confirms or cancels. They are typically used for destructive actions.
/// </remarks>
/// <seealso cref="IContentBuilder.AlertDialog"/>
public interface IAlertDialogBuilder
{
    /// <summary>Sets the alert dialog title.</summary>
    /// <param name="text">Title text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IAlertDialogBuilder Title(string text);

    /// <summary>Sets the alert dialog description (explains the action being confirmed).</summary>
    /// <param name="text">Description text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IAlertDialogBuilder Description(string text);

    /// <summary>Sets the cancel button text (defaults to "Cancel").</summary>
    /// <param name="text">Cancel button text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IAlertDialogBuilder CancelText(string text);

    /// <summary>Sets the confirm button text (defaults to "Continue").</summary>
    /// <param name="text">Confirm button text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IAlertDialogBuilder ConfirmText(string text);

    /// <summary>Sets the action invoked when the user confirms.</summary>
    /// <param name="action">Confirm callback.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IAlertDialogBuilder OnConfirm(Action action);
}

/// <summary>Sub-builder for accordion components.</summary>
/// <seealso cref="IContentBuilder.Accordion"/>
public interface IAccordionBuilder
{
    /// <summary>Adds an expandable accordion item.</summary>
    /// <param name="value">Unique value identifying this item (used for expand/collapse tracking).</param>
    /// <param name="title">Item header text.</param>
    /// <param name="content">Callback to build the expandable content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IAccordionBuilder Item(string value, string title, Action<IContentBuilder> content);
}

/// <summary>Sub-builder for dropdown menu and context menu components.</summary>
/// <remarks>
/// Supports nested submenus, checkbox items, separators, and labels for organizing
/// complex menu structures.
/// </remarks>
/// <seealso cref="IContentBuilder.DropdownMenu"/>
/// <seealso cref="IContentBuilder.ContextMenu"/>
public interface IDropdownMenuBuilder
{
    /// <summary>Adds a clickable menu item.</summary>
    /// <param name="label">Item label text.</param>
    /// <param name="onClick">Click handler.</param>
    /// <param name="icon">Optional icon name.</param>
    /// <param name="shortcut">Optional keyboard shortcut hint (e.g. <c>"Ctrl+S"</c>).</param>
    /// <param name="disabled">Whether the item is disabled.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDropdownMenuBuilder Item(string label, Action? onClick = null, string? icon = null, string? shortcut = null,
        bool disabled = false);

    /// <summary>Adds a checkbox menu item with toggle state.</summary>
    /// <param name="label">Item label text.</param>
    /// <param name="initial">Initial checked state.</param>
    /// <param name="onChanged">Callback invoked when the checked state changes.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDropdownMenuBuilder CheckboxItem(string label, bool initial = false, Action<bool>? onChanged = null);

    /// <summary>Adds a visual separator line between menu items.</summary>
    /// <returns>This builder for fluent chaining.</returns>
    IDropdownMenuBuilder Separator();

    /// <summary>Adds a non-interactive label (section header) in the menu.</summary>
    /// <param name="text">Label text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDropdownMenuBuilder Label(string text);

    /// <summary>Adds a nested submenu.</summary>
    /// <param name="label">Submenu trigger label.</param>
    /// <param name="submenu">Callback to build the submenu items.</param>
    /// <param name="icon">Optional icon for the submenu trigger.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDropdownMenuBuilder Sub(string label, Action<IDropdownMenuBuilder> submenu, string? icon = null);
}

/// <summary>Sub-builder for drawer components.</summary>
/// <seealso cref="IContentBuilder.Drawer"/>
public interface IDrawerBuilder
{
    /// <summary>Sets the drawer title.</summary>
    /// <param name="text">Title text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDrawerBuilder Title(string text);

    /// <summary>Sets the drawer description shown below the title.</summary>
    /// <param name="text">Description text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDrawerBuilder Description(string text);

    /// <summary>Sets the main drawer body content.</summary>
    /// <param name="content">Callback to build the drawer content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDrawerBuilder Content(Action<IContentBuilder> content);

    /// <summary>Sets the drawer footer content.</summary>
    /// <param name="content">Callback to build the footer content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IDrawerBuilder Footer(Action<IContentBuilder> content);
}

/// <summary>Sub-builder for sheet (side panel) components.</summary>
/// <seealso cref="IContentBuilder.Sheet"/>
public interface ISheetBuilder
{
    /// <summary>Sets the sheet title.</summary>
    /// <param name="text">Title text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ISheetBuilder Title(string text);

    /// <summary>Sets the sheet description shown below the title.</summary>
    /// <param name="text">Description text.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ISheetBuilder Description(string text);

    /// <summary>Sets the main sheet body content.</summary>
    /// <param name="content">Callback to build the sheet content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ISheetBuilder Content(Action<IContentBuilder> content);

    /// <summary>Sets the sheet footer content.</summary>
    /// <param name="content">Callback to build the footer content.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ISheetBuilder Footer(Action<IContentBuilder> content);
}

/// <summary>Sub-builder for breadcrumb navigation.</summary>
/// <seealso cref="IContentBuilder.Breadcrumb"/>
public interface IBreadcrumbBuilder
{
    /// <summary>Adds a breadcrumb item (optionally clickable).</summary>
    /// <param name="label">Display text.</param>
    /// <param name="href">Optional navigation URL. When null the item is non-interactive (current page).</param>
    /// <param name="icon">Optional icon name.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IBreadcrumbBuilder Item(string label, string? href = null, string? icon = null);

    /// <summary>Adds a visual separator between breadcrumb items.</summary>
    /// <returns>This builder for fluent chaining.</returns>
    IBreadcrumbBuilder Separator();
}

/// <summary>Sub-builder for a horizontal menubar component.</summary>
/// <seealso cref="IContentBuilder.Menubar"/>
public interface IMenubarBuilder
{
    /// <summary>Adds a top-level menu with dropdown items.</summary>
    /// <param name="label">Menu label shown in the menubar.</param>
    /// <param name="configure">Callback to build the dropdown items via <see cref="IDropdownMenuBuilder"/>.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IMenubarBuilder Menu(string label, Action<IDropdownMenuBuilder> configure);
}

/// <summary>Sub-builder for a navigation menu component with grouped links.</summary>
/// <seealso cref="IContentBuilder.NavigationMenu"/>
public interface INavigationMenuBuilder
{
    /// <summary>Adds a navigation link.</summary>
    /// <param name="label">Link display text.</param>
    /// <param name="href">Navigation URL.</param>
    /// <param name="description">Optional description shown below the link.</param>
    /// <param name="icon">Optional icon name.</param>
    /// <returns>This builder for fluent chaining.</returns>
    INavigationMenuBuilder Item(string label, string href, string? description = null, string? icon = null);

    /// <summary>Adds a named group of navigation items.</summary>
    /// <param name="title">Group heading text.</param>
    /// <param name="configure">Callback to add items to this group.</param>
    /// <returns>This builder for fluent chaining.</returns>
    INavigationMenuBuilder Group(string title, Action<INavigationMenuBuilder> configure);
}
namespace Editor.Shell;


/// <summary>Visual style variants for buttons.</summary>
/// <seealso cref="Variant"/>
public enum ButtonStyle
{
    /// <summary>Primary filled button.</summary>
    Default,
    /// <summary>Secondary muted button.</summary>
    Secondary,
    /// <summary>Outlined border button.</summary>
    Outline,
    /// <summary>Transparent background, visible on hover.</summary>
    Ghost,
    /// <summary>Red destructive action button.</summary>
    Destructive,
    /// <summary>Styled as a hyperlink.</summary>
    Link
}

/// <summary>Visual style variants for badges.</summary>
/// <seealso cref="Variant"/>
public enum BadgeStyle
{
    /// <summary>Primary filled badge.</summary>
    Default,
    /// <summary>Secondary muted badge.</summary>
    Secondary,
    /// <summary>Outlined border badge.</summary>
    Outline,
    /// <summary>Red destructive badge.</summary>
    Destructive
}

/// <summary>Visual style variants for alerts.</summary>
/// <seealso cref="Variant"/>
public enum AlertStyle
{
    /// <summary>Neutral default alert.</summary>
    Default,
    /// <summary>Red danger/error alert.</summary>
    Danger,
    /// <summary>Blue informational alert.</summary>
    Info,
    /// <summary>Green success alert.</summary>
    Success,
    /// <summary>Yellow warning alert.</summary>
    Warning
}

/// <summary>Visual style variants for toggles.</summary>
/// <seealso cref="Variant"/>
public enum ToggleStyle
{
    /// <summary>Default filled toggle.</summary>
    Default,
    /// <summary>Outlined border toggle.</summary>
    Outline
}

/// <summary>Visual style variants for toasts.</summary>
/// <seealso cref="Variant"/>
public enum ToastStyle
{
    /// <summary>Neutral default toast.</summary>
    Default,
    /// <summary>Red destructive toast.</summary>
    Destructive
}

/// <summary>Tooltip / Popover / HoverCard placement side.</summary>
public enum Side
{
    /// <summary>Placed above the trigger.</summary>
    Top,
    /// <summary>Placed below the trigger.</summary>
    Bottom,
    /// <summary>Placed to the left of the trigger.</summary>
    Left,
    /// <summary>Placed to the right of the trigger.</summary>
    Right
}

/// <summary>
/// Strongly-typed variant resolver. Converts variant enums to their lowercase string names
/// used by the rendering layer.
/// <para>Usage: <c>Variant.From(ButtonStyle.Ghost)</c> or <c>Variant.None</c></para>
/// </summary>
public static class Variant
{
    /// <summary>No variant (uses the component default).</summary>
    public const string? None = null;

    /// <summary>Resolve a <see cref="ButtonStyle"/> to its string name.</summary>
    public static string From(ButtonStyle style) => style.ToVariantName();

    /// <summary>Resolve a <see cref="BadgeStyle"/> to its string name.</summary>
    public static string From(BadgeStyle style) => style.ToVariantName();

    /// <summary>Resolve a <see cref="AlertStyle"/> to its string name.</summary>
    public static string From(AlertStyle style) => style.ToVariantName();

    /// <summary>Resolve a <see cref="ToggleStyle"/> to its string name.</summary>
    public static string From(ToggleStyle style) => style.ToVariantName();

    /// <summary>Resolve a <see cref="ToastStyle"/> to its string name.</summary>
    public static string From(ToastStyle style) => style.ToVariantName();

    /// <summary>Resolve a <see cref="Side"/> to its string name.</summary>
    public static string From(Side side) => side.ToVariantName();

    /// <summary>Pass through a raw variant name string.</summary>
    public static string Custom(string name) => name;
}

/// <summary>Extension methods for converting variant enums to their string representation.</summary>
public static class VariantExtensions
{
    /// <summary>Converts a <see cref="ButtonStyle"/> to its lowercase CSS variant name.</summary>
    /// <param name="style">The button style variant.</param>
    /// <returns>The lowercase string representation (e.g. <c>"ghost"</c>, <c>"destructive"</c>).</returns>
    public static string ToVariantName(this ButtonStyle style) => style switch
    {
        ButtonStyle.Secondary => "secondary",
        ButtonStyle.Outline => "outline",
        ButtonStyle.Ghost => "ghost",
        ButtonStyle.Destructive => "destructive",
        ButtonStyle.Link => "link",
        _ => "default"
    };

    /// <summary>Converts a <see cref="BadgeStyle"/> to its lowercase CSS variant name.</summary>
    /// <param name="style">The badge style variant.</param>
    /// <returns>The lowercase string representation.</returns>
    public static string ToVariantName(this BadgeStyle style) => style switch
    {
        BadgeStyle.Secondary => "secondary",
        BadgeStyle.Outline => "outline",
        BadgeStyle.Destructive => "destructive",
        _ => "default"
    };

    /// <summary>Converts an <see cref="AlertStyle"/> to its lowercase CSS variant name.</summary>
    /// <param name="style">The alert style variant.</param>
    /// <returns>The lowercase string representation.</returns>
    public static string ToVariantName(this AlertStyle style) => style switch
    {
        AlertStyle.Danger => "danger",
        AlertStyle.Info => "info",
        AlertStyle.Success => "success",
        AlertStyle.Warning => "warning",
        _ => "default"
    };

    /// <summary>Converts a <see cref="ToggleStyle"/> to its lowercase CSS variant name.</summary>
    /// <param name="style">The toggle style variant.</param>
    /// <returns>The lowercase string representation.</returns>
    public static string ToVariantName(this ToggleStyle style) => style switch
    {
        ToggleStyle.Outline => "outline",
        _ => "default"
    };

    /// <summary>Converts a <see cref="ToastStyle"/> to its lowercase CSS variant name.</summary>
    /// <param name="style">The toast style variant.</param>
    /// <returns>The lowercase string representation.</returns>
    public static string ToVariantName(this ToastStyle style) => style switch
    {
        ToastStyle.Destructive => "destructive",
        _ => "default"
    };

    /// <summary>Converts a <see cref="Side"/> to its lowercase CSS placement name.</summary>
    /// <param name="side">The placement side.</param>
    /// <returns>The lowercase string representation (e.g. <c>"top"</c>, <c>"bottom"</c>).</returns>
    public static string ToVariantName(this Side side) => side switch
    {
        Side.Top => "top",
        Side.Bottom => "bottom",
        Side.Left => "left",
        Side.Right => "right",
        _ => "top"
    };
}

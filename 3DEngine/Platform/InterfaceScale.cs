namespace Engine;

/// <summary>
/// The size of the panels, the console and the overlay's text (<c>interface.scale</c>), as a share
/// of their size at a scale factor of one. The window's units the interface is laid out in already
/// carry the display's own scale factor, which is the setting's native value.
/// </summary>
internal static class InterfaceScale
{
    /// <summary>The share set, or null for the display's own scale factor.</summary>
    internal static float? Chosen { get; set; }

    /// <summary>
    /// What the interface's sizes in the window's units are multiplied by on a display of the scale
    /// factor given, the share set over the display's own, and 1 where none is set.
    /// </summary>
    internal static float Over(float displayScale) =>
        Chosen is { } chosen ? chosen / Math.Max(displayScale > 0f ? displayScale : 1f, 0.01f) : 1f;
}

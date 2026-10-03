namespace Engine;

/// <summary>
/// Immutable window properties with input validation.
/// Title defaults to "3D Engine" if blank; dimensions are clamped to a minimum of 1.
/// </summary>
public readonly record struct WindowData
{
    /// <summary>Window title bar text.</summary>
    public string Title { get; }

    /// <summary>Window width in pixels (≥ 1).</summary>
    public int Width { get; }

    /// <summary>Window height in pixels (≥ 1).</summary>
    public int Height { get; }

    /// <summary>Creates a new <see cref="WindowData"/> with the specified properties.</summary>
    /// <param name="title">Window title bar text. If blank, defaults to <c>"3D Engine"</c>.</param>
    /// <param name="width">Window width in pixels. Clamped to a minimum of 1.</param>
    /// <param name="height">Window height in pixels. Clamped to a minimum of 1.</param>
    public WindowData(string title, int width, int height)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "3D Engine" : title;
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
    }
}

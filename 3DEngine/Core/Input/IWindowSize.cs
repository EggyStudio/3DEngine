namespace Engine;

/// <summary>
/// The window's size in pixels as the platform keeps it, which the window inserts under this name
/// as well as its own, so input reads it without naming the platform.
/// </summary>
internal interface IWindowSize
{
    /// <summary>The window's width in pixels.</summary>
    int Width { get; }

    /// <summary>The window's height in pixels.</summary>
    int Height { get; }
}

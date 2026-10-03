namespace Engine;

/// <summary>Window action applied on application startup.</summary>
public enum WindowCommand
{
    /// <summary>Window starts hidden.</summary>
    Hide = 0,
    /// <summary>Window starts in normal (restored) state.</summary>
    Normal = 1,
    /// <summary>Window starts minimized.</summary>
    Minimize = 2,
    /// <summary>Window starts maximized.</summary>
    Maximize = 3,
    /// <summary>Window is shown (platform default placement).</summary>
    Show = 4,
    /// <summary>Window is restored from minimized/maximized state.</summary>
    Restore = 5,
}

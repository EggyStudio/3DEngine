namespace Engine;

/// <summary>Optional modifier keys that must be held when a toggle key is pressed.</summary>
[Flags]
public enum KeyModifier
{
    /// <summary>No modifier key required.</summary>
    None  = 0,
    /// <summary>Left or right Ctrl must be held.</summary>
    Ctrl  = 1 << 0,
    /// <summary>Left or right Shift must be held.</summary>
    Shift = 1 << 1,
    /// <summary>Left or right Alt must be held.</summary>
    Alt   = 1 << 2,
}

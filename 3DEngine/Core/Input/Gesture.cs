namespace Engine;

/// <summary>The gestures <see cref="Gestures"/> recognizes, as raylib's <c>Gesture</c> flags.</summary>
[Flags]
public enum Gesture
{
    /// <summary>No gesture.</summary>
    None = 0,
    /// <summary>A finger touched the screen.</summary>
    Tap = 1,
    /// <summary>A finger touched the screen twice in quick succession, near one place.</summary>
    DoubleTap = 2,
    /// <summary>A finger stays down without moving far.</summary>
    Hold = 4,
    /// <summary>A finger moves while down.</summary>
    Drag = 8,
    /// <summary>A finger flicked right and lifted.</summary>
    SwipeRight = 16,
    /// <summary>A finger flicked left and lifted.</summary>
    SwipeLeft = 32,
    /// <summary>A finger flicked up and lifted.</summary>
    SwipeUp = 64,
    /// <summary>A finger flicked down and lifted.</summary>
    SwipeDown = 128,
    /// <summary>Two fingers move toward each other.</summary>
    PinchIn = 256,
    /// <summary>Two fingers move apart.</summary>
    PinchOut = 512,
}

namespace Engine;

/// <summary>The pointer's shapes <see cref="Engine3D.SetMouseCursor"/> sets, as raylib's <c>MouseCursor</c> names them.</summary>
public enum MouseCursor
{
    /// <summary>The desktop's own pointer.</summary>
    Default,
    /// <summary>An arrow, which is the default pointer.</summary>
    Arrow,
    /// <summary>The text caret's bar, over a field to type in.</summary>
    IBeam,
    /// <summary>A crosshair, for aiming or picking a point.</summary>
    Crosshair,
    /// <summary>A pointing hand, over a link or a button.</summary>
    PointingHand,
    /// <summary>A double arrow left and right, for resizing across.</summary>
    ResizeEW,
    /// <summary>A double arrow up and down, for resizing up and down.</summary>
    ResizeNS,
    /// <summary>A double arrow from top left to bottom right.</summary>
    ResizeNWSE,
    /// <summary>A double arrow from top right to bottom left.</summary>
    ResizeNESW,
    /// <summary>Arrows every way, for moving something.</summary>
    ResizeAll,
    /// <summary>A barred circle, where nothing can be done.</summary>
    NotAllowed,
}

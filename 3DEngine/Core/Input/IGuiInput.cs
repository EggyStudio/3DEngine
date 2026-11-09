namespace Engine;

/// <summary>
/// The immediate-mode GUI's side of input the program makes itself, as the console's, so the GUI's
/// windows see a key, the pointer, a button, typed text and the wheel as they see the window's own.
/// </summary>
/// <remarks>
/// The GUI's plugin sets <see cref="Current"/>, which input made where no GUI was ever built leaves
/// at null, and each call does nothing while the GUI has no context.
/// </remarks>
internal interface IGuiInput
{
    /// <summary>The GUI that input the program makes reaches, or null where none was built.</summary>
    static IGuiInput? Current { get; set; }

    /// <summary>A key pressed or let go.</summary>
    void Key(Key key, bool down);

    /// <summary>The pointer at (<paramref name="x"/>, <paramref name="y"/>) in the window.</summary>
    void Pointer(float x, float y);

    /// <summary>A mouse button pressed or let go.</summary>
    void Button(MouseButton button, bool down);

    /// <summary>Text typed into the focused field.</summary>
    void Text(string text);

    /// <summary>The wheel turned by <paramref name="amount"/>, positive away from the user.</summary>
    void Wheel(float amount);
}

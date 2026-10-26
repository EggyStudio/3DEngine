namespace Engine;

/// <summary>
/// A line of text a person edits, with its caret and selection, which the program keeps and passes
/// to <see cref="E3D.EditText(ref TextEdit, Rectangle, int)"/> each frame, and to
/// <see cref="E3D.DrawTextEdit(TextEdit, Rectangle)"/> to draw it.
/// </summary>
/// <remarks>
/// An edit is a value rather than a widget the engine keeps, so a program holds as many as it has
/// fields, as it holds cameras. <see cref="Caret"/> and <see cref="Anchor"/> count the text's C#
/// <c>char</c>s before them, and editing moves them a character a reader sees at a time, so neither
/// lands between the two halves of a character past U+FFFF or between a letter and a mark set on
/// it.
/// </remarks>
public struct TextEdit
{
    /// <summary>The text, empty for none.</summary>
    public string Text;

    /// <summary>Where the caret stands, the number of the text's <c>char</c>s before it.</summary>
    public int Caret;

    /// <summary>The other end of the selection from <see cref="Caret"/>, the caret's own place where nothing is selected.</summary>
    public int Anchor;

    /// <summary>How many pixels the text is scrolled left, so the caret stays inside the box.</summary>
    public float Scroll;

    /// <summary>Whether the edit holds the keyboard, as a click in its box or Tab from the edit before it gives it.</summary>
    public bool Focused;

    // Whether the left button went down in the box and has not come up since, so the caret follows
    // the pointer and the selection runs from where it went down.
    internal bool Dragging;

    /// <summary>An edit holding <paramref name="text"/>, its caret at the end.</summary>
    public TextEdit(string text)
    {
        Text = text ?? "";
        Caret = Anchor = Text.Length;
        Scroll = 0;
        Focused = false;
        Dragging = false;
    }

    /// <summary>The selected text, empty where nothing is selected.</summary>
    public readonly string SelectedText
    {
        get
        {
            var text = Text ?? "";
            var (from, to) = (Math.Clamp(Math.Min(Caret, Anchor), 0, text.Length), Math.Clamp(Math.Max(Caret, Anchor), 0, text.Length));
            return text[from..to];
        }
    }
}

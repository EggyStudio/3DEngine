using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Api;

/// <summary>
/// Two edits of a form updated each frame in order, typed into, moved through, selected, copied and
/// pasted through the input a run with no window takes, as a person would.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class E3DTextEditTests : IDisposable
{
    private readonly App _app = new App(Config.Default with { Headless = true, HeadlessFps = 240 }).AddPlugin(new DefaultPlugins());
    private static readonly Rectangle NameBox = new(20, 20, 300, 40), SeedBox = new(20, 80, 300, 40);
    private TextEdit _name = new(""), _seed = new("");
    private bool _entered;

    public E3DTextEditTests() => UseApp(_app);

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private Input Input => _app.World.Resource<Input>();

    // A frame updating the name, up to twelve characters, then the seed, with no limit, and
    // drawing both.
    private void Frame()
    {
        BeginDrawing();
        _entered = EditText(ref _name, NameBox, 12);
        EditText(ref _seed, SeedBox, 0);
        DrawTextEdit(_name, NameBox);
        DrawTextEdit(_seed, SeedBox);
        EndDrawing();
    }

    // A key pressed for a frame with the others held, then all of them let go for one.
    private void Press(Key key, params Key[] held)
    {
        foreach (var other in held) Input.SetKey(other, true);
        Input.SetKey(key, true);
        Frame();
        Input.SetKey(key, false);
        foreach (var other in held) Input.SetKey(other, false);
        Frame();
    }

    private void Type(string text)
    {
        Input.AddText(text);
        Frame();
    }

    // Where the default font, as the name's box draws it, puts the place after text's characters.
    private static float After(string text) => NameBox.X + 24 / 4f + MeasureTextEx(GetFontDefault(24), text, 24, 0).X;

    private void Click(float x, float y)
    {
        Input.SetMousePosition((int)MathF.Round(x), (int)y);
        Input.SetMouseButton(MouseButton.Left, true);
        Frame();
        Input.SetMouseButton(MouseButton.Left, false);
        Frame();
    }

    [Fact]
    public void A_Click_Gives_An_Edit_The_Keyboard_And_Typing_Fills_It_To_Its_Length()
    {
        Frame();
        IsTextInputActive().Should().BeFalse("no edit holds the keyboard before a click");
        Type("ignored");
        _name.Text.Should().BeEmpty("an edit without the keyboard takes no typing");

        Click(NameBox.X + 10, NameBox.Y + 20);
        _name.Focused.Should().BeTrue();
        IsTextInputActive().Should().BeTrue("the name holds the keyboard");
        Type("Hello world, and more");
        _name.Text.Should().Be("Hello world,", "the name holds twelve characters");
        _seed.Text.Should().BeEmpty("the seed has not the keyboard");
        TextInputArea.Should().NotBeNull("the input method's window is placed at the caret");

        Input.SetKey(Key.Enter, true);
        Frame();
        _entered.Should().BeTrue("Enter is answered by the edit that holds the keyboard");
        Input.SetKey(Key.Enter, false);
        Frame();
        _entered.Should().BeFalse();

        Click(400, 300);
        _name.Focused.Should().BeFalse("a click outside the box takes the keyboard away");
        Frame();
        IsTextInputActive().Should().BeFalse();
    }

    [Fact]
    public void The_Caret_Moves_Selects_And_Edits_Through_The_Clipboard()
    {
        _name = new TextEdit("Hello world");
        Frame();
        Click(After("Hello"), NameBox.Y + 20);
        _name.Caret.Should().Be(5, "a click puts the caret between the characters under the pointer");

        Press(Key.Home);
        _name.Caret.Should().Be(0);
        for (int i = 0; i < 5; i++) Press(Key.Right, Key.LeftShift);
        _name.SelectedText.Should().Be("Hello", "Shift and an arrow select");
        Press(Key.C, Key.LeftControl);
        GetClipboardText().Should().Be("Hello");

        Press(Key.End);
        Press(Key.V, Key.LeftControl);
        _name.Text.Should().Be("Hello worldH", "a paste is held to the edit's twelve characters");
        Press(Key.Left, Key.LeftControl);
        _name.Caret.Should().Be(6, "Control and an arrow move by words");
        Press(Key.End, Key.LeftShift);
        Press(Key.X, Key.LeftControl);
        _name.Text.Should().Be("Hello ");
        GetClipboardText().Should().Be("worldH", "a cut puts the selection on the clipboard");

        Press(Key.Backspace);
        _name.Text.Should().Be("Hello");
        Press(Key.Home);
        Press(Key.Delete);
        _name.Text.Should().Be("ello");

        // A held key repeats as the system repeats it, each repeat a character.
        Press(Key.End);
        Input.SetKey(Key.Backspace, true);
        Frame();
        Input.SetKey(Key.Backspace, true, repeat: true);
        Frame();
        Input.SetKey(Key.Backspace, true, repeat: true);
        Frame();
        Input.SetKey(Key.Backspace, false);
        Frame();
        _name.Text.Should().Be("e");
    }

    [Fact]
    public void A_Drag_Selects_And_Tab_Passes_The_Keyboard_In_The_Order_The_Edits_Are_Updated()
    {
        _name = new TextEdit("Hello world");
        Frame();
        Input.SetMousePosition((int)MathF.Round(After("He")), (int)NameBox.Y + 20);
        Input.SetMouseButton(MouseButton.Left, true);
        Frame();
        Input.SetMousePosition((int)MathF.Round(After("Hello wo")), (int)NameBox.Y + 20);
        Frame();
        Input.SetMouseButton(MouseButton.Left, false);
        Frame();
        _name.SelectedText.Should().Be("llo wo", "a drag selects from where the button went down");

        Press(Key.Tab);
        _name.Focused.Should().BeFalse();
        _seed.Focused.Should().BeTrue("Tab gives the keyboard to the edit updated after the name");
        IsTextInputActive().Should().BeTrue();
        Type("42");
        _seed.Text.Should().Be("42");
        _name.Text.Should().Be("Hello world");

        Press(Key.Tab);
        _name.Focused.Should().BeTrue("past the last edit Tab comes back to the first");
        _name.SelectedText.Should().Be("Hello world", "an edit Tab gives the keyboard to selects its text");
        Press(Key.Tab, Key.LeftShift);
        _seed.Focused.Should().BeTrue("Shift and Tab go back to the edit before, past the first to the last");
        _name.Focused.Should().BeFalse();
    }
}

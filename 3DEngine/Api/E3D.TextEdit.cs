using System.Globalization;
using System.Numerics;
using System.Text;

namespace Engine;

// -- How EditText edits a line of text and DrawTextEdit draws it, the calls themselves beside the
// typed text's in E3D.Input.cs.

public static partial class E3D
{
    // The edits' state across frames, the frame they were last updated in, how many were updated in
    // it and in the frame before, the call Tab passed the keyboard to and the frame it was passed
    // in, and the frame an edit last held it, kept for one app so a test's next app starts clean.
    private static App? _editApp;
    private static ulong _editFrame;
    private static int _editCalls, _editCallsBefore;
    private static (int Call, ulong Frame)? _keyboardPassed;
    private static ulong? _keyboardHeld;
    // When the caret last moved or the text last changed, which the caret's blink starts from, so
    // it shows while a person types or moves it.
    private static double _caretMoved;

    // The size the default font is drawn at in a box, six tenths of its height, at least the 10
    // DrawText draws at.
    private static int TextEditSize(Rectangle box) => Math.Clamp((int)(box.Height * 0.6f), 10, 256);

    // Where an edit's text starts in its box, a quarter of the font's size in from the left and
    // centered from top to bottom, scrolled left by the edit's scroll.
    private static Vector2 TextEditOrigin(TextEdit edit, Rectangle box, float fontSize) =>
        new(box.X + fontSize / 4 - edit.Scroll, box.Y + (box.Height - fontSize) / 2);

    // How far from the text's start its first chars end, with no spacing between characters as
    // DrawText draws them.
    private static float TextEditX(Font font, string text, int chars, float fontSize) =>
        chars <= 0 ? 0 : MeasureTextEx(font, text[..Math.Min(chars, text.Length)], fontSize, 0).X;

    private static bool UpdateTextEdit(ref TextEdit edit, Rectangle box, int most, Font font, float fontSize)
    {
        var frame = Res<Time>().FrameCount;
        if (!ReferenceEquals(_editApp, _app))
            (_editApp, _editFrame, _editCalls, _editCallsBefore, _keyboardPassed, _keyboardHeld) = (_app, frame, 0, 0, null, null);
        if (frame != _editFrame)
        {
            _editCallsBefore = frame == _editFrame + 1 ? _editCalls : 0;
            (_editFrame, _editCalls) = (frame, 0);
        }
        var call = _editCalls++;
        edit.Text ??= "";
        edit.Caret = Math.Clamp(edit.Caret, 0, edit.Text.Length);
        edit.Anchor = Math.Clamp(edit.Anchor, 0, edit.Text.Length);
        var moved = false;

        // Tab passes the keyboard on to the edit updated after it this frame, or past the last to
        // the first the next frame, and Shift and Tab to the one before it the next frame, every
        // edit having been updated by then; one passed it selects its whole text, and takes no keys
        // until the next frame, where the Tab that passed it would pass it on again.
        var handed = false;
        if (_keyboardPassed is { } passed)
        {
            var target = passed.Frame == frame ? (passed.Call > call ? -1 : passed.Call == call ? call : -2)
                : passed.Frame + 1 == frame && _editCallsBefore > 0 ? (passed.Call % _editCallsBefore + _editCallsBefore) % _editCallsBefore
                : -3;
            if (target == -3) _keyboardPassed = null;
            else if (target == call)
            {
                _keyboardPassed = null;
                (edit.Focused, edit.Anchor, edit.Caret, moved, handed) = (true, 0, edit.Text.Length, true, passed.Frame == frame);
            }
        }

        var shift = IsKeyDown(Key.LeftShift) || IsKeyDown(Key.RightShift);
        var mouse = GetMousePosition();
        if (IsMouseButtonPressed(MouseButton.Left))
        {
            edit.Focused = CheckCollisionPointRec(mouse, box);
            edit.Dragging = edit.Focused;
            if (edit.Focused)
            {
                edit.Caret = TextEditPlaceAt(edit, box, font, fontSize, mouse.X);
                if (!shift) edit.Anchor = edit.Caret;
                moved = true;
            }
        }
        else if (edit.Dragging)
        {
            if (IsMouseButtonDown(MouseButton.Left))
            {
                var at = TextEditPlaceAt(edit, box, font, fontSize, mouse.X);
                moved |= at != edit.Caret;
                edit.Caret = at;
            }
            else edit.Dragging = false;
        }

        var entered = false;
        if (edit.Focused) _keyboardHeld = frame;
        if (edit.Focused && !handed)
        {
            var command = IsKeyDown(Key.LeftControl) || IsKeyDown(Key.RightControl) || IsKeyDown(Key.LeftSuper) || IsKeyDown(Key.RightSuper);
            static bool Again(Key key) => IsKeyPressed(key) || IsKeyPressedRepeat(key);
            var typed = new StringBuilder();
            for (int c; (c = GetCharPressed()) != 0;)
                if (c >= 32 && c != 127 && c <= 0x10FFFF && c is < 0xD800 or > 0xDFFF)
                    typed.Append(char.ConvertFromUtf32(c));
            var before = (edit.Text, edit.Caret, edit.Anchor);
            if (typed.Length > 0 && !command) TextEditInsert(ref edit, typed.ToString(), most);
            if (command && IsKeyPressed(Key.A)) (edit.Anchor, edit.Caret) = (0, edit.Text.Length);
            if (command && (IsKeyPressed(Key.C) || IsKeyPressed(Key.X)) && edit.Caret != edit.Anchor)
            {
                SetClipboardText(edit.SelectedText);
                if (IsKeyPressed(Key.X)) TextEditInsert(ref edit, "", most);
            }
            // The clipboard's line breaks and tabs become spaces and its other control characters
            // go, since the edit holds one line.
            if (command && Again(Key.V))
                TextEditInsert(ref edit, new string(GetClipboardText().Select(c => c is '\n' or '\r' or '\t' ? ' ' : c).Where(c => !char.IsControl(c)).ToArray()), most);
            if (Again(Key.Backspace) || Again(Key.Delete))
            {
                if (edit.Caret == edit.Anchor)
                    edit.Anchor = Again(Key.Backspace) ? TextEditStep(edit.Text, edit.Caret, -1, command) : TextEditStep(edit.Text, edit.Caret, 1, command);
                TextEditInsert(ref edit, "", most);
            }
            foreach (var (key, way) in new[] { (Key.Left, -1), (Key.Right, 1) })
                if (Again(key))
                {
                    // An arrow without Shift leaves a selection at the end it points to.
                    edit.Caret = edit.Caret != edit.Anchor && !shift
                        ? (way < 0 ? Math.Min(edit.Caret, edit.Anchor) : Math.Max(edit.Caret, edit.Anchor))
                        : TextEditStep(edit.Text, edit.Caret, way, command);
                    if (!shift) edit.Anchor = edit.Caret;
                }
            if (IsKeyPressed(Key.Home) || IsKeyPressed(Key.End))
            {
                edit.Caret = IsKeyPressed(Key.Home) ? 0 : edit.Text.Length;
                if (!shift) edit.Anchor = edit.Caret;
            }
            moved |= before != (edit.Text, edit.Caret, edit.Anchor);
            entered = IsKeyPressed(Key.Enter) || IsKeyPressed(Key.KpEnter);
            if (IsKeyPressed(Key.Tab))
            {
                (edit.Focused, edit.Dragging) = (false, false);
                _keyboardPassed = (shift ? call - 1 : call + 1, frame);
            }
        }

        // The caret kept inside the box, the text scrolled no further than its end allows.
        var inside = Math.Max(0, box.Width - fontSize / 2);
        var caretX = TextEditX(font, edit.Text, edit.Caret, fontSize);
        if (caretX - edit.Scroll > inside) edit.Scroll = caretX - inside;
        if (caretX < edit.Scroll) edit.Scroll = caretX;
        edit.Scroll = Math.Clamp(edit.Scroll, 0, Math.Max(0, TextEditX(font, edit.Text, edit.Text.Length, fontSize) - inside));
        if (edit.Focused)
            SetTextInputArea(box, (int)(TextEditOrigin(edit, box, fontSize).X + caretX - box.X));
        if (moved) _caretMoved = GetTime();
        return entered;
    }

    private static bool TextInputHeld()
    {
        if (!ReferenceEquals(_editApp, _app) || !TryRes<Time>(out var time)) return false;
        var frame = time.FrameCount;
        return _keyboardHeld is { } held && held + 1 >= frame || _keyboardPassed is { } passed && passed.Frame + 1 >= frame;
    }

    // Puts text in place of the selection, or at the caret where nothing is selected, as much of it
    // as keeps the whole within most characters a reader sees, or all of it for most of 0 or less.
    private static void TextEditInsert(ref TextEdit edit, string text, int most)
    {
        var (from, to) = (Math.Min(edit.Caret, edit.Anchor), Math.Max(edit.Caret, edit.Anchor));
        var kept = edit.Text[..from] + edit.Text[to..];
        if (most > 0)
        {
            var room = Math.Max(0, most - new StringInfo(kept).LengthInTextElements);
            var elements = new StringInfo(text);
            if (elements.LengthInTextElements > room) text = elements.SubstringByTextElements(0, room);
        }
        edit.Text = kept[..from] + text + kept[from..];
        edit.Caret = edit.Anchor = from + text.Length;
    }

    // The place a character a reader sees away from at, toward the end for a way of 1 and the
    // start for -1, or past the next word and the spaces before it where by words, as Control and
    // an arrow move.
    private static int TextEditStep(string text, int at, int way, bool byWords)
    {
        var starts = StringInfo.ParseCombiningCharacters(text);
        int Next(int from) => way > 0 ? starts.FirstOrDefault(s => s > from, text.Length) : starts.LastOrDefault(s => s < from, 0);
        if (!byWords) return Next(at);
        var place = at;
        while (place != (way > 0 ? text.Length : 0) && char.IsWhiteSpace(text[way > 0 ? place : place - 1])) place = Next(place);
        while (place != (way > 0 ? text.Length : 0) && !char.IsWhiteSpace(text[way > 0 ? place : place - 1])) place = Next(place);
        return place;
    }

    // The place between characters nearest a point x pixels across the window.
    private static int TextEditPlaceAt(TextEdit edit, Rectangle box, Font font, float fontSize, float x)
    {
        var along = x - TextEditOrigin(edit, box, fontSize).X;
        var (place, width) = (0, 0f);
        foreach (var start in StringInfo.ParseCombiningCharacters(edit.Text).Skip(1).Append(edit.Text.Length))
        {
            var next = TextEditX(font, edit.Text, start, fontSize);
            if (along < (width + next) / 2) return place;
            (place, width) = (start, next);
        }
        return place;
    }

    private static void DrawEdit(TextEdit edit, Rectangle box, Font font, float fontSize, Color text, Color background)
    {
        var value = edit.Text ?? "";
        DrawRectangleRec(box, background);
        DrawRectangleLinesEx(box, edit.Focused ? 2 : 1, text);
        var origin = TextEditOrigin(edit, box, fontSize);
        // Kept inside the box's border and inside any scissor the program set, which it gets back.
        var outer = DrawList.Scissor;
        var inner = PixelsOf((int)box.X + 2, (int)box.Y + 2, Math.Max(0, (int)box.Width - 4), Math.Max(0, (int)box.Height - 4));
        if (outer is { } o)
        {
            var (left, top) = (Math.Max(o.X, inner.X), Math.Max(o.Y, inner.Y));
            inner = new ScissorRect(left, top, Math.Max(0, Math.Min(o.X + o.Width, inner.X + inner.Width) - left),
                Math.Max(0, Math.Min(o.Y + o.Height, inner.Y + inner.Height) - top));
        }
        DrawList.SetScissor(inner);
        if (edit.Caret != edit.Anchor)
        {
            var from = TextEditX(font, value, Math.Min(edit.Caret, edit.Anchor), fontSize);
            var to = TextEditX(font, value, Math.Max(edit.Caret, edit.Anchor), fontSize);
            DrawRectangleRec(new Rectangle(origin.X + from, origin.Y, to - from, fontSize), Fade(text, 0.3f));
        }
        DrawTextEx(font, value, origin, fontSize, 0, text);
        // The caret shows for half of each second from when it last moved.
        if (edit.Focused && (GetTime() - _caretMoved) % 1.0 < 0.5)
            DrawRectangleRec(new Rectangle(origin.X + TextEditX(font, value, edit.Caret, fontSize), origin.Y, Math.Max(1, fontSize / 12), fontSize), text);
        DrawList.SetScissor(outer);
    }
}

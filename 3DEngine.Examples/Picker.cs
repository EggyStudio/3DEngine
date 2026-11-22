using static Engine.E3D;

namespace Engine.Examples;

/// <summary>
/// A window listing every example under its folder's name, with a field above that keeps those
/// whose names hold what is typed, which the program opens when it is run without an example's
/// name, drawn with the engine's own shapes, text and text edit.
/// </summary>
/// <remarks>
/// The arrow keys and Page Up and Page Down move the choice, the wheel scrolls the list, and Enter
/// or a double click runs the one chosen. The list opens again as the example's window closes,
/// with the same filter and choice, so one example after another is run from it. Escape closes
/// nothing in it, as its window has no exit key, and closing the list ends the program. A headless
/// run, with no window to pick in, prints the names instead.
/// </remarks>
internal sealed class Picker(IReadOnlyCollection<string> names)
{
    // The list's look, a dark ground, the field and list a step lighter, folders' names in blue.
    private static readonly Color Ground = new(28, 30, 36);
    private static readonly Color Panel = new(36, 39, 47);
    private static readonly Color Line = new(62, 66, 78);
    private static readonly Color Ink = new(222, 226, 232);
    private static readonly Color Faint = new(128, 134, 146);
    private static readonly Color Folder = new(110, 168, 254);
    private static readonly Color Chosen = new(48, 76, 122);
    private const int Margin = 16, Row = 24, TextSize = 18, FieldTop = 62, FieldHeight = 34;
    private const int ListTop = FieldTop + FieldHeight + 12;

    private TextEdit _filter = new("");
    private string? _chosen;
    private float _scroll;
    private (string Name, double At)? _click;

    /// <summary>The name of the example picked, or null where the list was closed.</summary>
    public string? Pick()
    {
        SetConfigFlags(ConfigFlags.WindowResizable);
        InitWindow(900, 600, "3DEngine examples");
        SetTargetFPS(60);
        if (GetApp().World.Resource<Config>().Headless)
        {
            Console.WriteLine("This run has no window to pick an example in. The examples are:");
            foreach (var name in names) Console.WriteLine($"  {name}");
            CloseWindow();
            return null;
        }
        string? picked = null;
        while (picked is null && !WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(Ground);
            picked = List();
            EndDrawing();
        }
        CloseWindow();
        return picked;
    }

    // The list drawn over the whole window, answering the name picked this frame, if any.
    private string? List()
    {
        var (width, height) = (GetScreenWidth(), GetScreenHeight());
        // While the console or a panel of the engine's holds the keyboard, the list leaves the keys
        // to it, so a line typed there does not filter the list.
        var keys = !IsExitKeyHeld();
        DrawText($"{names.Count} examples", Margin, 12, 20, Ink);
        DrawText("Type to keep those whose names hold it, move with the arrow keys, and run one with Enter or a double click.", Margin, 38, 13, Faint);

        var field = new Rectangle(Margin, FieldTop, width - 2 * Margin, FieldHeight);
        _filter.Focused = keys;
        var entered = keys && EditText(ref _filter, field, 64, GetFontDefault(20), 20);
        DrawTextEdit(_filter, field, GetFontDefault(20), 20, Ink, Panel);
        if (_filter.Text.Length == 0) DrawText("shaders, camera, sponza", Margin + 5, FieldTop + (FieldHeight - 20) / 2, 20, Faint);

        // The names that hold the filter, in order under their folders, the folders in the order
        // the program lists them, and the lines drawn, each folder's name over its examples'.
        var filter = _filter.Text.Trim();
        var shown = names.Where(name => name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .GroupBy(FolderOf).SelectMany(folder => folder.Order(StringComparer.Ordinal)).ToList();
        if (_chosen is null || !shown.Contains(_chosen)) _chosen = shown.FirstOrDefault();
        var lines = shown.GroupBy(FolderOf)
            .SelectMany(folder => folder.Select(name => (Text: name, Folder: false)).Prepend((Text: $"{folder.Key} ({folder.Count()})", Folder: true)))
            .ToList();

        var area = new Rectangle(Margin, ListTop, width - 2 * Margin, Math.Max(Row, height - ListTop - Margin));
        var picked = entered ? _chosen : null;
        if (keys && _chosen is not null)
        {
            static bool Again(Key key) => IsKeyPressed(key) || IsKeyPressedRepeat(key);
            var page = Math.Max(1, (int)(area.Height / Row) - 2);
            var step = (Again(Key.Down) ? 1 : 0) - (Again(Key.Up) ? 1 : 0) + (Again(Key.Pagedown) ? page : 0) - (Again(Key.Pageup) ? page : 0);
            if (step != 0)
            {
                _chosen = shown[Math.Clamp(shown.IndexOf(_chosen) + step, 0, shown.Count - 1)];
                // The choice kept in view with the line over it, its folder's name where it leads.
                var y = lines.FindIndex(line => !line.Folder && line.Text == _chosen) * Row;
                if (y - Row < _scroll) _scroll = y - Row;
                else if (y + Row > _scroll + area.Height) _scroll = y + Row - area.Height;
            }
        }
        var mouse = GetMousePosition();
        var overList = CheckCollisionPointRec(mouse, area);
        if (overList) _scroll -= GetMouseWheelMove() * Row * 3;
        var content = lines.Count * Row;
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, content - area.Height));

        DrawRectangleRec(area, Panel);
        BeginScissorMode((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
        for (var i = (int)(_scroll / Row); i < lines.Count && i * Row - _scroll < area.Height; i++)
        {
            var (text, folder) = lines[i];
            var row = new Rectangle(area.X, area.Y + i * Row - _scroll, area.Width, Row);
            var (x, y) = ((int)row.X + 10, (int)row.Y + (Row - TextSize) / 2);
            if (folder)
            {
                DrawText(text, x, y, TextSize, Folder);
                var middle = (int)row.Y + Row / 2;
                DrawLine(x + MeasureText(text, TextSize) + 10, middle, (int)(row.X + row.Width) - 14, middle, Line);
                continue;
            }
            var over = overList && CheckCollisionPointRec(mouse, row);
            if (text == _chosen) DrawRectangleRec(row, Chosen);
            else if (over) DrawRectangleRec(row, Fade(Chosen, 0.4f));
            DrawText(text, x, y, TextSize, Ink);
            if (over && IsMouseButtonPressed(MouseButton.Left))
            {
                // A second click on the same name within four tenths of a second runs it.
                if (_click is { } click && click.Name == text && GetTime() - click.At < 0.4) picked = text;
                (_chosen, _click) = (text, (text, GetTime()));
            }
        }
        EndScissorMode();
        if (content > area.Height)
        {
            var thumb = Math.Max(Row, area.Height * area.Height / content);
            var top = area.Y + (area.Height - thumb) * _scroll / (content - area.Height);
            DrawRectangleRec(new Rectangle(area.X + area.Width - 7, top + 2, 4, thumb - 4), Line);
        }
        DrawRectangleLinesEx(area, 1, Line);
        if (shown.Count == 0) DrawText($"No example's name holds \"{filter}\".", (int)area.X + 10, (int)area.Y + 8, TextSize, Faint);
        return picked;
    }

    // An example's folder, the word of its name before the first underscore, as core or shaders.
    private static string FolderOf(string name) => name[..Math.Max(0, name.IndexOf('_'))];
}

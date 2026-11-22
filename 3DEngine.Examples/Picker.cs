using System.Numerics;
using ImGuiNET;
using static Engine.E3D;

namespace Engine.Examples;

/// <summary>
/// A window listing every example under its folder's name, with a box above that keeps those whose
/// names hold what is typed, which the program opens when it is run without an example's name.
/// </summary>
/// <remarks>
/// The arrow keys move the choice and Enter or a double click runs it, and the list opens again as
/// the example's window closes, with the same filter and choice, so one example after another is
/// run from it. Closing the list ends the program. A run that draws no ImGui, as a headless one,
/// prints the names instead.
/// </remarks>
internal sealed class Picker(IReadOnlyCollection<string> names)
{
    private string _filter = "";
    private string? _chosen;

    /// <summary>The name of the example picked, or null where the list was closed.</summary>
    public string? Pick()
    {
        InitWindow(900, 600, "3DEngine examples");
        SetTargetFPS(60);
        string? picked = null;
        var first = true;
        while (picked is null && !WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(new Color(28, 30, 36));
            if (ImGui.GetCurrentContext() == IntPtr.Zero)
            {
                EndDrawing();
                Console.WriteLine("This run draws no ImGui to pick an example in. The examples are:");
                foreach (var name in names) Console.WriteLine($"  {name}");
                break;
            }
            picked = List(first);
            first = false;
            EndDrawing();
        }
        CloseWindow();
        return picked;
    }

    // The list drawn over the whole window, answering the name picked this frame, if any.
    private string? List(bool first)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        ImGui.Begin("Examples", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
        ImGui.TextUnformatted($"{names.Count} examples. Type to keep those whose names hold it, move with the arrow keys, and run one with Enter or a double click.");
        if (first) ImGui.SetKeyboardFocusHere();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##filter", "shaders, camera, sponza", ref _filter, 64);

        // The names that hold the filter, in order under their folders, the folders in the order
        // the program lists them, so the arrow keys move down the list as it is drawn.
        var shown = names.Where(name => name.Contains(_filter.Trim(), StringComparison.OrdinalIgnoreCase))
            .GroupBy(Folder).SelectMany(folder => folder.Order(StringComparer.Ordinal)).ToList();
        if (_chosen is null || !shown.Contains(_chosen)) _chosen = shown.FirstOrDefault();
        string? picked = null;
        var moved = false;
        if (_chosen is not null)
        {
            var at = shown.IndexOf(_chosen);
            var step = (ImGui.IsKeyPressed(ImGuiKey.DownArrow) ? 1 : 0) - (ImGui.IsKeyPressed(ImGuiKey.UpArrow) ? 1 : 0);
            if (step != 0)
            {
                _chosen = shown[Math.Clamp(at + step, 0, shown.Count - 1)];
                moved = true;
            }
            if (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter)) picked = _chosen;
        }
        if (shown.Count == 0) ImGui.TextDisabled($"No example's name holds \"{_filter.Trim()}\".");

        ImGui.BeginChild("##names", Vector2.Zero, ImGuiChildFlags.Borders);
        foreach (var folder in shown.GroupBy(Folder))
        {
            ImGui.SeparatorText($"{folder.Key} ({folder.Count()})");
            foreach (var name in folder)
            {
                if (ImGui.Selectable(name, name == _chosen, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    _chosen = name;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) picked = name;
                }
                if (moved && name == _chosen) ImGui.SetScrollHereY();
            }
        }
        ImGui.EndChild();
        ImGui.End();
        return picked;
    }

    // An example's folder, the word of its name before the first underscore, as core or shaders.
    private static string Folder(string name) => name[..Math.Max(0, name.IndexOf('_'))];
}

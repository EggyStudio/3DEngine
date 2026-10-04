namespace Engine;

public static partial class Engine3D
{
    /// <summary>Sets a number of the program's own in the frame profile, which <c>e3d command profile</c> reports, as a stress test's count.</summary>
    public static void SetProfileValue(string name, double value)
    {
        if (TryRes<FrameProfile>(out var profile)) profile.Set(name, value);
    }

    /// <summary>A measured average from the frame profile in milliseconds, as <c>"work"</c>, <c>"frame"</c> or <c>"gpu.models"</c>, or 0 before it was measured.</summary>
    public static double GetProfileAverage(string name) =>
        TryRes<FrameProfile>(out var profile) ? profile.Average(name) : 0;

    /// <summary>
    /// Draws the frame profile in an ImGui window of its own, which a program shows while it is
    /// worked on: the frame and its work, the program's own values, and each group of averages,
    /// largest first, which <c>e3d command profile</c> reports too.
    /// </summary>
    /// <remarks>Called between <c>BeginDrawing</c> and <c>EndDrawing</c>, as any ImGui window is.</remarks>
    public static void DrawProfileWindow()
    {
        if (!TryRes<FrameProfile>(out var profile)) return;
        var (values, groups) = profile.Snapshot();
        ImGuiNET.ImGui.SetNextWindowSize(new System.Numerics.Vector2(420, 360), ImGuiNET.ImGuiCond.FirstUseEver);
        if (!ImGuiNET.ImGui.Begin("Frame profile"))
        {
            ImGuiNET.ImGui.End();
            return;
        }
        ImGuiNET.ImGui.Text($"{profile.Average("frame"):0.00} ms a frame, {profile.Average("work"):0.00} ms of it work, over {profile.Frames} frames");
        foreach (var (name, value) in values) ImGuiNET.ImGui.Text($"{name}: {value:0.###}");
        foreach (var (group, averages) in groups)
        {
            if (group is "frame" or "work") continue;
            // The renderer's and the program's open, the long lists of stages and systems closed.
            var open = group is "cpu" or "gpu" or "program" or "render" ? ImGuiNET.ImGuiTreeNodeFlags.DefaultOpen : ImGuiNET.ImGuiTreeNodeFlags.None;
            if (!ImGuiNET.ImGui.CollapsingHeader($"{group} ({averages.Sum(a => a.Milliseconds):0.00} ms)###{group}", open)) continue;
            foreach (var (name, milliseconds) in averages)
                ImGuiNET.ImGui.Text($"{milliseconds,8:0.000} ms  {(name.Length > group.Length ? name[(group.Length + 1)..] : name)}");
        }
        ImGuiNET.ImGui.End();
    }

    private static void Profile(string name, TimeSpan elapsed)
    {
        if (TryRes<FrameProfile>(out var profile)) profile.Add(name, elapsed.TotalMilliseconds);
    }
}

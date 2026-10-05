using ImGuiNET;

namespace Engine;

/// <summary>HUD overlay showing real-time performance metrics: FPS, frame time, and 1-second peak.</summary>
/// <remarks>
/// Renders into an ImGui <c>"Performance"</c> window.  The peak FPS counter resets every second
/// based on <see cref="Time.ElapsedSeconds"/>. Hidden until F2 is pressed.
/// </remarks>
/// <seealso cref="Time"/>
[Behavior]
internal struct PerformanceHud
{
    private static double _peakFps;
    private static double _peakWindowStart;

    /// <summary>Draws the performance overlay each render frame.</summary>
    /// <param name="ctx">Behavior context providing access to <see cref="Time"/> and other resources.</param>
    [OnRender]
    [ToggleKey(Key.F2, DefaultEnabled = false)]
    public static void Draw(BehaviorContext ctx)
    {
        var time = ctx.Time;
        double fps = time.Fps;

        // Reset peak every second
        if (time.ElapsedSeconds - _peakWindowStart >= 1.0)
        {
            _peakFps = 0;
            _peakWindowStart = time.ElapsedSeconds;
        }

        if (fps > _peakFps)
            _peakFps = fps;

        ImGui.Begin("Performance", ImGuiWindowFlags.NoSavedSettings);
        ImGui.Text($"FPS:       {time.SmoothedFps:0}");
        ImGui.Text($"Peak FPS:  {_peakFps:0}");
        ImGui.Text($"Frame:     {time.DeltaSeconds * 1000.0:0.00} ms");
        ImGui.Text($"Frames:    {time.FrameCount}");
        ImGui.End();
    }
}

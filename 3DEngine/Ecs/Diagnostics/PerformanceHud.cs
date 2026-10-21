using ImGuiNET;

namespace Engine;

/// <summary>HUD overlay showing real-time performance metrics, FPS, frame time, and 1-second peak.</summary>
/// <remarks>
/// Renders into an ImGui <c>"Performance"</c> window.  The peak FPS counter resets every second
/// based on <see cref="Time.ElapsedSeconds"/>. Hidden until F3 is pressed, the key
/// <see cref="E3D.SetProfileWindowKey"/> moves, read each frame rather than fixed by an attribute so
/// a program can move it, F2 being the screenshot's.
/// </remarks>
/// <seealso cref="Time"/>
[Behavior]
internal struct PerformanceHud
{
    private static double _peakFps;
    private static double _peakWindowStart;
    private static bool _shown;

    /// <summary>Whether the window is shown, which its key flips.</summary>
    internal static bool Shown => _shown;

    /// <summary>
    /// Flips the window where its key went down this frame, and answers whether it is shown, before
    /// the system's context is built, so an app without a clock, as a test's may be, builds none
    /// while the window is hidden.
    /// </summary>
    public static bool Toggled(World world)
    {
        if (world.TryGetResource<DebugKeys>(out var keys) && keys.ProfileWindow != Key.Null
            && world.TryGetResource<Input>(out var input) && input.KeyPressed(keys.ProfileWindow))
            _shown = !_shown;
        return _shown;
    }

    /// <summary>Draws the performance overlay each render frame.</summary>
    /// <param name="ctx">Behavior context providing access to <see cref="Time"/> and other resources.</param>
    [OnRender]
    [RunIf(nameof(Toggled))]
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

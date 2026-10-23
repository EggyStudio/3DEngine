using System.Numerics;

namespace Engine;

public static partial class E3D
{
    // -- The interface F1 hides, and the debug overlay F3 shows over every program.

    /// <summary>Opens the scope of the program's interface, which F1 hides, and answers whether it is drawn this frame, false while it is hidden.</summary>
    /// <remarks>
    /// <para>
    /// The program draws its HUD between this and <see cref="EndUi"/>, and while F1 has hidden it the
    /// engine drops every 2D shape and text drawn inside, whatever the program does. ImGui draws
    /// whatever a frame submits, so a program's own ImGui windows go inside an <c>if</c> on the
    /// answer, as they go inside one on <c>ImGui.Begin</c>, and cost nothing while hidden:
    /// </para>
    /// <code>
    /// if (BeginUi())
    /// {
    ///     ImGui.Begin("Inventory");
    ///     ImGui.End();
    /// }
    /// DrawText($"Health {health}", 10, 10, 20, Color.White);   // dropped while hidden
    /// EndUi();
    /// </code>
    /// <para>
    /// What is drawn outside the scope stays whatever F1 says, as a pause screen, a menu or an
    /// inventory a game shows over its world, as Minecraft hides its HUD and never its screens. The
    /// world draws on, and the debug overlay stays. <see cref="SetUiHiddenKey"/> moves the key.
    /// </para>
    /// </remarks>
    public static bool BeginUi()
    {
        var hidden = DebugOverlay.Of(World).UiHidden;
        (_uiScopes ??= new()).Push((hidden, hidden ? DrawList.Here() : default));
        return !hidden;
    }

    /// <summary>Closes the scope <see cref="BeginUi"/> opened, dropping what was drawn inside it while the interface is hidden.</summary>
    /// <exception cref="InvalidOperationException">No scope is open.</exception>
    public static void EndUi()
    {
        if (_uiScopes is null || !_uiScopes.TryPop(out var scope))
            throw new InvalidOperationException("EndUi closes the scope BeginUi opened, and none is open.");
        if (scope.Hidden) DrawList.Rewind(scope.Here);
    }

    /// <summary>Whether F1 has hidden the interface drawn inside <see cref="BeginUi"/>.</summary>
    public static bool IsUiHidden() => TryRes<DebugOverlay>(out var overlay) && overlay.UiHidden;

    /// <summary>
    /// Sets the key that hides and shows the program's interface, F1 until moved, as Minecraft hides
    /// its HUD, or turns it off with <see cref="Key.Null"/>.
    /// </summary>
    /// <remarks>
    /// The key is not taken while a field of an ImGui window has the caret, and the program sees the
    /// press as it sees any key, so a program that takes F1 for itself moves or turns this off.
    /// </remarks>
    public static void SetUiHiddenKey(Key key) => World.GetOrInsertResource(static () => new DebugKeys()).HideUi = key;

    /// <summary>
    /// Sets the key that shows and hides the engine's debug overlay, F3 until moved, as Minecraft's
    /// debug screen, or turns it off with <see cref="Key.Null"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The overlay is two columns of lines in the engine's font, each on a dark half-clear backing,
    /// with no window, header or border. On the left are the engine's version, the frame rate and
    /// time, the camera's place, facing and field of view and the world's counts, and on the right
    /// the system, the CPU and memory, the GPU, its driver and memory, the display, and the
    /// renderer's passes, draw calls, triangles, lights, shadows, global illumination and GPU time.
    /// </para>
    /// <para>
    /// A strip of the last 240 frames' times stands under the frame rate, as the RivaTuner overlay
    /// draws its graph, each frame a bar green to 60 frames a second, yellow to 30 and red past it,
    /// with the last frame's time, the average and the one percent low beside it, and the GPU's time
    /// a frame under them. A program's sections (<see cref="AddDebugOverlaySection"/>) follow the
    /// engine's lines on the left.
    /// </para>
    /// <para>
    /// The key is taken as it is let go, where no other key went down while it was held, so a
    /// program's own chords on it stand and never open the overlay, and the program sees the press
    /// as any key. Shift held with it shows the profiler's pass timings under the right column, the
    /// slowest first, and the overlay with them, as Minecraft's Shift+F3 does. The overlay is drawn
    /// over everything else the frame draws in 2D, and F1 leaves it.
    /// </para>
    /// </remarks>
    public static void SetDebugOverlayKey(Key key) => World.GetOrInsertResource(static () => new DebugKeys()).Overlay = key;

    /// <summary>
    /// Adds a section of the program's own to the debug overlay's left column, under the engine's
    /// lines and any section added before, in place of one of the same name.
    /// </summary>
    /// <param name="name">The section's heading, which it is taken away by.</param>
    /// <param name="lines">What it says, asked four times a second while the overlay shows.</param>
    /// <remarks>
    /// A world generator's seed and the chunk the player stands in, or an enemy count, read as the
    /// engine's own lines are. A section whose lines throw says why in a line of its own rather than
    /// stopping the overlay. The sections last while the app runs.
    /// </remarks>
    /// <example>
    /// <code>
    /// AddDebugOverlaySection("World", () => [$"Seed {seed}", $"Chunk {chunk.X} {chunk.Y}"]);
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public static void AddDebugOverlaySection(string name, Func<IEnumerable<string>> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(lines);
        DebugOverlay.Of(World).AddSection(name, lines);
    }

    /// <summary>Takes a section of the debug overlay away by its name, and says whether there was one.</summary>
    public static bool RemoveDebugOverlaySection(string name) => DebugOverlay.Of(World).RemoveSection(name);

    // The interface's scopes open on this thread, each with whether it was hidden as it opened and
    // where the draw list stood, which a hidden scope's close rewinds to.
    [ThreadStatic]
    private static Stack<(bool Hidden, (int Vertices, int Indices, int Batches, int LastCount) Here)>? _uiScopes;

    // The overlay's look, Minecraft's, light gray text on black at half, a line's backing three
    // pixels wider than its text on each side, the columns two pixels in from the window's edges.
    private const int OverlayText = 16, OverlayLine = OverlayText + 2, OverlayPad = 3, OverlayMargin = 2;
    private static readonly Color OverlayInk = new(224, 224, 224, 255), OverlayBacking = new(0, 0, 0, 128);

    // The strip, a pixel a frame across and a bit more than a pixel a millisecond up, its top 50
    // milliseconds, so a frame of 20 a second fills it, and its bars' colors to 60 frames a second,
    // to 30 and past it.
    private const int StripHeight = 48;
    private const float StripTop = 50;
    private static readonly Color StripFast = new(80, 220, 100, 255), StripSlow = new(240, 200, 60, 255), StripStall = new(240, 80, 70, 255),
        StripRule = new(160, 160, 160, 160);

    // Draws the overlay's lines into the window in screen space, after everything else the frame
    // drew in 2D, whatever 3D mode, target or shader the program left set.
    internal static void DrawDebugOverlay(DebugOverlay.Said said, ReadOnlySpan<float> times)
    {
        DrawList.SetTarget(0);
        DrawList.SetShader(0, default);
        DrawList.SetScissor(null);
        DrawList.SetBlend(BlendMode.Alpha);
        DrawList.SetCull(CullMode.None);
        DrawList.SetModel(Matrix4x4.Identity);
        var (width, height) = (GetScreenWidth(), GetScreenHeight());
        DrawList.SetTransform(Matrix4x4.CreateOrthographicOffCenter(0, width, 0, height, -1, 1), depthTest: false);

        var y = OverlayMargin;
        foreach (var line in said.Head) y = OverlayLineAt(line, OverlayMargin, y, alignRight: false, width);

        // The strip with the frames' times beside it, then the rest of the left column under both.
        DrawStrip(OverlayMargin, y, times);
        var beside = y;
        foreach (var line in said.Beside) beside = OverlayLineAt(line, OverlayMargin + DebugOverlay.StripFrames + OverlayPad, beside, alignRight: false, width);
        y = Math.Max(y + StripHeight + OverlayPad, beside);
        foreach (var line in said.Left) y = OverlayLineAt(line, OverlayMargin, y, alignRight: false, width);
        y = OverlayMargin;
        foreach (var line in said.Right) y = OverlayLineAt(line, width - OverlayMargin, y, alignRight: true, width);
    }

    // The frames' times as bars, a column a frame with the newest at the right, on black at half with
    // faint rows at 60 and 30 frames a second, as the RivaTuner overlay draws its graph. The strip
    // holds no frame yet at its left until four seconds have run.
    private static void DrawStrip(int x, int y, ReadOnlySpan<float> times)
    {
        DrawRectangle(x, y, DebugOverlay.StripFrames, StripHeight, OverlayBacking);
        foreach (var fps in (ReadOnlySpan<float>)[60, 30])
            DrawRectangle(x, y + StripHeight - (int)MathF.Round(1000 / fps / StripTop * StripHeight), DebugOverlay.StripFrames, 1, StripRule);

        var empty = DebugOverlay.StripFrames - times.Length;
        for (int i = 0; i < times.Length; i++)
        {
            var ms = times[i];
            var bar = Math.Clamp((int)MathF.Ceiling(ms / StripTop * StripHeight), 1, StripHeight);
            var color = ms <= 1000f / 60 + 0.5f ? StripFast : ms <= 1000f / 30 + 0.5f ? StripSlow : StripStall;
            DrawRectangle(x + empty + i, y + StripHeight - bar, 1, bar, color);
        }
    }

    // A line at its column's edge, its backing behind it, an empty line a gap of a line's height,
    // and answers where the next line goes.
    private static int OverlayLineAt(string line, int edge, int y, bool alignRight, int width)
    {
        if (line.Length > 0)
        {
            var wide = MeasureText(line, OverlayText) + 2 * OverlayPad;
            var x = alignRight ? edge - wide : edge;
            DrawRectangle(x, y, wide, OverlayLine, OverlayBacking);
            DrawText(line, x + OverlayPad, y + 1, OverlayText, OverlayInk);
        }
        return y + OverlayLine;
    }
}

using System.Numerics;
using Diagnostics;

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
    /// inventory a game shows over its world, so F1 hides the HUD and never a screen the player
    /// opened. The world draws on, and the debug overlay stays. <see cref="SetUiHiddenKey"/> moves
    /// the key.
    /// </para>
    /// </remarks>
    public static bool BeginUi()
    {
        var hidden = DiagnosticsBinding.Of(World).Keys.InterfaceHidden;
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
    public static bool IsUiHidden() => TryRes<DiagnosticsBinding>(out var binding) && binding.Keys.InterfaceHidden;

    /// <summary>
    /// Sets the key that hides and shows the program's interface, F1 until moved, or turns it off
    /// with <see cref="Key.Null"/>.
    /// </summary>
    /// <remarks>
    /// The key is the setting <c>keys.hide_interface</c>, which the console sets too. It is taken
    /// as it comes up where no other key went down while it was held, and the program sees the
    /// press as it sees any key, so a program that takes F1 for itself moves or turns this off.
    /// </remarks>
    public static void SetUiHiddenKey(Key key) => DiagnosticsBinding.Of(World).Bind(KeyAction.HideInterface, key);

    /// <summary>
    /// Sets the key that shows and hides the engine's debug overlay, F3 until moved, or turns it
    /// off with <see cref="Key.Null"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The overlay is two columns of lines in the engine's font, each on a dark half-clear backing,
    /// with no window, header or border. On the left are the engine's version, the frame rate and
    /// time, the camera's place, facing and field of view and the world's counts, and on the right
    /// the system, the CPU and memory, the GPU, its driver and memory, the display, and the
    /// renderer's passes, draw calls, triangles, lights, shadows, global illumination and GPU time.
    /// In a window too narrow for the two side by side, a line of the right column that would meet
    /// the left's goes down under it, the lines after it following, and a line wider than the
    /// window breaks after a comma.
    /// </para>
    /// <para>
    /// A strip of the last 240 frames' times stands under the frame rate, each frame a bar green to
    /// 60 frames a second, yellow to 30 and red past it, with the GPU's time a frame beside it. The
    /// frame rate's line gives the last frame's time, the average and the one percent low. A
    /// program's sections (<see cref="AddDebugOverlaySection"/>) follow the engine's lines on the
    /// left.
    /// </para>
    /// <para>
    /// The key is the setting <c>keys.overlay</c>, which the console sets too. It is taken as it is
    /// let go, where no other key went down while it was held, so a program's own chords on it
    /// stand and never open the overlay, and the program sees the press as any key. Shift held with
    /// it shows the profiler's pass timings under the right column, the slowest first, and the
    /// overlay with them. The overlay is drawn over everything else the frame draws in 2D, and F1
    /// leaves it.
    /// </para>
    /// </remarks>
    public static void SetDebugOverlayKey(Key key) => DiagnosticsBinding.Of(World).Bind(KeyAction.Overlay, key);

    /// <summary>
    /// Adds a section of the program's own to the debug overlay's left column, under the engine's
    /// lines and any section added before, in place of one of the same name.
    /// </summary>
    /// <param name="name">The section's heading, which it is taken away by.</param>
    /// <param name="lines">What it says, asked four times a second while the overlay shows.</param>
    /// <remarks>
    /// A section holds what only the program knows, as the seed its world was generated from or an
    /// enemy count, drawn in the overlay's look. A section whose lines throw is a line saying it
    /// failed and why, and the overlay goes on. The sections last while the app runs.
    /// </remarks>
    /// <example>
    /// <code>
    /// AddDebugOverlaySection("World", () => [$"Seed {seed}", $"Enemies {enemies.Count}"]);
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public static void AddDebugOverlaySection(string name, Func<IEnumerable<string>> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(lines);
        DiagnosticsBinding.Of(World).AddSection(name, lines);
    }

    /// <summary>Takes a section of the debug overlay away by its name, and says whether there was one.</summary>
    public static bool RemoveDebugOverlaySection(string name) => DiagnosticsBinding.Of(World).RemoveSection(name);

    // The interface's scopes open on this thread, each with whether it was hidden as it opened and
    // where the draw list stood, which a hidden scope's close rewinds to.
    [ThreadStatic]
    private static Stack<(bool Hidden, (int Vertices, int Indices, int Batches, int LastCount) Here)>? _uiScopes;

    // The overlay's look, light gray text on black at half, a line's backing three pixels wider
    // than its text on each side, the columns two pixels in from the window's edges and eight
    // pixels at least between them.
    private const int OverlayText = 16, OverlayLine = OverlayText + 2, OverlayPad = 3, OverlayMargin = 2, OverlayGap = 8;
    private static readonly Color OverlayInk = new(224, 224, 224, 255), OverlayBacking = new(0, 0, 0, 128);

    // The strip, a pixel a frame across and a bit more than a pixel a millisecond up, its top 50
    // milliseconds, so a frame of 20 a second fills it, and its bars' colors to 60 frames a second,
    // to 30 and past it.
    private const int StripHeight = 48;
    private const float StripTop = 50;
    private static readonly Color StripFast = new(80, 220, 100, 255), StripSlow = new(240, 200, 60, 255), StripStall = new(240, 80, 70, 255),
        StripRule = new(160, 160, 160, 160);

    /// <summary>
    /// A line of the overlay where it is drawn, with its backing's left edge, top and width.
    /// </summary>
    internal readonly record struct OverlayBox(string Text, int X, int Y, int Wide);

    // Draws the overlay's lines into the window in screen space, after everything else the frame
    // drew in 2D, whatever 3D mode, target or shader the program left set, where LayOutOverlay puts
    // them.
    internal static void DrawDebugOverlay(OverlayModel overlay)
    {
        DrawList.SetTarget(0);
        DrawList.SetShader(0, default);
        DrawList.SetScissor(null);
        DrawList.SetBlend(BlendMode.Alpha);
        DrawList.SetCull(CullMode.None);
        DrawList.SetModel(Matrix4x4.Identity);
        DrawList.SetScene(false);
        // Laid out in the window's units over interface.scale, so the whole overlay is drawn larger
        // or smaller by it.
        var scale = TryRes<AppWindow>(out var window) ? InterfaceScale.Over(window.Sdl.DisplayScale) : 1f;
        var (width, height) = ((int)(GetScreenWidth() / scale), (int)(GetScreenHeight() / scale));
        DrawList.SetTransform(Matrix4x4.CreateOrthographicOffCenter(0, width, 0, height, -1, 1), depthTest: false);

        var (lines, strip) = LayOutOverlay(overlay, width);
        DrawStrip(OverlayMargin, strip, [.. overlay.FrameTimes]);
        foreach (var line in lines)
        {
            DrawRectangle(line.X, line.Y, line.Wide, OverlayLine, OverlayBacking);
            DrawText(line.Text, line.X + OverlayPad, line.Y + 1, OverlayText, OverlayInk);
        }
    }

    /// <summary>
    /// Places the overlay's lines in a window of the width given, and answers them with the strip's
    /// top. The head's lines stand at the top left with the strip under them and the lines beside
    /// it next to it, the left column's lines under both, and the right column's at the top right.
    /// </summary>
    /// <remarks>
    /// A line of the right column that would meet a line of the left, or the strip, goes down
    /// under what it would meet, and the lines after it follow, so a narrow window keeps the
    /// columns apart with every line whole and in its order, and a wide one has them side by side
    /// as they are. A line wider than the window is broken after its last comma that fits, or its
    /// last space, and its pieces stand one under the other.
    /// </remarks>
    internal static (List<OverlayBox> Lines, int Strip) LayOutOverlay(OverlayModel overlay, int width)
    {
        var lines = new List<OverlayBox>();
        int Left(string line, int x, int y)
        {
            foreach (var piece in Pieces(line, width - OverlayMargin - x))
            {
                if (piece.Length > 0) lines.Add(new(piece, x, y, MeasureText(piece, OverlayText) + 2 * OverlayPad));
                y += OverlayLine;
            }
            return y;
        }

        var y = OverlayMargin;
        foreach (var line in overlay.Lines(OverlayPlace.Head)) y = Left(line, OverlayMargin, y);
        var strip = y;
        var beside = y;
        foreach (var line in overlay.Lines(OverlayPlace.Beside)) beside = Left(line, OverlayMargin + OverlayModel.StripFrames + OverlayPad, beside);
        y = Math.Max(y + StripHeight + OverlayPad, beside);
        foreach (var line in overlay.Lines(OverlayPlace.Left)) y = Left(line, OverlayMargin, y);

        // What the right column keeps clear of, the left's lines and the strip, each as its right
        // edge with the gap, its top and its bottom.
        var taken = lines.Select(line => (Right: line.X + line.Wide + OverlayGap, Top: line.Y, Bottom: line.Y + OverlayLine)).ToList();
        taken.Add((OverlayMargin + OverlayModel.StripFrames + OverlayGap, strip, strip + StripHeight));

        y = OverlayMargin;
        foreach (var line in overlay.Lines(OverlayPlace.Right))
            foreach (var piece in Pieces(line, width - 2 * OverlayMargin))
            {
                if (piece.Length > 0)
                {
                    var wide = MeasureText(piece, OverlayText) + 2 * OverlayPad;
                    var x = width - OverlayMargin - wide;
                    // Down under each thing met, until the line meets none, which ends, as each
                    // step goes below what it met.
                    while (taken.FindIndex(t => t.Right > x && t.Top < y + OverlayLine && y < t.Bottom) is var met and >= 0) y = taken[met].Bottom;
                    lines.Add(new(piece, x, y, wide));
                }
                y += OverlayLine;
            }
        return (lines, strip);
    }

    // A line in pieces whose backings are no wider than the room given, each broken after the last
    // comma that fits, or at the last space, and a word wider than the room a piece of its own.
    private static List<string> Pieces(string line, int room)
    {
        var pieces = new List<string>();
        while (MeasureText(line, OverlayText) + 2 * OverlayPad > room)
        {
            int Fitting(string mark)
            {
                for (var at = line.LastIndexOf(mark, StringComparison.Ordinal); at > 0; at = line.LastIndexOf(mark, at - 1, StringComparison.Ordinal))
                    if (MeasureText(line[..(at + mark.Length - 1)], OverlayText) + 2 * OverlayPad <= room) return at + mark.Length - 1;
                return -1;
            }
            var cut = Fitting(", ") is var comma and > 0 ? comma : Fitting(" ");
            // Past the line's indent, so an indented line's first word is a piece, not its indent.
            if (cut < 0) cut = line.IndexOf(' ', line.Length - line.TrimStart().Length);
            if (cut < 0) break;
            pieces.Add(line[..cut].TrimEnd());
            line = line[cut..].TrimStart();
        }
        pieces.Add(line);
        return pieces;
    }

    // The frames' times as bars, a column a frame with the newest at the right, on black at half
    // with faint rows at 60 and 30 frames a second. The strip holds no frame yet at its left until
    // four seconds have run.
    private static void DrawStrip(int x, int y, ReadOnlySpan<float> times)
    {
        DrawRectangle(x, y, OverlayModel.StripFrames, StripHeight, OverlayBacking);
        foreach (var fps in (ReadOnlySpan<float>)[60, 30])
            DrawRectangle(x, y + StripHeight - (int)MathF.Round(1000 / fps / StripTop * StripHeight), OverlayModel.StripFrames, 1, StripRule);

        var empty = OverlayModel.StripFrames - times.Length;
        for (int i = 0; i < times.Length; i++)
        {
            var ms = times[i];
            var bar = Math.Clamp((int)MathF.Ceiling(ms / StripTop * StripHeight), 1, StripHeight);
            var color = ms <= 1000f / 60 + 0.5f ? StripFast : ms <= 1000f / 30 + 0.5f ? StripSlow : StripStall;
            DrawRectangle(x + empty + i, y + StripHeight - bar, 1, bar, color);
        }
    }
}

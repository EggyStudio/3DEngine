using System.Globalization;
using static Engine.E3D;

namespace Engine.Examples;

public static partial class ScenesSponza
{
    private static readonly Rectangle FetchButton = new(40, 250, 300, 40);
    private static readonly Rectangle CancelButton = new(360, 250, 140, 40);

    // The screen the example opens on where Sponza is not fetched. It names the command that
    // fetches it, and its button fetches it here, the download running on the thread pool while
    // the screen draws its stage and its megabytes, a second press doing nothing until it ends and
    // the other button stopping it. A fetch that fails says why, the button back for another try.
    // Answers whether the pack is fetched, or false where the window was closed first.
    private static bool FetchScreen(ScenePack? pack, string? problem)
    {
        Task<string?>? running = null;
        CancellationTokenSource? stop = null;
        var told = new Told();
        var failed = problem;
        while (!WindowShouldClose())
        {
            if (running is { IsCompleted: true })
            {
                failed = running.GetAwaiter().GetResult();
                (running, stop) = (null, Ended(stop));
                if (failed is null) return true;
            }
            var mouse = GetMousePosition();
            var pressed = IsMouseButtonPressed(MouseButton.Left);
            if (pack is not null && running is null && pressed && CheckCollisionPointRec(mouse, FetchButton))
            {
                failed = null;
                stop = new CancellationTokenSource();
                told.Report(new ScenePackProgress(ScenePackStage.Downloading, 0, pack.Size));
                running = ScenePacks.FetchAsync(pack, told, stop.Token);
            }
            else if (running is not null && pressed && CheckCollisionPointRec(mouse, CancelButton))
                stop!.Cancel();

            BeginDrawing();
            ClearBackground(new Color(30, 30, 34));
            DrawText("Sponza is not fetched.", 40, 120, 20, Color.RayWhite);
            DrawText("Fetch it here, or run ./e3d scenes fetch intel-sponza", 40, 160, 20, Color.RayWhite);
            DrawText("in the engine's checkout.", 40, 186, 20, Color.RayWhite);
            if (pack is not null)
            {
                Button(FetchButton, running is null ? $"Fetch it, {Megabytes(pack.Size)} MB" : "Fetching", running is null, mouse);
                if (running is not null) Button(CancelButton, stop!.IsCancellationRequested ? "Stopping" : "Cancel", !stop.IsCancellationRequested, mouse);
            }
            if (running is not null)
            {
                var step = told.Last;
                DrawText(Said(step), 40, 316, 20, Color.RayWhite);
                DrawRectangle(40, 348, 720, 12, new Color(60, 60, 66, 255));
                DrawRectangle(40, 348, (int)(720 * step.Fraction), 12, new Color(120, 170, 230, 255));
            }
            else if (failed is not null)
                Wrapped(failed, 40, 316, 720, 18, new Color(240, 150, 130, 255));
            EndDrawing();
        }
        // Closed as it fetches, the fetch is stopped and waited for, so it leaves no part of the pack.
        if (running is not null)
        {
            stop!.Cancel();
            running.GetAwaiter().GetResult();
            Ended(stop);
        }
        return false;
    }

    private static CancellationTokenSource? Ended(CancellationTokenSource? stop)
    {
        stop?.Dispose();
        return null;
    }

    private static string Said(ScenePackProgress step) => step.Stage switch
    {
        ScenePackStage.Downloading => $"Downloading, {Megabytes(step.Bytes)} of {Megabytes(step.Total)} MB",
        ScenePackStage.Checking => "Checking the pack's SHA-256 against its manifest's",
        ScenePackStage.Unpacking => $"Unpacking, {Megabytes(step.Bytes)} of {Megabytes(step.Total)} MB",
        _ => "Fetched",
    };

    private static string Megabytes(long bytes) => (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture);

    // A button of a label, lighter under the pointer and dim where it takes no press.
    private static void Button(Rectangle area, string label, bool live, System.Numerics.Vector2 mouse)
    {
        var over = live && CheckCollisionPointRec(mouse, area);
        DrawRectangleRec(area, !live ? new Color(52, 52, 58, 255) : over ? new Color(92, 128, 176, 255) : new Color(70, 100, 140, 255));
        DrawRectangleLinesEx(area, 1, live ? new Color(150, 180, 220, 255) : new Color(80, 80, 88, 255));
        var width = MeasureText(label, 20);
        DrawText(label, (int)(area.X + (area.Width - width) / 2), (int)(area.Y + (area.Height - 20) / 2), 20, live ? Color.RayWhite : Color.Gray);
    }

    // Text broken at its spaces into lines no wider than the width given.
    private static void Wrapped(string text, int x, int y, int width, int size, Color color)
    {
        var line = "";
        foreach (var word in text.Split(' '))
        {
            var longer = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && MeasureText(longer, size) > width)
            {
                DrawText(line, x, y, size, color);
                (line, y) = (word, y + size + 6);
            }
            else line = longer;
        }
        if (line.Length > 0) DrawText(line, x, y, size, color);
    }

    // The last a fetch told, from the thread it runs on, for the frame to read.
    private sealed class Told : IProgress<ScenePackProgress>
    {
        private readonly Lock _gate = new();
        private ScenePackProgress _last;

        public ScenePackProgress Last
        {
            get { lock (_gate) return _last; }
        }

        public void Report(ScenePackProgress value)
        {
            lock (_gate) _last = value;
        }
    }
}

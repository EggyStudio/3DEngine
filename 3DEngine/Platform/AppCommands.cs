namespace Engine;

/// <summary>The console command that asks the app to close.</summary>
internal static class AppCommands
{
    [Command("app.quit", "Asks the app to close, as the window's close button does")]
    internal static string Quit()
    {
        // Logged with the process that asked, since an app closed by a script that meant another
        // ends as one that closed of its own does.
        Log.Category("Engine.AppExit").Info($"app.quit was asked by {ConsoleHost.Caller ?? "a caller that did not say which process it is"}, so the app closes.");
        ConsoleHost.World!.GetOrInsertResource(() => new AppExit()).Requested = true;
        return "closing";
    }
}

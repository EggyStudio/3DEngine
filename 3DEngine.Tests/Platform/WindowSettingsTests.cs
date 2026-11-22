using Diagnostics;
using FluentAssertions;
using SDL3;

namespace Engine.Tests.Platform;

/// <summary>
/// The window's page of settings on a window of SDL's offscreen driver, each setting's round
/// through the settings file, given to the window before it is shown and written back as it is
/// set, a mode the system refuses answered with the reason and the mode before kept, and the page
/// only reading while the window is hidden.
/// </summary>
/// <remarks>
/// A window of the offscreen driver draws through VK_EXT_headless_surface, and closing it quits
/// SDL for the whole process, so these run alone in the window collection.
/// </remarks>
[Collection("Window")]
[Trait("Category", "Render")]
public sealed class WindowSettingsTests
{
    private const string Title = "window settings test";

    // A line run as the e3d client runs it, with the world lent.
    private static string Run(App app, string line)
    {
        using (ConsoleHost.Lend(app.World, app)) return ConsoleHost.Run(line).Text;
    }

    private static void Frames(App app, int count)
    {
        for (var i = 0; i < count; i++)
        {
            app.BeginFrame();
            app.EndFrame();
        }
    }

    // A program of the title opened on the offscreen driver, its settings file at the path.
    private static App Open(string path, bool hidden = false)
    {
        var given = DiagnosticsBinding.SettingsPath;
        DiagnosticsBinding.SettingsPath = config => config.WindowData.Title == Title ? path : given(config);
        Restore = () => DiagnosticsBinding.SettingsPath = given;
        SDL.SetHint(SDL.Hints.VideoDriver, "offscreen");
        return new App(Config.Default.WithWindow(Title, 320, 240) with { Hidden = hidden }).AddPlugin(new DefaultPlugins());
    }

    private static Action Restore = () => { };

    private static void Close(App app)
    {
        app.Shutdown();
        SDL.ResetHint(SDL.Hints.VideoDriver);
        Restore();
    }

    private static (int Width, int Height) Size(IntPtr window)
    {
        SDL.GetWindowSize(window, out var width, out var height);
        return (width, height);
    }

    [NeedsHeadlessWindowFact]
    public void Each_Setting_Of_The_Window_Goes_Round_Through_The_File_Given_Before_The_Window_Is_Shown()
    {
        using var folder = new TestFolder("window-settings-");
        var path = Path.Combine(folder.Path, "settings.txt");
        File.WriteAllText(path, "window.mode borderless\nwindow.resizable off\nwindow.width 480\nwindow.height 272\nwindow.vsync on\n");
        var app = Open(path);
        try
        {
            var window = app.World.Resource<AppWindow>();
            var sdl = window.Sdl.Window;
            WindowModes.Of(sdl).Should().Be(WindowMode.Borderless, "the file's mode is given before the window is shown");
            WindowModes.WindowedSize(window).Should().Be((480, 272), "the size as a window is kept while the window covers its screen");

            Frames(app, 2);
            Run(app, "window.mode").Should().StartWith("window.mode is Borderless");
            Run(app, "window.resizable").Should().StartWith("window.resizable is on",
                "SDL's offscreen driver gives a window no edges to fix, so the file's line is refused and leaves the file");
            Run(app, "window.width").Should().StartWith("window.width is 480");
            Run(app, "window.height").Should().StartWith("window.height is 272");
            Run(app, "window.vsync").Should().StartWith("window.vsync is on");

            Run(app, "window.mode windowed").Should().Be("window.mode is Windowed.");
            Size(sdl).Should().Be((480, 272), "leaving the mode the window takes the size it has as a window");
            Run(app, "window.mode borderless").Should().Be("window.mode is Borderless.");
            Run(app, "window.width 512").Should().Be("window.width is 512.", "the size as a window is set while the window covers its screen");
            WindowModes.WindowedSize(window).Should().Be((512, 272), "the size is kept for the window to take as it leaves the mode");
            Run(app, "window.width").Should().StartWith("window.width is 512");
            Run(app, "window.mode windowed");
            Size(sdl).Should().Be((512, 272));
            Run(app, "window.resizable off").Should().Be("The system kept the window resizable.",
                "a driver that answers yes and leaves the window is caught");
            (SDL.GetWindowFlags(sdl) & SDL.WindowFlags.Resizable).Should().NotBe((SDL.WindowFlags)0);
            Run(app, "window.mode borderless");

            File.ReadAllLines(path).Should().BeEquivalentTo(["window.mode Borderless", "window.width 512", "window.height 272", "window.vsync on"],
                "each setting set is written, and one back at its default leaves the file");
            Run(app, "window.monitor 1").Should().Be("There is one screen, so the window stays on it.",
                "the offscreen driver has one screen, so the window's screen only reads");
            Run(app, "window.monitor").Should().StartWith("window.monitor is 0");
        }
        finally
        {
            Close(app);
        }

        File.ReadAllLines(path).Should().BeEquivalentTo(["window.mode Borderless", "window.width 512", "window.height 272", "window.vsync on"],
            "the file written as the program ends holds what it held");
    }

    [NeedsHeadlessWindowFact]
    public void A_Mode_The_System_Refuses_Answers_With_The_Reason_And_Keeps_The_Mode_Before()
    {
        using var folder = new TestFolder("window-refused-");
        var path = Path.Combine(folder.Path, "settings.txt");
        File.WriteAllText(path, "window.mode fullscreen\n");
        var app = Open(path);
        try
        {
            var sdl = app.World.Resource<AppWindow>().Sdl.Window;
            WindowModes.Of(sdl).Should().Be(WindowMode.Windowed, "the screen of SDL's offscreen driver lists no mode for a fullscreen window, so the file's line is refused");
            Frames(app, 1);
            Run(app, "window.mode borderless").Should().Be("window.mode is Borderless.");
            Run(app, "window.mode fullscreen").Should().StartWith("The system refused the fullscreen window, the screen offering no mode for it.");
            Run(app, "window.mode").Should().StartWith("window.mode is Borderless", "the mode before is kept");
            WindowModes.Of(sdl).Should().Be(WindowMode.Borderless);
            File.ReadAllLines(path).Should().Equal(["window.mode Borderless"], "the refused line is not written again, nor the refused mode");
        }
        finally
        {
            Close(app);
        }
    }

    [NeedsHeadlessWindowFact]
    public void A_Hidden_Window_Has_Its_Page_Only_Read_With_The_Reason()
    {
        using var folder = new TestFolder("window-hidden-");
        var app = Open(Path.Combine(folder.Path, "settings.txt"), hidden: true);
        try
        {
            Frames(app, 1);
            const string hidden = "The window is hidden, as --hidden or E3D_HIDDEN asks, so its settings only read.";
            foreach (var name in new[] { "window.mode", "window.resizable", "window.width", "window.height", "window.monitor", "window.vsync" })
            {
                using (ConsoleHost.Lend(app.World, app)) ConsoleHost.Registry.FindSetting(name)!.Locked.Should().Be(hidden, "{0} only reads while the window is hidden", name);
                Run(app, $"reset {name}").Should().Be(hidden);
            }
            Run(app, "window.mode fullscreen").Should().Be(hidden);
            WindowModes.Of(app.World.Resource<AppWindow>().Sdl.Window).Should().Be(WindowMode.Windowed);
        }
        finally
        {
            Close(app);
        }
    }
}

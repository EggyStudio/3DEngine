using Diagnostics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.DiagnosticsHost;

/// <summary>
/// The engine's settings in the process's registry, a page of the panel for each first word of
/// their names, a set reaching what the lent world holds or refused where it holds none, and the
/// settings file of a shown program read as it starts and written as a saved setting changes, in a
/// headless run.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class EngineSettingsTests : IDisposable
{
    private const string Title = "settings test";

    public EngineSettingsTests() =>
        UseApp(new App(Config.Default.WithWindow(Title, 64, 64) with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 })
            .AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static void Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            BeginDrawing();
            ClearBackground(Color.DarkBlue);
            EndDrawing();
        }
    }

    // A line run as the e3d client runs it, with the world lent.
    private static string Run(string line)
    {
        using (ConsoleHost.Lend(GetApp().World, GetApp())) return ConsoleHost.Run(line).Text;
    }

    [Fact]
    public void Every_Value_A_Line_Or_A_Variable_Set_Is_A_Setting_On_The_Panel_Page_Of_Its_First_Word()
    {
        var pages = EngineSettings.All.GroupBy(setting => setting.Name[..setting.Name.IndexOf('.')])
            .ToDictionary(page => page.Key, page => page.Count());
        pages.Should().Equal(new Dictionary<string, int>
        {
            ["ao"] = 8, ["gi"] = 26, ["field"] = 1, ["window"] = 6, ["render"] = 5, ["interface"] = 1, ["imgui"] = 1, ["time"] = 1,
            ["live"] = 1, ["log"] = 1, ["scenes"] = 1, ["sky"] = 1, ["run"] = 10,
        });
        EngineSettings.All.Should().OnlyContain(setting => ReferenceEquals(ConsoleHost.Registry.FindSetting(setting.Name), setting),
            "each setting is reached from a line by its name");
        EngineSettings.All.Where(setting => setting.Saved).Select(setting => setting.Name)
            .Should().BeEquivalentTo(["window.mode", "window.resolution", "window.resizable", "window.monitor", "window.vsync", "imgui.viewports",
                    "render.scale", "render.filter", "render.aspect", "render.fit", "render.fps_limit", "interface.scale"],
                "the file keeps what a player chooses for the machine and none of the run's");
        EngineSettings.All.Where(setting => setting.ReadOnly).Select(setting => setting.Name).Should().BeEquivalentTo(
            ["run.frames", "run.serve", "run.headless", "run.offscreen", "run.hidden", "run.seed", "window.samples", "run.shuffle",
                "run.sessions", "run.validation", "run.slangc"],
            "how a tool started the run only reads, since no start reads a value set while the program runs");
        EngineSettings.All.Should().NotContain(setting => setting.AtNextStart, "no setting of the engine's waits for a start");
        foreach (var gone in new[] { "gi.toggle", "ao.set", "window.vsync", "window.monitor" })
            ConsoleHost.Registry.FindCommand(gone).Should().BeNull("{0} only set a value, which a setting holds", gone);

        Frames(1);
        var binding = DiagnosticsBinding.Of(GetApp().World);
        binding.Panel.Open = true;
        binding.Panel.Rows.Where(row => row.Kind == PanelRowKind.Page).Select(row => row.Label)
            .Should().Contain([.. pages.Keys, "keys"], "the panel draws a page for each first word");
    }

    [Fact]
    public void A_Set_Reaches_What_The_Lent_World_Holds_And_One_The_Run_Cannot_Apply_Or_That_Only_Reads_Is_Refused()
    {
        Frames(1);
        Run("ao.method circular").Should().Be("ao.method is Circular.");
        GetApp().World.Resource<AmbientOcclusionSettings>().Method.Should().Be(AmbientOcclusionMethod.CircularOccluders);
        Run("ao.slope 100").Should().Be("ao.slope takes 1 to 64, not 100.");
        Run("time.frame 0.025").Should().Be("time.frame is 0.025.");
        GetApp().World.Resource<Time>().FrameSeconds.Should().Be(0.025);
        Run("reset time.frame");
        GetApp().World.Resource<Time>().FrameSeconds.Should().Be(0, "the default reads the clock");
        Run("run.frames 123456").Should().Be("run.frames is read only.");
        Run("run.frames").Should().StartWith("run.frames is 0", "the run's config gives it");
        Run("run.headless").Should().StartWith("run.headless is on");
        Run("gi.show tiles").Should().Be("The program runs no global illumination, which SetGlobalIllumination turns on.");
        Run("gi.show").Should().StartWith("gi.show is None", "a refused set leaves the value as it read");
        Run("window.vsync on").Should().Be("The program has no window and draws no frames.", "a headless run has no swapchain");
        Run("window.mode fullscreen").Should().Be("The program has no window and draws no frames.", "a headless run has no window to cover the screen");
        Run("sky.hours 3").Should().Be("The program runs no day sky, which SetSkyTime or SetSkyDay starts.");
        Run("time.frame 1e999").Should().StartWith("time.frame takes", "an infinity is no number of seconds");
    }

    [Fact]
    public void A_Shown_Programs_Settings_Are_Read_From_Its_File_As_It_Starts_And_Written_As_One_Changes()
    {
        using var folder = new TestFolder("settings-");
        var path = Path.Combine(folder.Path, "Game", "settings.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "keys.console F7\nwindow.vsync on\nlate.value 5\nmodule.unknown 3\n");
        var given = DiagnosticsBinding.SettingsPath;
        DiagnosticsBinding.SettingsPath = config => config.WindowData.Title == Title ? path : given(config);
        try
        {
            Frames(1);
            Run("keys.console").Should().StartWith("keys.console is F7", "the file is read as the program starts");
            Run("window.vsync").Should().StartWith("window.vsync is off", "a headless run has no swapchain, so the file's line is refused");
            Run("reset keys.console");
            File.ReadAllText(path).Should().Be("late.value 5\nmodule.unknown 3\n",
                "a setting back at its default leaves the file, a refused line is not written again, and a line of a name this run does not know stays");
            var late = new Setting<int>("late.value", "A saved setting added after the start.", 0);
            Registry.Process.Add(late);
            try
            {
                late.Value.Should().Be(5, "a setting added after the start takes its line from the file");
                Run("late.value 6");
                File.ReadAllText(path).Should().Be("late.value 6\nmodule.unknown 3\n", "the file is written as a setting added after the start is set");
                Run("reset late.value");
            }
            finally
            {
                Registry.Process.Remove("late.value");
            }
            Run("keys.console F7");
            File.ReadAllText(path).Should().Be("keys.console F7\nmodule.unknown 3\n");
            Run("ao.method circular");
            File.ReadAllText(path).Should().NotContain("ao.method", "the file keeps none of the run's own settings");
            Run("reset keys.console");
            Run("reset ao.method");
            File.ReadAllText(path).Should().Be("module.unknown 3\n");
            CloseWindow();
            UseApp(null);
            File.ReadAllText(path).Should().Be("module.unknown 3\n", "the file written as the program ends holds what it held");
        }
        finally
        {
            DiagnosticsBinding.SettingsPath = given;
        }
    }
}

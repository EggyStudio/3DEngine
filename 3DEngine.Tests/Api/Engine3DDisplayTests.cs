using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

[Collection("Engine3D")]
[Trait("Category", "Unit")]
public class Engine3DDisplayTests
{
    [Fact]
    public void Modes_That_Differ_Only_Past_Whole_Hertz_Are_Listed_Once_In_Order()
    {
        var modes = DistinctModes([(2560, 1440, 165.0f), (2560, 1440, 164.96f), (2560, 1440, 60f), (1920, 1080, 59.94f), (1920, 1080, 60f)]);

        modes.Should().Equal(new MonitorMode(2560, 1440, 165), new MonitorMode(2560, 1440, 60), new MonitorMode(1920, 1080, 60));
    }

    [Fact]
    public void A_Monitor_That_Is_Not_There_Has_No_Modes_And_No_Window_Is_Changed()
    {
        GetMonitorModes(-1).Should().BeEmpty();
        GetMonitorModes(GetMonitorCount()).Should().BeEmpty();

        // With no window these do nothing, as the rest of the window's calls do.
        SetWindowFullscreenMode(new MonitorMode(1280, 720, 60));
        SetWindowMonitor(0);
        IsWindowFullscreen().Should().BeFalse();
        ToggleBorderlessWindowed();
        SetWindowIcon(GenImageColor(16, 16, Color.Red));
        GetWindowScaleDPI().Should().Be(System.Numerics.Vector2.One);
        GetMonitorPhysicalWidth(-1).Should().Be(0);
    }

    [Fact]
    public void A_Monitors_Size_In_Millimeters_Is_Its_Pixels_At_96_An_Inch_Times_Its_Scale_As_Raylibs_SDL_Backend_Has_It()
    {
        Millimeters(3840, 1).Should().Be(1016);
        Millimeters(3840, 2).Should().Be(508, "a monitor that doubles its pixels has twice as many an inch");
    }

    [Fact]
    public void Time_Is_Read_From_The_Clock_As_It_Is_Asked_And_From_The_Frame_Where_Frames_Count_A_Set_Time()
    {
        UseApp(new App(Config.Default with { Headless = true, HeadlessFps = 1000 }).AddPlugin(new DefaultPlugins()));
        try
        {
            BeginDrawing();
            var start = GetTime();
            WaitTime(0.05);
            GetTime().Should().BeGreaterThanOrEqualTo(start + 0.045, "raylib's GetTime reads the clock, so a program's own wait shows in it");
            EndDrawing();
        }
        finally
        {
            CloseWindow();
        }

        UseApp(new App(Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 0.5 }).AddPlugin(new DefaultPlugins()));
        try
        {
            BeginDrawing();
            var frame = GetTime();
            WaitTime(0.05);
            GetTime().Should().Be(frame, "a frame that counts a set time keeps its time through the frame");
            EndDrawing();
            BeginDrawing();
            GetTime().Should().BeApproximately(frame + 0.5, 1e-9, "the next frame is half a second on");
            EndDrawing();
        }
        finally
        {
            CloseWindow();
        }
    }

    [Fact]
    public void The_Trace_Log_Level_Is_The_Consoles_Least()
    {
        var before = LogConfig.ConsoleMinimumLevel;
        try
        {
            SetTraceLogLevel(LogLevel.Warning);
            LogConfig.ConsoleMinimumLevel.Should().Be(LogLevel.Warning);
            TraceLog(LogLevel.Info, "below the console's level, so only in the file");
        }
        finally
        {
            LogConfig.ConsoleMinimumLevel = before;
        }
    }

    [Fact]
    public void Dropped_Files_Are_Kept_Until_They_Are_Unloaded()
    {
        var app = new App();
        app.World.InitResource<Input>();
        UseApp(app);
        try
        {
            IsFileDropped().Should().BeFalse();
            app.World.Resource<Input>().AddDroppedFile("/levels/one.json");
            app.World.Resource<Input>().AddDroppedFile("/levels/two.json");
            app.World.Resource<Input>().BeginFrame();

            IsFileDropped().Should().BeTrue("a drop outlasts the frame it arrived in");
            LoadDroppedFiles().Should().Equal("/levels/one.json", "/levels/two.json");
            UnloadDroppedFiles();
            IsFileDropped().Should().BeFalse();
        }
        finally
        {
            UseApp(null);
        }
    }
}

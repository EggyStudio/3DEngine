using System.Buffers.Binary;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Diagnostics;

/// <summary>
/// F2 writing a screenshot in every program into a folder beside it, named by the time as Minecraft
/// names its own, the key moved or turned off, the press still the program's, and F3 the
/// performance window's key.
/// </summary>
/// <remarks>
/// A run with no GPU has no frame to write, so the files are read on the device, offscreen, and
/// the names and the keys without a GPU.
/// </remarks>
[Collection("E3D")]
public sealed class ScreenshotKeyTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-screenshot-key-");

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static Input Input => GetApp().World.Resource<Input>();

    private static DebugKeys Keys => GetApp().World.Resource<DebugKeys>();

    // A frame with a key held down through it, then a frame with it up, so it is pressed once.
    private static void Press(Key key)
    {
        Input.SetKey(key, true);
        BeginDrawing();
        ClearBackground(Color.DarkBlue);
        EndDrawing();
        Input.SetKey(key, false);
        Frames(1);
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

    [Fact]
    [Trait("Category", "Unit")]
    public void Three_Screenshots_In_One_Second_Take_The_Time_Then_Two_And_Three_And_The_Next_Second_The_Time_Alone()
    {
        var keys = new DebugKeys { Folder = _folder.Path };
        var second = new DateTime(2026, 10, 21, 3, 45, 12);
        keys.Next(second).Should().Be(Path.Combine(_folder.Path, "2026-10-21_03.45.12.png"));
        keys.Next(second).Should().Be(Path.Combine(_folder.Path, "2026-10-21_03.45.12_2.png"), "a name given this run is not given again before its file is written");
        keys.Next(second).Should().Be(Path.Combine(_folder.Path, "2026-10-21_03.45.12_3.png"));
        keys.Next(second.AddSeconds(1)).Should().Be(Path.Combine(_folder.Path, "2026-10-21_03.45.13.png"));

        // A file standing in the folder from another run is passed over too.
        File.WriteAllBytes(Path.Combine(_folder.Path, "2026-10-21_03.45.14.png"), []);
        new DebugKeys { Folder = _folder.Path }.Next(second.AddSeconds(2)).Should().Be(Path.Combine(_folder.Path, "2026-10-21_03.45.14_2.png"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void The_Keys_Move_And_Turn_Off_And_A_Press_With_No_Renderer_Reaches_The_Program_And_Writes_Nothing()
    {
        UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));
        Keys.Folder = _folder.Path;
        (Keys.Screenshot, Keys.ProfileWindow).Should().Be((Key.F2, Key.F3), "F2 and F3 are Minecraft's keys for the two");

        Input.SetKey(Key.F2, true);
        BeginDrawing();
        IsKeyPressed(Key.F2).Should().BeTrue("the press is the program's as well");
        EndDrawing();
        Input.SetKey(Key.F2, false);
        Directory.EnumerateFiles(_folder.Path).Should().BeEmpty("a run with no renderer has no frame to write");

        SetScreenshotKey(Key.F12);
        SetProfileWindowKey(Key.Null);
        (Keys.Screenshot, Keys.ProfileWindow).Should().Be((Key.F12, Key.Null));
        SetScreenshotKey(Key.Null);
        Keys.Screenshot.Should().Be(Key.Null);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void F2_Writes_The_Frame_Into_The_Folder_Two_Presses_Take_Two_Names_And_A_Moved_Or_Turned_Off_Key_Is_Followed()
    {
        var config = Config.Default.WithWindow("screenshot key", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        Keys.Folder = _folder.Path;
        // Held in one second, so the two presses fall in it whatever the machine's clock does.
        Keys.Clock = static () => new DateTime(2026, 10, 21, 3, 45, 12);
        Frames(2);

        Press(Key.F2);
        Press(Key.F2);
        Frames(5);
        var written = Directory.EnumerateFiles(_folder.Path, "*.png").Order(StringComparer.Ordinal).ToArray();
        written.Select(Path.GetFileName).Should().Equal(["2026-10-21_03.45.12.png", "2026-10-21_03.45.12_2.png"],
            "each press writes a file of its own, the second in the same second counted from two");
        var header = File.ReadAllBytes(written[0]).AsSpan(16, 8);
        (BinaryPrimitives.ReadInt32BigEndian(header), BinaryPrimitives.ReadInt32BigEndian(header[4..])).Should().Be((96, 64), "the file is the frame, at its size");

        SetScreenshotKey(Key.F12);
        Press(Key.F2);
        Frames(5);
        Directory.EnumerateFiles(_folder.Path, "*.png").Should().HaveCount(2, "F2 writes nothing once the key is moved");
        Press(Key.F12);
        Frames(5);
        Directory.EnumerateFiles(_folder.Path, "*.png").Should().HaveCount(3, "the moved key writes");

        SetScreenshotKey(Key.Null);
        Press(Key.F12);
        Press(Key.F2);
        Frames(5);
        Directory.EnumerateFiles(_folder.Path, "*.png").Should().HaveCount(3, "a key turned off writes nothing");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void F3_Shows_And_Hides_The_Performance_Window_Which_F2_No_Longer_Does()
    {
        UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));
        Frames(1);
        var shown = PerformanceHud.Shown;
        Press(Key.F2);
        PerformanceHud.Shown.Should().Be(shown, "F2 is the screenshot's key");
        Press(Key.F3);
        PerformanceHud.Shown.Should().Be(!shown, "F3 flips the window");
        Press(Key.F3);
        PerformanceHud.Shown.Should().Be(shown);
    }
}

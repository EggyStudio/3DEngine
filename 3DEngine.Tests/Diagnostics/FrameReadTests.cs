using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Diagnostics;

/// <summary>
/// <c>frame.read</c>, a rectangle of the frame drawn read as its mean color and its largest step:
/// a frame of a known clear color read back as it, a frame of two halves giving the step at the
/// middle, a rectangle of one half, and the command's refusals, measured on frames of known pixels
/// with no device and on the device's own frames where there is one.
/// </summary>
[Collection("E3D")]
public sealed class FrameReadTests : IDisposable
{
    private static readonly Color Sky = new(81, 137, 218, 255), Orange = new(255, 128, 0, 255);

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // A frame of RGBA pixels, rows from the top, its left half one color and its right half another.
    private static byte[] Halves(int width, int height, Color left, Color right)
    {
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var color = x < width / 2 ? left : right;
                var at = (y * width + x) * 4;
                (rgba[at], rgba[at + 1], rgba[at + 2], rgba[at + 3]) = (color.R, color.G, color.B, color.A);
            }
        return rgba;
    }

    // Runs a command in the app's world, and the answer it gives later where it gives one.
    private static (string Answer, Func<string?>? Later, string? Code) Run(string line)
    {
        using (ConsoleHost.Lend(GetApp().World, GetApp()))
            return (ConsoleCommands.Run(line) ?? "", ConsoleHost.Pending, ConsoleHost.Failure?.Code);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Frame_Of_One_Color_Is_Read_Back_As_It_With_No_Step()
    {
        var frame = Halves(64, 48, Sky, Sky);

        FrameReadCommands.Measure(frame, 64, 0, 0, 64, 48).Should().Be(new FrameReadCommands.Reading(81, 137, 218, 0, 0, 0));
        FrameReadCommands.Measure(frame, 64, 10, 5, 7, 3).Should().Be(new FrameReadCommands.Reading(81, 137, 218, 0, 10, 5));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Frame_Of_Two_Halves_Steps_At_The_Middle_And_One_Half_Read_Alone_Is_Its_Color()
    {
        var frame = Halves(64, 48, Color.Black, Orange);

        // Half black and half orange mean half the orange, rounded away from zero, and the step is
        // red's whole range, between the last black pixel of the middle row and the first orange.
        var whole = FrameReadCommands.Measure(frame, 64, 0, 0, 64, 48);
        whole.Should().Be(new FrameReadCommands.Reading(128, 64, 0, 255, 31, 24));
        FrameReadCommands.Say(whole, 94).Should().Be("mean 128 64 0 step 255 at 31 24 frame 94");

        FrameReadCommands.Measure(frame, 64, 32, 0, 32, 48).Should().Be(new FrameReadCommands.Reading(255, 128, 0, 0, 32, 0));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void With_No_Device_The_Command_Says_There_Is_Nothing_To_Read_And_Refuses_A_Rectangle_That_Is_Not_One()
    {
        UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));

        Run("frame.read").Code.Should().Be("NO_RENDERER");
        Run("frame.read 1 2 3").Code.Should().Be("BAD_ARGUMENT");
        Run("frame.read 0 0 0 5").Code.Should().Be("BAD_ARGUMENT");
        Run("frame.read 0 0 a 5").Code.Should().Be("BAD_ARGUMENT");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Devices_Frame_Of_A_Clear_Color_Reads_As_It_And_One_Of_Two_Halves_Steps_At_The_Middle()
    {
        var config = Config.Default.WithWindow("frame read", 64, 48) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));

        string Read(string line, Action draw)
        {
            for (int i = 0; i < 2; i++) Frame(draw);
            var (answer, later, code) = Run(line);
            code.Should().BeNull(answer);
            for (int i = 0; i < 10; i++)
            {
                Frame(draw);
                if (later!() is { } read) return read;
            }
            throw new InvalidOperationException($"{line} gave no answer in ten frames.");
        }

        static void Frame(Action draw)
        {
            BeginDrawing();
            draw();
            EndDrawing();
        }

        Read("frame.read", () => ClearBackground(Sky)).Should().StartWith("mean 81 137 218 step 0 at 0 0 frame ");

        void TwoHalves()
        {
            ClearBackground(Color.Black);
            DrawRectangle(32, 0, 32, 48, Orange);
        }
        Read("frame.read", TwoHalves).Should().StartWith("mean 128 64 0 step 255 at 31 24 frame ");
        Read("frame.read 40 8 16 16", TwoHalves).Should().StartWith("mean 255 128 0 step 0 at 40 8 frame ");
    }
}

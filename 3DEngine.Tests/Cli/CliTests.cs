using System.Text.Json;
using FluentAssertions;
using StbImageSharp;

namespace Engine.Tests.Cli;

[Collection("Console")]
[Trait("Category", "Unit")]
public sealed class CliTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-cli-test-");

    // A session's times, which these tests write and read back as values and never wait on.
    private static readonly DateTimeOffset Stamp = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void An_Envelope_Carries_Success_Data_And_Errors()
    {
        using var ok = JsonDocument.Parse(CliJson.Ok("status", w => w.WriteNumber("frame", 7), id: "a"));
        ok.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        ok.RootElement.GetProperty("id").GetString().Should().Be("a");
        ok.RootElement.GetProperty("data").GetProperty("frame").GetInt32().Should().Be(7);

        using var fail = JsonDocument.Parse(CliJson.Fail("run", "NO_SESSION", "nothing"));
        fail.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        fail.RootElement.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
        fail.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("NO_SESSION");
    }

    [Fact]
    public void A_Session_File_Reads_Back_What_Was_Written()
    {
        var previous = CliSessionFile.Directory;
        CliSessionFile.Directory = _folder.Path;
        try
        {
            var session = new CliSession(Environment.ProcessId, 4242, "token", "/project", "Game", "Title", "hidden",
                Stamp, true, "ready", 12, Stamp);
            CliSessionFile.Write(session);

            var read = CliSessionFile.All().Should().ContainSingle().Subject;
            read.Port.Should().Be(4242);
            read.Mode.Should().Be("hidden");
            read.State.Should().Be("ready");
            read.Report.Should().Be("unreachable", "its heartbeat is long past, so the app is taken as no longer answering");

            CliSessionFile.Remove(session.Pid);
            CliSessionFile.All().Should().BeEmpty();
        }
        finally
        {
            CliSessionFile.Directory = previous;
        }
    }

    [Fact]
    public async Task A_Request_Over_The_Socket_Is_Answered_Between_Frames()
    {
        var app = new App();
        var ecs = new EcsWorld();
        app.World.InsertResource(ecs);
        app.World.InsertResource(new Time());
        ecs.Spawn();

        var queue = new CliQueue();
        using var server = new CliServer(queue, new AppThreads());
        var session = new CliSession(Environment.ProcessId, server.Port, server.Token, "", "test", "test", "headless",
            Stamp, false, "ready", 0, Stamp);

        // The main thread's part, pumped while the client waits on its socket.
        using var stop = new CancellationTokenSource();
        var pump = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                queue.Pump(app.World, app);
                Thread.Yield();
            }
        });

        using var answer = JsonDocument.Parse(CliClient.Send(session, "run", "entity.count", 10));
        stop.Cancel();
        await pump;

        answer.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        answer.RootElement.GetProperty("data").GetProperty("result").GetString().Should().Be("1");
    }

    [Fact]
    public void The_App_Waits_For_An_Answer_As_Long_As_The_Request_Says()
    {
        // A queue nobody pumps, so the answer never comes and the wait is all there is to see. The
        // caller gives up reading after ten seconds, so an app waiting the default half minute in
        // place of the request's second fails the read rather than answering.
        using var server = new CliServer(new CliQueue(), new AppThreads());
        using var caller = new System.Net.Sockets.TcpClient("127.0.0.1", server.Port) { ReceiveTimeout = 10_000 };
        using var stream = caller.GetStream();
        using var writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\n" };
        using var reader = new StreamReader(stream);

        writer.WriteLine($$"""{"op":"run","token":"{{server.Token}}","line":"frames.wait 1000","seconds":1}""");
        using var answer = JsonDocument.Parse(reader.ReadLine()!);

        answer.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("TIMEOUT", "the request's own second, not the default half minute, is waited");
    }

    [Fact]
    public void A_Request_With_The_Wrong_Token_Is_Refused()
    {
        using var server = new CliServer(new CliQueue(), new AppThreads());
        var session = new CliSession(Environment.ProcessId, server.Port, "wrong", "", "test", "test", "headless",
            Stamp, false, "ready", 0, Stamp);

        using var answer = JsonDocument.Parse(CliClient.Send(session, "ping", seconds: 5));

        answer.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("BAD_TOKEN");
    }

    [Fact]
    public void A_Png_Reads_Back_As_The_Pixels_Written()
    {
        var pixels = new byte[3 * 2 * 4];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i * 10);
        var path = Path.Combine(_folder.Path, "shot.png");

        PngWriter.Write(path, pixels, 3, 2);

        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        image.Width.Should().Be(3);
        image.Height.Should().Be(2);
        image.Data.Should().Equal(pixels);
    }

    [Fact]
    public void Run_Modes_Come_From_Flags_And_Variables()
    {
        var (arguments, variable) = (RunMode.Arguments, RunMode.Variable);
        try
        {
            RunMode.Arguments = () => ["game", "--serve", "--frames", "90"];
            RunMode.Variable = name => name == "E3D_HIDDEN" ? "1" : null;

            var config = RunMode.Apply(Config.Default);

            config.Serve.Should().BeTrue();
            config.Hidden.Should().BeTrue();
            config.Headless.Should().BeFalse();
            config.Frames.Should().Be(90UL);
            config.FrameSeconds.Should().Be(0, "the clock is read unless asked otherwise");
            RunMode.Describe(config).Should().Be("hidden");

            RunMode.Arguments = () => ["game", "--frame-time", "0.02"];
            RunMode.Apply(Config.Default).FrameSeconds.Should().Be(0.02);
            RunMode.Arguments = () => ["game"];
            RunMode.Variable = name => name == "E3D_FRAME_TIME" ? "0.0125" : null;
            RunMode.Apply(Config.Default).FrameSeconds.Should().Be(0.0125);

            RunMode.Samples(4).Should().Be(4, "a window is drawn at the samples it would be unless asked");
            RunMode.Variable = name => name == "E3D_SAMPLES" ? "1" : null;
            RunMode.Samples(4).Should().Be(1);
            RunMode.Arguments = () => ["game", "--samples", "2"];
            RunMode.Samples(4).Should().Be(2, "a flag is read before the variable");

            RunMode.Arguments = () => ["game"];
            RunMode.Variable = _ => null;
            RunMode.Seed().Should().BeNull("a window seeds the generator from the clock unless asked");
            RunMode.Variable = name => name == "E3D_SEED" ? "7" : null;
            RunMode.Seed().Should().Be(7u);
            RunMode.Arguments = () => ["game", "--seed", "42"];
            RunMode.Seed().Should().Be(42u, "a flag is read before the variable");
        }
        finally
        {
            (RunMode.Arguments, RunMode.Variable) = (arguments, variable);
        }
    }

    [Fact]
    public void A_Synthetic_Key_Is_Held_For_Its_Frames_Then_Released()
    {
        var input = new Input();
        var synthetic = new SyntheticInput();

        synthetic.Key(input, Key.W, frame: 10, frames: 3);
        input.KeyDown(Key.W).Should().BeFalse("the press waits for the loop to process events");
        input.ApplyQueued();
        input.KeyPressed(Key.W).Should().BeTrue();

        synthetic.Update(input, 12);
        input.ApplyQueued();
        input.KeyDown(Key.W).Should().BeTrue();
        synthetic.Update(input, 13);
        input.ApplyQueued();
        input.KeyDown(Key.W).Should().BeFalse();
        input.KeyReleased(Key.W).Should().BeTrue();
    }

    [Fact]
    public void Input_Commands_Take_Names_And_Not_Numbers()
    {
        InputCommands.TryName<MouseButton>("right", out var button).Should().BeTrue();
        button.Should().Be(MouseButton.Right);
        InputCommands.TryName<Key>("two", out var key).Should().BeTrue();
        key.Should().Be(Key.Two);

        InputCommands.TryName<MouseButton>("100", out _).Should().BeFalse("a number names no button, and ImGui stops the program over one past its five");
        InputCommands.TryName<MouseButton>("Middle, Right", out _).Should().BeFalse("a list of names is not a name");
        InputCommands.TryName<Key>("30", out _).Should().BeFalse();
    }

    [Fact]
    public void Mouse_Buttons_Reach_ImGui_In_Its_Order()
    {
        SdlImGuiInput.ImGuiButton(MouseButton.Left).Should().Be((int)ImGuiNET.ImGuiMouseButton.Left);
        SdlImGuiInput.ImGuiButton(MouseButton.Right).Should().Be((int)ImGuiNET.ImGuiMouseButton.Right);
        SdlImGuiInput.ImGuiButton(MouseButton.Middle).Should().Be((int)ImGuiNET.ImGuiMouseButton.Middle);
        SdlImGuiInput.ImGuiButton(MouseButton.X2).Should().Be(4);
    }
}

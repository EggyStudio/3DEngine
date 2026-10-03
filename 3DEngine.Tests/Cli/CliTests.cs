using System.Text.Json;
using FluentAssertions;
using StbImageSharp;

namespace Engine.Tests.Cli;

[Collection("Console")]
[Trait("Category", "Unit")]
public sealed class CliTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("engine-cli-test-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

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
        CliSessionFile.Directory = _directory;
        try
        {
            var session = new CliSession(Environment.ProcessId, 4242, "token", "/project", "Game", "Title", "hidden",
                DateTimeOffset.UtcNow, true, "ready", 12, DateTimeOffset.UtcNow);
            CliSessionFile.Write(session);

            var read = CliSessionFile.All().Should().ContainSingle().Subject;
            read.Port.Should().Be(4242);
            read.Mode.Should().Be("hidden");
            read.Report.Should().Be("ready");

            CliSessionFile.Remove(session.Pid);
            CliSessionFile.All().Should().BeEmpty();
        }
        finally
        {
            CliSessionFile.Directory = previous;
        }
    }

    [Fact]
    public void A_Request_Over_The_Socket_Is_Answered_Between_Frames()
    {
        var app = new App();
        var ecs = new EcsWorld();
        app.World.InsertResource(ecs);
        app.World.InsertResource(new Time());
        ecs.Spawn();

        var queue = new CliQueue();
        using var server = new CliServer(queue);
        var session = new CliSession(Environment.ProcessId, server.Port, server.Token, "", "test", "test", "headless",
            DateTimeOffset.UtcNow, false, "ready", 0, DateTimeOffset.UtcNow);

        // The main thread's part, pumped while the client waits on its socket.
        using var stop = new CancellationTokenSource();
        var pump = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                queue.Pump(app.World, app);
                Thread.Sleep(5);
            }
        });

        using var answer = JsonDocument.Parse(CliClient.Send(session, "run", "entity.count", 10));
        stop.Cancel();
        pump.Wait();

        answer.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        answer.RootElement.GetProperty("data").GetProperty("result").GetString().Should().Be("1");
    }

    [Fact]
    public void A_Request_With_The_Wrong_Token_Is_Refused()
    {
        using var server = new CliServer(new CliQueue());
        var session = new CliSession(Environment.ProcessId, server.Port, "wrong", "", "test", "test", "headless",
            DateTimeOffset.UtcNow, false, "ready", 0, DateTimeOffset.UtcNow);

        using var answer = JsonDocument.Parse(CliClient.Send(session, "ping", seconds: 5));

        answer.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("BAD_TOKEN");
    }

    [Fact]
    public void A_Png_Reads_Back_As_The_Pixels_Written()
    {
        var pixels = new byte[3 * 2 * 4];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i * 10);
        var path = Path.Combine(_directory, "shot.png");

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
            RunMode.Describe(config).Should().Be("hidden");
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
        input.KeyPressed(Key.W).Should().BeTrue();

        synthetic.Update(input, 12);
        input.KeyDown(Key.W).Should().BeTrue();
        synthetic.Update(input, 13);
        input.KeyDown(Key.W).Should().BeFalse();
        input.KeyReleased(Key.W).Should().BeTrue();
    }
}

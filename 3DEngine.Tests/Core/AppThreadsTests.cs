using System.Net;
using System.Net.Sockets;
using FluentAssertions;

namespace Engine.Tests.Core;

/// <summary>No thread an app's parts started is alive once the app has shut down.</summary>
[Collection("Memory")]
[Trait("Category", "Integration")]
public sealed class AppThreadsTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-app-threads-");
    private readonly string _sessions = CliSessionFile.Directory;

    public AppThreadsTests() => CliSessionFile.Directory = _folder.Path;

    public void Dispose()
    {
        CliSessionFile.Directory = _sessions;
        _folder.Dispose();
    }

    [Fact]
    public void A_Served_App_With_A_Caller_Connected_Leaves_None_Of_Its_Threads_Alive_After_Shutdown()
    {
        var app = new App(Config.Default with { Headless = true, Serve = true }).AddPlugin(new DefaultPlugins());
        app.BeginFrame();
        app.EndFrame();
        var threads = app.World.Resource<AppThreads>();

        // A caller connected and left idle, whose connection's thread waits on a read until the
        // caller writes or hangs up. A request with the wrong token is answered by that thread
        // itself, so its answer says the thread is running.
        var port = CliSessionFile.All().Single(s => s.Pid == Environment.ProcessId).Port;
        using var caller = new TcpClient { ReceiveTimeout = 10_000 };
        caller.Connect(IPAddress.Loopback, port);
        var stream = caller.GetStream();
        using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        using var reader = new StreamReader(stream, leaveOpen: true);
        writer.WriteLine("""{"op":"ping","token":"wrong"}""");
        reader.ReadLine().Should().Contain("BAD_TOKEN");
        var started = threads.Started;
        started.Select(t => t.Name).Should().BeEquivalentTo(["e3d-cli-accept", "e3d-cli-connection"]);

        app.Shutdown();

        started.Where(t => t.IsAlive).Select(t => t.Name).Should().BeEmpty("Shutdown joins the threads the app's parts started, the console's listener and its connection among them");
    }

    [Fact]
    public void A_Headless_App_Leaves_No_Thread_Of_Its_Own_Alive_After_Shutdown()
    {
        var app = new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins());
        app.BeginFrame();
        app.EndFrame();
        var started = app.World.TryGetResource<AppThreads>(out var threads) ? threads.Started : [];

        app.Shutdown();

        started.Where(t => t.IsAlive).Should().BeEmpty();
    }
}

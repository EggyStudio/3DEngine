using FluentAssertions;

namespace Engine.Tests.Core;

[Trait("Category", "Unit")]
public class AppFrameTests
{
    private static (App App, List<Stage> Ran) Recording()
    {
        var app = new App();
        var ran = new List<Stage>();
        foreach (var stage in StageOrder.AllInOrder())
        {
            var s = stage;
            app.AddSystem(s, new SystemDescriptor(_ => ran.Add(s), $"Record.{s}").MainThreadOnly());
        }
        return (app, ran);
    }

    [Fact]
    public void BeginFrame_Runs_Startup_Then_First_Through_Update()
    {
        var (app, ran) = Recording();

        app.BeginFrame();

        ran.Should().Equal(Stage.Startup, Stage.First, Stage.PreUpdate, Stage.Update);
    }

    [Fact]
    public void EndFrame_Runs_PostUpdate_Through_Last()
    {
        var (app, ran) = Recording();
        app.BeginFrame();
        ran.Clear();

        app.EndFrame();

        ran.Should().Equal(Stage.PostUpdate, Stage.Render, Stage.Last);
    }

    [Fact]
    public void Startup_Runs_Once_Across_Frames()
    {
        var (app, ran) = Recording();

        app.Frame();
        app.Frame();

        ran.Count(s => s == Stage.Startup).Should().Be(1);
        ran.Count(s => s == Stage.Update).Should().Be(2);
        app.FrameCount.Should().Be(2);
    }

    [Fact]
    public void Shutdown_Runs_Cleanup_Once()
    {
        var (app, ran) = Recording();
        app.Frame();

        app.Shutdown();
        app.Shutdown();

        ran.Count(s => s == Stage.Cleanup).Should().Be(1);
    }
}

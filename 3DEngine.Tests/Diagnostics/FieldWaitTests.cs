using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Diagnostics;

/// <summary>
/// <c>field.wait</c>, which a picture of a scene waits on before it is taken: answered once the
/// scene's distance field is built and its light has run its frames on it, at once where no field
/// is drawn, and given as many frames as asked rather than the 600 a command answering later is
/// given unless it says.
/// </summary>
[Collection("E3D")]
public sealed class FieldWaitTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // Runs a command in the app's world, with the answer it gives later and the frames it is given.
    private static (string Answer, Func<string?>? Later, ulong Frames) Run(string line)
    {
        using (ConsoleHost.Lend(GetApp().World, GetApp()))
            return (ConsoleCommands.Run(line) ?? "", ConsoleHost.Pending, ConsoleHost.PendingFrames);
    }

    // Asks a command answering later once a frame, as the CLI's queue does, after each frame drawn.
    private static (string? Answer, int Frames) Poll(Func<string?> later, Action draw, int most)
    {
        for (int frame = 1; frame <= most; frame++)
        {
            draw();
            using (ConsoleHost.Lend(GetApp().World, GetApp()))
                if (later() is { } answer) return (answer, frame);
        }
        return (null, most);
    }

    [NeedsVulkanFact]
    public void A_Wait_Answers_Once_The_Field_Is_Built_And_Its_Light_Has_Run_Its_Frames_On_It()
    {
        var config = Config.Default.WithWindow("field wait", 160, 90) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.Low);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 2, 6), Vector3.Zero, Vector3.UnitY, 60);
        void Draw()
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            for (int i = 0; i < 4; i++) DrawModel(block, new Vector3(i * 1.5f - 2.25f, 0, 0), 1, Color.Gray);
            EndMode3D();
            EndDrawing();
        }
        Draw();

        var (said, later, frames) = Run("field.wait 900");
        said.Should().Be("waiting for the scene's distance field");
        frames.Should().BeGreaterThan(ConsoleHost.LaterFrames, "the wait is given the frames asked and not the default");
        var (answer, waited) = Poll(later!, Draw, 900);
        answer.Should().StartWith("waited ").And.Contain("built");
        waited.Should().BeGreaterThanOrEqualTo(SceneFieldCommands.LightFrames, "the light runs its frames on the field built");
        var plan = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<SceneFieldRenderer>()!.Plan!;
        plan.Built.Should().BeTrue();
        plan.FramesBuilt.Should().BeGreaterThanOrEqualTo(SceneFieldCommands.LightFrames);
        UnloadModel(block);
    }

    [NeedsVulkanFact]
    public void A_Wait_Where_No_Field_Is_Drawn_Answers_At_Once()
    {
        var config = Config.Default.WithWindow("field wait", 160, 90) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        void Draw()
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            DrawRectangle(10, 10, 40, 30, Color.Orange);
            EndDrawing();
        }
        Draw();
        var (_, later, _) = Run("field.wait 900");
        var (answer, waited) = Poll(later!, Draw, 10);
        answer.Should().Be("no scene field is drawn, so nothing was waited for");
        waited.Should().BeLessThanOrEqualTo(3);
    }
}

using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Diagnostics;

/// <summary>
/// <c>pipelines.wait</c>, which a script that reads a pixel after the first sight of a material
/// waits on: answered once no pipeline is being made, on the workers or in the driver, and not
/// while a worker's compile is held.
/// </summary>
[Collection("E3D")]
public sealed class PipelinesWaitTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [NeedsVulkanFact]
    public void A_Wait_Holds_While_A_Workers_Compile_Is_Held_And_Answers_Once_It_Ends()
    {
        // A worker's compile held in the driver by the test, as a mirror's pipelines are made on
        // workers after the frame that asks for them. The wait asked meanwhile answers none of the
        // frames drawn while it is held, and answers in the frame after it lets go.
        var config = Config.Default.WithWindow("pipelines wait", 64, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        void Draw()
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            EndDrawing();
        }
        Draw();
        var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
        var spirv = SlangCompiler.Compile("""
            RWStructuredBuffer<uint> counts;

            [shader("compute")]
            [numthreads(1, 1, 1)]
            void computeMain(uint3 id : SV_DispatchThreadID)
            {
                counts[id.x] = id.x + 3;
            }
            """, "waited.slang", "computeMain", ShaderStage.Compute);
        using var inside = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        device.WhileMakingPipeline = () =>
        {
            inside.Set();
            gate.Wait(TimeSpan.FromSeconds(60));
        };
        var compile = Task.Run(() => device.CreateComputePipeline(spirv, 0, [0]));
        try
        {
            inside.Wait(TimeSpan.FromSeconds(60)).Should().BeTrue("the worker's compile reached the driver");
            device.WhileMakingPipeline = null;
            Func<string?> later;
            using (ConsoleHost.Lend(GetApp().World, GetApp()))
            {
                ConsoleCommands.Run("pipelines.wait 120").Should().Be("waiting for the pipelines being made");
                later = ConsoleHost.Pending!;
            }
            for (int frame = 0; frame < 30; frame++)
            {
                Draw();
                using (ConsoleHost.Lend(GetApp().World, GetApp()))
                    later().Should().BeNull("a worker's compile is still in the driver");
            }
            gate.Set();
            compile.Wait(TimeSpan.FromSeconds(60)).Should().BeTrue("the compile ends once let go");
            Draw();
            using (ConsoleHost.Lend(GetApp().World, GetApp()))
                later().Should().StartWith("no pipeline is being made", "the wait answers in the frame after the compile ends");
        }
        finally
        {
            gate.Set();
            device.WhileMakingPipeline = null;
            using (compile.Result) { }
        }
    }
}

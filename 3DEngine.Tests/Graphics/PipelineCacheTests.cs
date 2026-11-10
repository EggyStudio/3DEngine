using System.Buffers.Binary;
using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Graphics;

/// <summary>
/// The driver's compiled pipelines kept in the user's cache folder between runs
/// (<see cref="GraphicsDevice.PipelineCachePath"/>).
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class PipelineCacheTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // Opens a window, draws a lit cube for a few frames and closes it, as a run of a program does.
    private static void Run()
    {
        var config = Config.Default.WithWindow("pipeline cache", 64, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var camera = new Camera3D(new Vector3(2, 2, 2), Vector3.Zero, Vector3.UnitY, 45);
        for (int frame = 0; frame < 3; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawCube(Vector3.Zero, 1, 1, 1, Color.White);
            EndMode3D();
            EndDrawing();
        }
        CloseWindow();
        UseApp(null);
    }

    [NeedsVulkanFact]
    public void The_Pipelines_Are_Written_Once_The_Frames_After_Those_That_Made_Them_Made_None_And_A_File_Past_The_Bound_Is_Read_As_Empty()
    {
        // In a cache folder of the test's own, a lit cube drawn until the file is written, the
        // program still running, as a run that is killed then keeps them, or until 120 frames have
        // made no pipeline on a driver that keeps none in the cache. Drawn for a set count of frames,
        // the file was not there on a runner's lavapipe, which keeps none.
        using var folder = new TestFolder("engine-pipelines-");
        UserCache.Folder = folder.Path;
        try
        {
            var path = GraphicsDevice.PipelineCachePath;
            var config = Config.Default.WithWindow("pipeline cache", 64, 64) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
            var camera = new Camera3D(new Vector3(2, 2, 2), Vector3.Zero, Vector3.UnitY, 45);
            var (made, quiet) = (-1L, 0);
            for (int frame = 0; frame < 1000 && !File.Exists(path) && !(quiet > 130 && !device.PipelineCacheHolds); frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawCube(Vector3.Zero, 1, 1, 1, Color.White);
                EndMode3D();
                EndDrawing();
                var now = device.PipelinesMade.Made;
                (made, quiet) = now == made ? (made, quiet + 1) : (now, 0);
            }
            // A driver that keeps no pipelines in the cache, as lavapipe with Mesa's own shader cache
            // off, gives nothing past the header to write, and the device says so in place of
            // writing a file, which leaves the bound nothing to read either.
            if (!device.PipelineCacheHolds)
            {
                File.Exists(path).Should().BeFalse("a cache that holds nothing past its header is not written");
                return;
            }
            File.Exists(path).Should().BeTrue("the pipelines are written once 120 frames after those that made them made none");
            CloseWindow();
            UseApp(null);

            // A file past the bound, 300 MB of it, starts the cache empty, and the run's own are
            // written in its place.
            using (var grown = new FileStream(path, FileMode.Open))
                grown.SetLength(300L * 1024 * 1024);
            Run();
            new FileInfo(path).Length.Should().BeLessThan(GraphicsDevice.PipelineCacheBound, "a file past the bound was read as empty and the run's pipelines written in its place");
        }
        finally
        {
            UserCache.Folder = null!;
        }
    }

    [NeedsVulkanFact]
    public void A_Run_Writes_The_Pipelines_It_Compiled_And_A_Cache_Another_Device_Wrote_Is_Read_As_Empty()
    {
        var path = GraphicsDevice.PipelineCachePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // A cache another GPU wrote, the header Vulkan gives one naming a vendor of 0xFFFF.
        var foreign = new byte[64];
        BinaryPrimitives.WriteUInt32LittleEndian(foreign, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(foreign.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(foreign.AsSpan(8), 0xFFFF);
        File.WriteAllBytes(path, foreign);

        Run();
        var written = File.ReadAllBytes(path);
        // A driver that keeps no pipelines in the cache, as lavapipe with Mesa's own shader cache
        // off, writes nothing over it.
        if (written.SequenceEqual(foreign)) return;
        // The run's cache is written over the one it could not read, its header naming this device,
        // and on a lavapipe that keeps no pipelines in it the header is all of it, 32 bytes.
        written.Length.Should().BeGreaterThanOrEqualTo(32, "the run's cache is written over the one it could not read, its header at least");
        BinaryPrimitives.ReadUInt32LittleEndian(written).Should().Be(32u, "the header says its own size");
        BinaryPrimitives.ReadUInt32LittleEndian(written.AsSpan(8)).Should().NotBe(0xFFFFu, "the header names the device that wrote it");

        // A run that compiles nothing the file lacks leaves it as it was.
        var at = File.GetLastWriteTimeUtc(path);
        Run();
        File.GetLastWriteTimeUtc(path).Should().Be(at, "the second run's pipelines were all in the cache it read");
        File.ReadAllBytes(path).Should().Equal(written);
    }

    [NeedsVulkanFact]
    public void A_Pipeline_The_Device_Makes_Is_Named_By_The_Shader_It_Is_Made_From_With_Its_Time()
    {
        var config = Config.Default.WithWindow("pipeline names", 64, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        BeginDrawing();
        ClearBackground(Color.Black);
        EndDrawing();
        var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
        device.TakePipelinesNamed();

        var spirv = SlangCompiler.Compile("""
            RWStructuredBuffer<uint> counts;

            [shader("compute")]
            [numthreads(1, 1, 1)]
            void computeMain(uint3 id : SV_DispatchThreadID)
            {
                counts[id.x] = id.x;
            }
            """, "named.slang", "computeMain", ShaderStage.Compute);
        using (device.CreateComputePipeline(spirv, 0, [0]))
            device.TakePipelinesNamed().Should().ContainSingle().Which.Should().MatchRegex(@"^named\.slang's computeMain in \d+ ms$",
                "the frame's line in the log names each pipeline the driver made by its shaders and its time");
    }
}

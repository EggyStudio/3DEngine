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

    // Opens a window, draws a lit cube for a few frames and closes it, as a run of a program does,
    // and answers whether the device said its driver's cache held pipelines past its header.
    private static bool Run()
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
        var holds = ((GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!).PipelineCacheHolds;
        CloseWindow();
        UseApp(null);
        return holds;
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
    public void The_Device_Sweeps_The_Pipelines_Of_Other_Versions_No_Run_Wrote_Lately()
    {
        // A file another package of the engine wrote long ago, in its version's folder, which
        // the device's cache, opened under this version, leaves no more.
        using var cache = new TestFolder("engine-pipelines-");
        UserCache.Folder = cache.Path;
        try
        {
            var old = Path.Combine(cache.Path, "pipelines", "6.2.17", "testhost.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(old)!);
            File.WriteAllBytes(old, [1, 2, 3]);
            File.SetLastWriteTimeUtc(old, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            Run();
            Directory.Exists(Path.GetDirectoryName(old)).Should().BeFalse("another version's pipelines no run wrote lately are swept when the cache is opened");
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

        var holds = Run();
        var written = File.ReadAllBytes(path);
        // A driver whose cache the device says holds nothing past its header, as lavapipe's,
        // writes nothing over it, by the one rule the write and the device's answer share.
        if (!holds)
        {
            written.Should().Equal(foreign, "a cache that holds nothing past its header is not written");
            return;
        }
        // The run's cache is written over the one it could not read, its header naming this device
        // and pipelines past it.
        written.Length.Should().BeGreaterThan(32, "the run's cache is written over the one it could not read, pipelines past its header");
        BinaryPrimitives.ReadUInt32LittleEndian(written).Should().Be(32u, "the header says its own size");
        BinaryPrimitives.ReadUInt32LittleEndian(written.AsSpan(8)).Should().NotBe(0xFFFFu, "the header names the device that wrote it");

        // A run that compiles nothing the file lacks leaves it as it was.
        var at = File.GetLastWriteTimeUtc(path);
        Run();
        File.GetLastWriteTimeUtc(path).Should().Be(at, "the second run's pipelines were all in the cache it read");
        File.ReadAllBytes(path).Should().Equal(written);
    }

    [NeedsVulkanFact]
    public void The_Write_Waits_For_A_Compile_In_Flight_And_A_Served_Program_Answers_In_The_Frame_After()
    {
        // A served program's renderer writes the driver's cache on the main thread while a worker
        // makes a pipeline through it, held in the driver by the test. The write reads the cache
        // only once the compile has let it go, and a command sent then is answered in the frames
        // after, as a game driven through e3d is.
        using var sessions = new TestFolder("engine-sessions-");
        using var cache = new TestFolder("engine-pipelines-");
        var previous = CliSessionFile.Directory;
        CliSessionFile.Directory = sessions.Path;
        UserCache.Folder = cache.Path;
        try
        {
            var config = Config.Default.WithWindow("pipeline write", 64, 64) with { Headless = true, Offscreen = true, Samples = 1, Serve = true };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            var camera = new Camera3D(new Vector3(2, 2, 2), Vector3.Zero, Vector3.UnitY, 45);
            void Frame()
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawCube(Vector3.Zero, 1, 1, 1, Color.White);
                EndMode3D();
                EndDrawing();
            }
            Frame();
            var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
            var spirv = SlangCompiler.Compile("""
                RWStructuredBuffer<uint> counts;

                [shader("compute")]
                [numthreads(1, 1, 1)]
                void computeMain(uint3 id : SV_DispatchThreadID)
                {
                    counts[id.x] = id.x;
                }
                """, "in-flight.slang", "computeMain", ShaderStage.Compute);

            using var inside = new ManualResetEventSlim();
            using var gate = new ManualResetEventSlim();
            var released = 0;
            device.WhileMakingPipeline = () =>
            {
                inside.Set();
                gate.Wait();
            };
            var compile = Task.Run(() => device.CreateComputePipeline(spirv, 0, [0]));
            inside.Wait(TimeSpan.FromSeconds(60)).Should().BeTrue("the worker's compile reached the driver");
            device.WhileMakingPipeline = null;
            // The compile let go a while after the write was asked for, the release marked first.
            var release = Task.Run(() =>
            {
                Thread.Sleep(300);
                Volatile.Write(ref released, 1);
                gate.Set();
            });
            device.SavePipelineCache();
            Volatile.Read(ref released).Should().Be(1, "the write read the driver's cache only after the compile in flight let it go");
            release.Wait();
            using (compile.Result) { }

            var session = CliSessionFile.Read(CliSessionFile.PathFor(Environment.ProcessId));
            session.Should().NotBeNull("the served program has its session file");
            var answer = Task.Run(() => CliClient.Send(session!, "run", "frames.wait 1", 60));
            for (int frame = 0; frame < 2000 && !answer.IsCompleted; frame++)
                Frame();
            answer.IsCompleted.Should().BeTrue("the program answers a command in the frames after the write");
            using var said = System.Text.Json.JsonDocument.Parse(answer.Result);
            said.RootElement.GetProperty("success").GetBoolean().Should().BeTrue(answer.Result);
        }
        finally
        {
            CloseWindow();
            UseApp(null);
            CliSessionFile.Directory = previous;
            UserCache.Folder = null!;
        }
    }

    [NeedsVulkanFact]
    public void The_Frames_Go_On_While_Their_Write_Waits_For_A_Compile_In_Flight()
    {
        // The write the frames ask for once QuietFrames have made no pipeline waits for a worker's
        // compile, held in the driver by the test, and the frames after it are drawn meanwhile, the
        // file written once the compile lets the cache go.
        using var cache = new TestFolder("engine-pipelines-");
        UserCache.Folder = cache.Path;
        using var gate = new ManualResetEventSlim();
        Task<ComputePipeline>? compile = null;
        try
        {
            var config = Config.Default.WithWindow("pipeline write aside", 64, 64) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            var camera = new Camera3D(new Vector3(2, 2, 2), Vector3.Zero, Vector3.UnitY, 45);
            void Frame()
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawCube(Vector3.Zero, 1, 1, 1, Color.White);
                EndMode3D();
                EndDrawing();
            }
            Frame();
            var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
            if (!device.PipelineCacheHolds) return;
            var spirv = SlangCompiler.Compile("""
                RWStructuredBuffer<uint> counts;

                [shader("compute")]
                [numthreads(1, 1, 1)]
                void computeMain(uint3 id : SV_DispatchThreadID)
                {
                    counts[id.x] = id.x + 2;
                }
                """, "aside.slang", "computeMain", ShaderStage.Compute);
            using var inside = new ManualResetEventSlim();
            device.WhileMakingPipeline = () =>
            {
                inside.Set();
                gate.Wait(TimeSpan.FromSeconds(60));
            };
            compile = Task.Run(() => device.CreateComputePipeline(spirv, 0, [0]));
            inside.Wait(TimeSpan.FromSeconds(60)).Should().BeTrue("the worker's compile reached the driver");
            device.WhileMakingPipeline = null;

            // Twice QuietFrames drawn with the compile held, which a write on the frame's thread
            // stops at the first quiet stretch's end until the gate gives way, the file then
            // written before the frames go on.
            for (int frame = 0; frame < 240; frame++)
                Frame();
            File.Exists(GraphicsDevice.PipelineCachePath).Should().BeFalse("the frames were drawn while the write waited for the compile in flight");
            gate.Set();
            for (int frame = 0; frame < 2000 && !File.Exists(GraphicsDevice.PipelineCachePath); frame++)
                Frame();
            File.Exists(GraphicsDevice.PipelineCachePath).Should().BeTrue("the write went on once the compile let the cache go");
        }
        finally
        {
            gate.Set();
            if (compile is not null) using (compile.Result) { }
            CloseWindow();
            UseApp(null);
            UserCache.Folder = null!;
        }
    }

    [NeedsVulkanFact]
    public void A_Compile_On_One_Thread_Is_Not_Held_Behind_One_In_Flight_On_Another()
    {
        // A game's mirror asks its pipelines of workers while the frame's own thread makes a
        // shader's that takes the driver a second and more, and the mirror showed nothing until
        // both were made one after the other. The driver's cache takes compiles at once from any
        // thread, so one held in the driver by the test leaves another to finish beside it.
        var config = Config.Default.WithWindow("pipelines at once", 64, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        BeginDrawing();
        ClearBackground(Color.Black);
        EndDrawing();
        var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
        var spirv = SlangCompiler.Compile("""
            RWStructuredBuffer<uint> counts;

            [shader("compute")]
            [numthreads(1, 1, 1)]
            void computeMain(uint3 id : SV_DispatchThreadID)
            {
                counts[id.x] = id.x + 1;
            }
            """, "at-once.slang", "computeMain", ShaderStage.Compute);

        using var inside = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        var heldThread = 0;
        device.WhileMakingPipeline = () =>
        {
            if (Interlocked.CompareExchange(ref heldThread, Environment.CurrentManagedThreadId, 0) != 0) return;
            inside.Set();
            gate.Wait(TimeSpan.FromSeconds(30));
        };
        var held = Task.Run(() => device.CreateComputePipeline(spirv, 0, [0]));
        bool finished;
        try
        {
            inside.Wait(TimeSpan.FromSeconds(60)).Should().BeTrue("the first compile reached the driver");
            var other = Task.Run(() => device.CreateComputePipeline(spirv, 0, [0]));
            finished = other.Wait(TimeSpan.FromSeconds(15));
            gate.Set();
            other.Wait();
            using (other.Result) { }
        }
        finally
        {
            gate.Set();
            device.WhileMakingPipeline = null;
            using (held.Result) { }
        }
        finished.Should().BeTrue("the second compile finished while the first was held in the driver");
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
        // Made twice from the one SPIR-V, the second found in the cache the first went into, where
        // the driver says where each came from and its cache keeps pipelines past its header, as a
        // software device's and lavapipe's with Mesa's own cache off do not.
        using (device.CreateComputePipeline(spirv, 0, [0]))
        using (device.CreateComputePipeline(spirv, 0, [0]))
        {
            var lines = device.TakePipelinesNamed();
            lines.Should().HaveCount(2).And.AllSatisfy(line => line.Should().MatchRegex(
                @"^named\.slang's computeMain in \d+ ms, (compiled|from the cache|the driver saying not where from), stages [0-9a-f]{8}$",
                "the frame's line in the log names each pipeline the driver made by its shaders, its time, where it came from and its stages' hash"));
            lines[1][^8..].Should().Be(lines[0][^8..], "the stages' hash is of their bytes alone");
            if (device.PipelineCacheHolds && !lines.Any(line => line.Contains("saying not")))
                lines[1].Should().Contain("from the cache", "the second was made from the bytes the first put in the cache");
        }
    }
}

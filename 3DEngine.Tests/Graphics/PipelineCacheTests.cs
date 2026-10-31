using System.Buffers.Binary;
using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Graphics;

/// <summary>
/// The driver's compiled pipelines kept in the program's cache folder between runs
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
        written.Length.Should().BeGreaterThan(foreign.Length, "the run's pipelines are written over the cache it could not read");
        BinaryPrimitives.ReadUInt32LittleEndian(written.AsSpan(8)).Should().NotBe(0xFFFFu, "the header names the device that wrote it");

        // A run that compiles nothing the file lacks leaves it as it was.
        var at = File.GetLastWriteTimeUtc(path);
        Run();
        File.GetLastWriteTimeUtc(path).Should().Be(at, "the second run's pipelines were all in the cache it read");
        File.ReadAllBytes(path).Should().Equal(written);
    }
}

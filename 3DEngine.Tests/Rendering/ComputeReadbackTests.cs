using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A storage buffer read back without a wait: asked for after a dispatch, not made in the frame it
/// was asked in, read right a few frames on, and asked every frame without its buffers growing.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class ComputeReadbackTests : IDisposable
{
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public ComputeReadbackTests()
    {
        var config = Config.Default.WithWindow("readback test", 32, 32) with { Headless = true, Offscreen = true };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    public void Dispose()
    {
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the copies");
        CloseWindow();
        UseApp(null);
    }

    // Writes each value's index plus a start, a dispatch of 64 threads a group.
    private const string Sequence = """
        uniform uint count;
        uniform uint start;
        RWStructuredBuffer<uint> values;

        [shader("compute")]
        [numthreads(64, 1, 1)]
        void computeMain(uint3 id : SV_DispatchThreadID)
        {
            if (id.x < count) values[id.x] = start + id.x;
        }
        """;

    private static GraphicsDevice Device => (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics;

    // A shader writing 256 values into a buffer of its own, dispatched once from start.
    private static (Shader Shader, ShaderBuffer Values) Written(uint start = 0)
    {
        var shader = LoadComputeShaderFromMemory(Sequence, "sequence.slang");
        shader.IsValid.Should().BeTrue();
        var values = LoadShaderBuffer(256 * 4);
        SetShaderValue(shader, GetShaderLocation(shader, "count"), 256);
        SetShaderValueBuffer(shader, GetShaderLocation(shader, "values"), values);
        Dispatch(shader, start);
        return (shader, values);
    }

    private static void Dispatch(Shader shader, uint start)
    {
        SetShaderValue(shader, GetShaderLocation(shader, "start"), (int)start);
        ComputeShaderDispatch(shader, 4, 1, 1);
    }

    private static void Frame()
    {
        BeginDrawing();
        ClearBackground(Color.Black);
        EndDrawing();
    }

    [NeedsVulkanFact]
    public void A_Readback_Asked_After_A_Dispatch_Is_Not_Made_In_Its_Frame_And_Reads_What_The_Dispatch_Wrote_After()
    {
        Frame();
        BeginDrawing();
        var (shader, values) = Written();
        var readback = ReadShaderBufferAsync(values);
        readback.Size.Should().Be(1024, "the size 0 reads the whole buffer");
        IsShaderReadbackReady(readback).Should().BeFalse("the copy goes to the GPU with the frame's submission");
        // A dispatch after the ask writes what the readback does not read.
        Dispatch(shader, 1000);
        ClearBackground(Color.Black);
        EndDrawing();

        var frames = 1;
        while (!IsShaderReadbackReady(readback) && frames < 10)
        {
            Frame();
            frames++;
        }
        IsShaderReadbackReady(readback).Should().BeTrue($"the copy is made within the frames in flight, {frames} drawn");
        var result = new uint[256];
        GetShaderReadback<uint>(readback, result);
        result.Should().Equal(Enumerable.Range(0, 256).Select(i => (uint)i), "the readback holds what the dispatch before it wrote");
        UnloadShaderReadback(readback);
        IsShaderReadbackReady(readback).Should().BeFalse("an unloaded readback is no longer known");
        UnloadShaderBuffer(values);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Readback_Read_In_The_Frame_It_Was_Asked_In_Waits_And_Reads_Right()
    {
        BeginDrawing();
        var (shader, values) = Written(7);
        var readback = ReadShaderBufferAsync(values, 16, 32);
        readback.Size.Should().Be(32);
        var result = new uint[8];
        GetShaderReadback<uint>(readback, result);
        result.Should().Equal(Enumerable.Range(4, 8).Select(i => (uint)(7 + i)), "the bytes from the offset, the copy sent and waited for");
        IsShaderReadbackReady(readback).Should().BeTrue();
        ClearBackground(Color.Black);
        EndDrawing();
        UnloadShaderReadback(readback);
        UnloadShaderBuffer(values);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Readback_Asked_And_Unloaded_Every_Frame_Keeps_A_Few_Buffers_And_No_More()
    {
        var (shader, values) = Written();
        // Each frame slot's arena makes the buffer a frame's dispatch takes its uniforms from the
        // first time the slot draws one, and keeps it for the slot's later frames, so a frame of
        // the loop's own work in every slot comes before the count the device returns to.
        for (var frame = 0; frame < GraphicsDevice.ReadbackStagingKept; frame++)
        {
            BeginDrawing();
            Dispatch(shader, 0);
            ClearBackground(Color.Black);
            EndDrawing();
        }
        // The dispatches' own buffers let go first, as they are again before the count after.
        Device.WaitIdle();
        Device.WaitForCompute();
        var buffers = Device.Usage.Buffers;
        var census = Device.BufferCensus();
        var pending = new Queue<ShaderReadback>();
        for (var frame = 0; frame < 100; frame++)
        {
            BeginDrawing();
            Dispatch(shader, (uint)frame);
            pending.Enqueue(ReadShaderBufferAsync(values));
            // Each read the frame it is ready, and let go, as a game asking every frame does.
            while (pending.Count > 0 && IsShaderReadbackReady(pending.Peek()))
            {
                var ready = pending.Dequeue();
                var first = new uint[1];
                GetShaderReadback<uint>(ready, first);
                UnloadShaderReadback(ready);
            }
            ClearBackground(Color.Black);
            EndDrawing();
        }
        foreach (var left in pending) UnloadShaderReadback(left);
        for (var frame = 0; frame < 5; frame++) Frame();

        // The pool's own count, which holds nothing of the device's other transients, keeps as many
        // of a size as frames can be in flight and one, however many a device's timing has waiting
        // at once.
        Device.ReadbackStagingCount.Should().BeLessThanOrEqualTo(GraphicsDevice.ReadbackStagingKept, "the buffers asked in flight go back to the pool and are taken again");

        // With the device idle nothing is in flight, so it holds what it held at the start and the
        // pool's buffers, whatever a machine's timing, and a leak of one a frame shows past them.
        Device.WaitIdle();
        Device.WaitForCompute();
        (Device.Usage.Buffers - Device.ReadbackStagingCount).Should().Be(buffers,
            $"the device holds no buffer past the pool's once idle, and over the start it holds {OverStart(census)}");
        UnloadShaderBuffer(values);
        UnloadShader(shader);
    }

    // The buffers the device holds over the census taken at the start, each by its size and use and
    // where it was made, so a run that fails says what it kept.
    private static string OverStart(IReadOnlyList<BufferCount> start)
    {
        var before = start.ToDictionary(line => line.Maker, line => line.Count);
        var over = Device.BufferCensus()
            .Select(line => (line, Count: line.Count - before.GetValueOrDefault(line.Maker)))
            .Where(entry => entry.Count != 0)
            .Select(entry => $"{entry.Count} of {entry.line.Bytes / entry.line.Count} bytes for {entry.line.Maker.Usage} from {Path.GetFileName(entry.line.Maker.File)}:{entry.line.Maker.Line}")
            .ToArray();
        return over.Length == 0 ? "nothing" : string.Join(", ", over);
    }

    [NeedsVulkanFact]
    public void A_Readback_Past_The_Buffers_End_Is_Cut_Short_And_One_Of_No_Buffer_Is_Invalid()
    {
        var (shader, values) = Written();
        var readback = ReadShaderBufferAsync(values, 1000, 100);
        readback.Size.Should().Be(24, "the bytes from the offset to the buffer's end");
        UnloadShaderReadback(readback);
        ReadShaderBufferAsync(values, 1024).IsValid.Should().BeFalse("the offset lies at the buffer's end");
        ReadShaderBufferAsync(default).IsValid.Should().BeFalse("no buffer is loaded");
        UnloadShaderBuffer(values);
        UnloadShader(shader);
    }
}

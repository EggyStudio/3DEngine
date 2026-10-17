using FluentAssertions;

namespace Engine.Tests.Graphics;

/// <summary>
/// Graphics pipelines made on a real Vulkan device while another thread compacts the managed heap
/// again and again, so a stage's entry point name read through a pointer into memory a collection
/// moves is read where that memory was.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class PipelineEntryNameTests
{
    // A triangle from the vertex's index and a flat color, with no input, binding or constant that
    // the pipeline would have to declare beside its stages.
    private const string Flat = """
        [shader("vertex")]
        float4 vertexMain(uint id : SV_VertexID) : SV_Position
        {
            return float4(float(id & 1u) * 2.0 - 1.0, float(id >> 1u) * 2.0 - 1.0, 0.0, 1.0);
        }

        [shader("fragment")]
        float4 fragmentMain() : SV_Target
        {
            return float4(1.0, 0.0, 0.0, 1.0);
        }
        """;

    [NeedsVulkanFact]
    public void Pipelines_Made_While_The_Heap_Is_Compacted_Name_Their_Entry_Point()
    {
        var errorsBefore = GraphicsDevice.ValidationErrors.Count;
        var program = new SlangLoader().Compile(Flat, "flat.slang");
        using var device = new GraphicsDevice();
        device.Initialize(new OffscreenSurface(16, 16), "pipeline name test");
        var vertex = device.CreateShader(new ShaderDesc(ShaderStage.Vertex, program.Vertex));
        var fragment = device.CreateShader(new ShaderDesc(ShaderStage.Fragment, program.Fragment));

        // A name read through a pointer into an array of its bytes was read as empty where a
        // collection had moved the array and what was allocated after had cleared where it lay, on
        // one run of the suite in many, which collections forced over and over with allocations
        // between make the common case.
        using var stop = new CancellationTokenSource();
        var compactor = Task.Run(() =>
        {
            var held = new byte[64][];
            while (!stop.IsCancellationRequested)
            {
                GC.Collect(0, GCCollectionMode.Forced, blocking: true, compacting: true);
                for (int i = 0; i < 4096; i++) held[i % held.Length] = new byte[32];
            }
        });
        try
        {
            for (int i = 0; i < 20; i++)
                device.CreateGraphicsPipeline(new GraphicsPipelineDesc(device.SwapchainRenderPass, vertex, fragment, Cull: CullMode.None)).Dispose();
        }
        finally
        {
            stop.Cancel();
            compactor.Wait();
        }

        vertex.Dispose();
        fragment.Dispose();
        GraphicsDevice.ValidationErrors.Skip(errorsBefore).Should().BeEmpty("every pipeline names the entry point its stages' modules hold");
    }

    [Fact]
    public void A_Shader_Given_No_Entry_Point_Name_Is_Refused_Where_It_Is_Made()
    {
        var made = () => new GraphicsDevice().CreateShader(new ShaderDesc(ShaderStage.Vertex, new byte[] { 3, 2, 35, 7 }, ""));

        made.Should().Throw<ArgumentException>().WithMessage("*Vertex shader*no entry point name*");
    }
}

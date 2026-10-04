using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>Runs compute shaders through the flat API on a real Vulkan device and reads their buffers back.</summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class ComputeTests : IDisposable
{
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public ComputeTests()
    {
        var config = Config.Default.WithWindow("compute test", 32, 32) with { Headless = true, Offscreen = true };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    public void Dispose()
    {
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the dispatches");
        CloseWindow();
        UseApp(null);
    }

    private const string Scale = """
        uniform float scale;
        uniform uint count;
        RWStructuredBuffer<float> values;
        StructuredBuffer<float> offsets;

        [shader("compute")]
        [numthreads(64, 1, 1)]
        void computeMain(uint3 id : SV_DispatchThreadID)
        {
            if (id.x < count)
                values[id.x] = values[id.x] * scale + offsets[id.x];
        }
        """;

    [NeedsVulkanFact]
    public void A_Dispatch_Writes_What_Its_Uniforms_And_Buffers_Say_And_Reads_Back()
    {
        var shader = LoadComputeShaderFromMemory(Scale, "scale.slang");
        shader.IsValid.Should().BeTrue();
        const int Count = 1000;
        var values = LoadShaderBuffer<float>(Enumerable.Range(0, Count).Select(i => (float)i).ToArray());
        var offsets = LoadShaderBuffer<float>(Enumerable.Repeat(0.5f, Count).ToArray());
        SetShaderValue(shader, GetShaderLocation(shader, "scale"), 2f);
        SetShaderValue(shader, GetShaderLocation(shader, "count"), Count);
        SetShaderValueBuffer(shader, GetShaderLocation(shader, "values"), values);
        SetShaderValueBuffer(shader, GetShaderLocation(shader, "offsets"), offsets);

        // Two dispatches in a row, the second reading what the first wrote.
        ComputeShaderDispatch(shader, (Count + 63) / 64, 1, 1);
        ComputeShaderDispatch(shader, (Count + 63) / 64, 1, 1);

        var result = new float[Count];
        ReadShaderBuffer<float>(values, result);
        for (int i = 0; i < Count; i++) result[i].Should().Be((i * 2 + 0.5f) * 2 + 0.5f, $"value {i} is scaled and offset twice");

        UnloadShaderBuffer(values);
        UnloadShaderBuffer(offsets);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Dispatch_With_A_Buffer_Unset_Is_Skipped_And_A_Shader_With_No_Compute_Stage_Is_Refused()
    {
        var shader = LoadComputeShaderFromMemory(Scale, "scale.slang");
        shader.IsValid.Should().BeTrue();
        var values = LoadShaderBuffer<float>([1f, 2f]);
        values.IsValid.Should().BeTrue();
        SetShaderValueBuffer(shader, GetShaderLocation(shader, "values"), values);
        SetShaderValue(shader, GetShaderLocation(shader, "count"), 2);
        ComputeShaderDispatch(shader, 1, 1, 1);

        var result = new float[2];
        ReadShaderBuffer<float>(values, result);
        result.Should().Equal([1f, 2f], "with no buffer set for offsets nothing ran");

        LoadComputeShaderFromMemory("""
            import engine;
            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target { return input.color; }
            """, "flat.slang").IsValid.Should().BeFalse();

        // Left loaded, the buffer and the shader's pipeline are freed with the window.
    }
}

using FluentAssertions;

namespace Engine.Tests.Graphics;

[Trait("Category", "Unit")]
public class SlangCompilerTests : IDisposable
{
    private const string Source = """
        struct Output { float4 position : SV_Position; };

        [shader("vertex")]
        Output vertexMain(float3 position : POSITION)
        {
            Output output;
            output.position = float4(position, 1.0);
            return output;
        }

        [shader("fragment")]
        float4 fragmentMain(Output input) : SV_Target
        {
            return float4(1.0, 0.5, 0.25, 1.0);
        }
        """;

    // The first word of every SPIR-V module, little-endian.
    private static readonly byte[] SpirvMagic = [0x03, 0x02, 0x23, 0x07];

    private readonly string _cache = Directory.CreateTempSubdirectory("engine-slang-test-").FullName;

    public void Dispose() => Directory.Delete(_cache, recursive: true);

    [Fact]
    public void EntryPoints_Are_Found_With_Their_Stages()
    {
        SlangLoader.EntryPoints(Source).Should().Equal(
            ("vertexMain", ShaderStage.Vertex),
            ("fragmentMain", ShaderStage.Fragment));
    }

    [Fact]
    public void A_Program_Compiles_To_Spirv_For_Each_Stage()
    {
        if (!SlangCompiler.Available) return;

        var program = new SlangLoader(_cache).Compile(Source, "test.slang");

        program.Vertex.Take(4).Should().Equal(SpirvMagic);
        program.Fragment.Take(4).Should().Equal(SpirvMagic);
    }

    [Fact]
    public void A_Cached_Entry_Loads_Without_A_Compiler()
    {
        if (!SlangCompiler.Available) return;

        var compiled = SlangCompiler.Compile(Source, "test.slang", "vertexMain", ShaderStage.Vertex, _cache);
        var cached = SlangCompiler.Compile(Source, "test.slang", "vertexMain", ShaderStage.Vertex, _cache, null, compiler: null);

        cached.Should().Equal(compiled);
    }

    [Fact]
    public void A_Changed_Source_Misses_The_Cache()
    {
        if (!SlangCompiler.Available) return;

        SlangCompiler.Compile(Source, "test.slang", "vertexMain", ShaderStage.Vertex, _cache);
        var changed = Source.Replace("1.0, 0.5", "0.0, 0.5");

        var act = () => SlangCompiler.Compile(changed, "test.slang", "vertexMain", ShaderStage.Vertex, _cache, null, compiler: null);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not in the shader cache*");
    }

    [Fact]
    public void A_Compile_Error_Names_The_File()
    {
        if (!SlangCompiler.Available) return;

        var act = () => SlangCompiler.Compile("float4 broken(", "broken.slang", "broken", ShaderStage.Fragment, _cache);

        act.Should().Throw<InvalidOperationException>().WithMessage("*broken.slang*");
    }

    [Fact]
    public void The_Built_In_Shaders_Compile()
    {
        if (!SlangCompiler.Available) return;

        var shaders = Path.Combine(AppContext.BaseDirectory, "source", "shaders");
        foreach (var file in new[] { "mesh.slang", "imgui.slang" })
        {
            var program = new SlangLoader(_cache, shaders).Compile(File.ReadAllText(Path.Combine(shaders, file)), file);
            program.Stages.Keys.Should().BeEquivalentTo([ShaderStage.Vertex, ShaderStage.Fragment], file);
        }
    }
}

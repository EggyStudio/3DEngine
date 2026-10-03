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

    [NeedsSlangFact]
    public void A_Program_Compiles_To_Spirv_For_Each_Stage()
    {
        
        var program = new SlangLoader(_cache).Compile(Source, "test.slang");

        program.Vertex.Take(4).Should().Equal(SpirvMagic);
        program.Fragment.Take(4).Should().Equal(SpirvMagic);
    }

    [NeedsSlangFact]
    public void A_Cached_Entry_Loads_Without_A_Compiler()
    {
        
        var compiled = SlangCompiler.Compile(Source, "test.slang", "vertexMain", ShaderStage.Vertex, _cache);
        var cached = SlangCompiler.Compile(Source, "test.slang", "vertexMain", ShaderStage.Vertex, _cache, null, compiler: null);

        cached.Should().Equal(compiled);
    }

    [NeedsSlangFact]
    public void A_Changed_Source_Misses_The_Cache()
    {
        
        SlangCompiler.Compile(Source, "test.slang", "vertexMain", ShaderStage.Vertex, _cache);
        var changed = Source.Replace("1.0, 0.5", "0.0, 0.5");

        var act = () => SlangCompiler.Compile(changed, "test.slang", "vertexMain", ShaderStage.Vertex, _cache, null, compiler: null);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not in the shader cache*");
    }

    [NeedsSlangFact]
    public void A_Compile_Error_Names_The_File()
    {
        
        var act = () => SlangCompiler.Compile("float4 broken(", "broken.slang", "broken", ShaderStage.Fragment, _cache);

        act.Should().Throw<InvalidOperationException>().WithMessage("*broken.slang*");
    }

    [NeedsSlangFact]
    public void The_Built_In_Shaders_Compile()
    {
        
        var shaders = Path.Combine(AppContext.BaseDirectory, "source", "shaders");
        foreach (var file in new[] { "imgui.slang", "immediate.slang", "model.slang" })
        {
            var program = new SlangLoader(_cache, shaders).Compile(File.ReadAllText(Path.Combine(shaders, file)), file);
            program.Stages.Keys.Should().BeEquivalentTo([ShaderStage.Vertex, ShaderStage.Fragment], file);
        }
    }

    [Fact]
    public void A_Cache_Key_Follows_Imports_With_Forward_Slashes_And_Counts_Missing_Ones()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_cache, "imports")).FullName;
        Directory.CreateDirectory(Path.Combine(folder, "lights"));
        File.WriteAllText(Path.Combine(folder, "engine.slang"), "module engine;");
        File.WriteAllText(Path.Combine(folder, "lights", "point-light.slang"), "import engine;\n#include \"common.slang\"");
        File.WriteAllText(Path.Combine(folder, "unrelated.slang"), "module unrelated;");

        var files = SlangCompiler.ImportedFiles("import lights.point_light;\nimport engine;", folder).Select(f => (f.Path, f.Bytes is not null));

        files.Should().Equal(("lights/point-light.slang", true), ("engine.slang", true), ("?common.slang", false));
    }

    [NeedsSlangFact]
    public void A_Shipped_Cache_Still_Serves_After_A_Shader_Of_Its_Own_Is_Added_Beside_It()
    {
        // A copy of the built-in shaders, compiled into a cache as build/pack.sh does.
        var shaders = Directory.CreateDirectory(Path.Combine(_cache, "shaders")).FullName;
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "source", "shaders"), "*.slang"))
            File.Copy(file, Path.Combine(shaders, Path.GetFileName(file)));
        var cache = Path.Combine(_cache, "cache");
        SlangLoader.Precompile(shaders, cache);

        // A game adds a shader of its own to the same folder.
        File.WriteAllText(Path.Combine(shaders, "mine.slang"), "import engine;\n[shader(\"fragment\")] float4 fragmentMain() : SV_Target { return 1; }");

        var model = File.ReadAllText(Path.Combine(shaders, "model.slang"));
        var immediate = File.ReadAllText(Path.Combine(shaders, "immediate.slang"));
        var load = () => SlangCompiler.Compile(model, "model.slang", "vertexMain", ShaderStage.Vertex, cache, shaders, compiler: null);
        load.Should().NotThrow("model.slang imports nothing the game's shader changed");
        SlangCompiler.Compile(immediate, "immediate.slang", "fragmentMain", ShaderStage.Fragment, cache, shaders, compiler: null)
            .Take(4).Should().Equal(SpirvMagic);

        // What a shader imports does change its key.
        File.AppendAllText(Path.Combine(shaders, "engine.slang"), "\n// changed\n");
        var stale = () => SlangCompiler.Compile(immediate, "immediate.slang", "fragmentMain", ShaderStage.Fragment, cache, shaders, compiler: null);
        stale.Should().Throw<InvalidOperationException>().WithMessage("*not in the shader cache*");
    }

    [NeedsSlangFact]
    public void Top_Level_Uniforms_Are_Reflected_And_Kept_In_The_Cache()
    {
        const string withUniforms = """
            uniform float4 tint;
            uniform float strength;

            [shader("fragment")]
            float4 fragmentMain() : SV_Target
            {
                return tint * strength;
            }
            """;

        var compiled = SlangCompiler.CompileStage(withUniforms, "tinted.slang", "fragmentMain", ShaderStage.Fragment, _cache);
        var cached = SlangCompiler.CompileStage(withUniforms, "tinted.slang", "fragmentMain", ShaderStage.Fragment, _cache, null, compiler: null);

        compiled.Uniforms.Should().Equal(new ShaderUniform("tint", 0, 16), new ShaderUniform("strength", 16, 4));
        cached.Uniforms.Should().Equal(compiled.Uniforms);
        new ShaderProgram("tinted.slang", new Dictionary<ShaderStage, byte[]> { [ShaderStage.Fragment] = compiled.Spirv }, compiled.Uniforms)
            .UniformSize.Should().Be(32);
    }
}

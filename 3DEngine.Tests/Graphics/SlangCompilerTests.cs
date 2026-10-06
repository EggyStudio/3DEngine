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

    private readonly TestFolder _folder = new("engine-slang-test-");

    public void Dispose() => _folder.Dispose();

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
        
        var program = new SlangLoader(_folder.Path).Compile(Source, "test.slang");

        program.Vertex.Take(4).Should().Equal(SpirvMagic);
        program.Fragment.Take(4).Should().Equal(SpirvMagic);
    }

    [NeedsSlangFact]
    public void A_Stage_Says_How_Many_Input_Locations_It_Reads()
    {
        var program = new SlangLoader(_folder.Path).Compile("""
            struct Output { float4 position : SV_Position; float4 color : COLOR0; float2 uv : TEXCOORD0; };

            [shader("vertex")]
            Output vertexMain(float3 position : POSITION, [[vk::location(5)]] float4 color : COLOR0, uint id : SV_VertexID)
            {
                Output output;
                output.position = float4(position, 1.0);
                output.color = color;
                output.uv = float2(id, 0.0);
                return output;
            }

            [shader("fragment")]
            float4 fragmentMain(Output input) : SV_Target
            {
                return input.color * input.uv.x;
            }
            """, "inputs.slang");

        // The vertex stage's color sits at location 5 and its vertex index is built in, and the
        // fragment stage reads the color and the coordinate at 0 and 1 beside the built-in position.
        program.InputLocations(ShaderStage.Vertex).Should().Be(6);
        program.InputLocations(ShaderStage.Fragment).Should().Be(2);
        program.InputLocations(ShaderStage.Compute).Should().Be(0);
        ShaderProgram.OutputLocations(program.Fragment).Should().Be(1, "the fragment stage writes one color");
        ShaderProgram.OutputLocations(program.Vertex).Should().Be(2, "and the vertex stage the color and the coordinate beside the built-in position");
    }

    [NeedsSlangFact]
    public void A_Cached_Entry_Loads_Without_A_Compiler()
    {
        
        var compiled = SlangCompiler.Compile(Source, "test.slang", "vertexMain", ShaderStage.Vertex, _folder.Path);
        var cached = SlangCompiler.Compile(Source, "test.slang", "vertexMain", ShaderStage.Vertex, _folder.Path, null, compiler: null);

        cached.Should().Equal(compiled);
    }

    [NeedsSlangFact]
    public void A_Changed_Source_Misses_The_Cache()
    {
        
        SlangCompiler.Compile(Source, "test.slang", "vertexMain", ShaderStage.Vertex, _folder.Path);
        var changed = Source.Replace("1.0, 0.5", "0.0, 0.5");

        var act = () => SlangCompiler.Compile(changed, "test.slang", "vertexMain", ShaderStage.Vertex, _folder.Path, null, compiler: null);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not in the shader cache*");
    }

    [NeedsSlangFact]
    public void A_Compile_Error_Names_The_File()
    {
        
        var act = () => SlangCompiler.Compile("float4 broken(", "broken.slang", "broken", ShaderStage.Fragment, _folder.Path);

        act.Should().Throw<InvalidOperationException>().WithMessage("*broken.slang*");
    }

    [NeedsSlangFact]
    public void The_Built_In_Shaders_Compile()
    {
        
        var shaders = Path.Combine(AppContext.BaseDirectory, "source", "shaders");
        foreach (var file in new[] { "imgui.slang", "immediate.slang", "model.slang" })
        {
            var program = new SlangLoader(_folder.Path, shaders).Compile(File.ReadAllText(Path.Combine(shaders, file)), file);
            program.Stages.Keys.Should().BeEquivalentTo([ShaderStage.Vertex, ShaderStage.Fragment], file);
        }
    }

    [NeedsSlangFact]
    public void The_Model_Pass_Sets_Are_Read_From_Its_Shader()
    {
        var shaders = Path.Combine(AppContext.BaseDirectory, "source", "shaders");
        var program = new SlangLoader(_folder.Path, shaders).Compile(File.ReadAllText(Path.Combine(shaders, "model.slang")), "model.slang");

        var lights = program.LayoutOf(1);
        lights.Select(b => b.Binding).Should().Equal(Enumerable.Range(0, 6 + LightingUboPacker.MaxProbes).Select(b => (uint)b),
            "the lighting buffer, the shadow maps, the environment and sky, the probes' cubes and the occlusion");
        lights[0].Type.Should().Be(DescriptorType.UniformBuffer);
        lights.Skip(1).Should().OnlyContain(b => b.Type == DescriptorType.CombinedImageSampler);
        lights.Should().OnlyContain(b => b.Stages.HasFlag(ShaderStageFlags.Fragment));
        program.LayoutOf(0).Select(b => b.Binding).Should().Equal([1u, 2u, 3u, 4u, 5u], "the material's five maps");

        // The same read back from the cache, as a program that ships without slangc does.
        var cached = new SlangLoader(_folder.Path, shaders).Compile(File.ReadAllText(Path.Combine(shaders, "model.slang")), "model.slang");
        cached.LayoutOf(1).Should().Equal(lights);
    }

    [Fact]
    public void Layouts_Merge_By_Binding_The_First_Giving_The_Kind_And_Each_Its_Stages()
    {
        var merged = ShaderProgram.Merge(
            [new DescriptorSetLayoutBinding(2, DescriptorType.StorageBuffer, ShaderStageFlags.Vertex)],
            [new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.Fragment),
             new DescriptorSetLayoutBinding(2, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment)]);

        merged.Should().Equal(
            new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.Fragment),
            new DescriptorSetLayoutBinding(2, DescriptorType.StorageBuffer, ShaderStageFlags.All));
    }

    [Fact]
    public void A_Cache_Key_Follows_Imports_With_Forward_Slashes_And_Counts_Missing_Ones()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_folder.Path, "imports")).FullName;
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
        var shaders = Directory.CreateDirectory(Path.Combine(_folder.Path, "shaders")).FullName;
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "source", "shaders"), "*.slang"))
            File.Copy(file, Path.Combine(shaders, Path.GetFileName(file)));
        var cache = Path.Combine(_folder.Path, "cache");
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

        var compiled = SlangCompiler.CompileStage(withUniforms, "tinted.slang", "fragmentMain", ShaderStage.Fragment, _folder.Path);
        var cached = SlangCompiler.CompileStage(withUniforms, "tinted.slang", "fragmentMain", ShaderStage.Fragment, _folder.Path, null, compiler: null);

        compiled.Uniforms.Should().Equal(new ShaderUniform("tint", 0, 16), new ShaderUniform("strength", 16, 4));
        cached.Uniforms.Should().Equal(compiled.Uniforms);
        new ShaderProgram("tinted.slang", new Dictionary<ShaderStage, byte[]> { [ShaderStage.Fragment] = compiled.Spirv }, compiled.Uniforms)
            .UniformSize.Should().Be(32);
    }

    [NeedsSlangFact]
    public void Each_Element_And_Field_Of_A_Uniform_Has_GLSLs_Name_As_Raylibs_Lights_Are_Found()
    {
        const string withSpots = """
            struct Spot { float2 pos; float inner; float radius; };
            uniform Spot spots[3];
            uniform int3 palette[2];

            [shader("fragment")]
            float4 fragmentMain() : SV_Target
            {
                return float4(spots[1].pos, spots[2].inner, palette[1].x);
            }
            """;

        var compiled = SlangCompiler.CompileStage(withSpots, "spots.slang", "fragmentMain", ShaderStage.Fragment, _folder.Path);
        var cached = SlangCompiler.CompileStage(withSpots, "spots.slang", "fragmentMain", ShaderStage.Fragment, _folder.Path, null, compiler: null);

        compiled.Uniforms.Should().Contain(new ShaderUniform("spots", 0, 48), "the whole array is set at once by SetShaderValueV");
        compiled.Uniforms.Should().Contain(new ShaderUniform("spots[1]", 16, 16));
        compiled.Uniforms.Should().Contain(new ShaderUniform("spots[1].pos", 16, 8));
        compiled.Uniforms.Should().Contain(new ShaderUniform("spots[2].inner", 40, 4));
        compiled.Uniforms.Should().Contain(new ShaderUniform("palette[1]", 64, 12), "each element sixteen bytes on, holding its twelve");
        cached.Uniforms.Should().Equal(compiled.Uniforms, "and the cache keeps every name");
    }

    [Fact]
    public void An_Include_Is_Found_Beside_The_File_That_Names_It_Before_The_Import_Folder()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_folder.Path, "nested")).FullName;
        Directory.CreateDirectory(Path.Combine(folder, "water"));
        File.WriteAllText(Path.Combine(folder, "water", "ocean.slang"), "#include \"waves.slang\"");
        File.WriteAllText(Path.Combine(folder, "water", "waves.slang"), "// beside ocean");
        File.WriteAllText(Path.Combine(folder, "waves.slang"), "// in the import folder");

        byte[]? Waves() => SlangCompiler.ImportedFiles("import water.ocean;", folder).Single(f => f.Path.EndsWith("waves.slang")).Bytes;

        SlangCompiler.ImportedFiles("import water.ocean;", folder).Select(f => f.Path)
            .Should().Equal("water/ocean.slang", "water/waves.slang");
        var before = Waves();
        File.WriteAllText(Path.Combine(folder, "water", "waves.slang"), "// changed beside ocean");
        Waves().Should().NotEqual(before, "an edit to the included file changes what is hashed");
    }

    [NeedsSlangFact]
    public void Uniform_Arrays_Are_Laid_Out_With_A_Stride_Of_Sixteen_Bytes()
    {
        const string arrays = """
            uniform float4 tint;
            uniform float weights[3];
            uniform float4 colors[2];

            [shader("fragment")]
            float4 fragmentMain(float4 position : SV_Position) : SV_Target
            {
                return tint * weights[0] + colors[1] * weights[2];
            }
            """;
        var stage = SlangCompiler.CompileStage(arrays, "arrays.slang", "fragmentMain", ShaderStage.Fragment, _folder.Path);
        var uniforms = stage.Uniforms.ToDictionary(u => u.Name);
        uniforms["weights"].Size.Should().Be(48, "a float in an array takes sixteen bytes, as std140 lays a uniform buffer out");
        uniforms["colors"].Size.Should().Be(32);
    }
}

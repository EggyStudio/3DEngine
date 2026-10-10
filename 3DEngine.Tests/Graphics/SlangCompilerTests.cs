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
    public void A_Stage_Says_Which_Input_Locations_It_Reads()
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
        ShaderProgram.InputLocationSet(program.Vertex).Should().BeEquivalentTo([0, 5], "the locations between are not read, so a pipeline feeds them nothing");
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
    public void No_Stage_Of_A_Built_In_Shader_Reads_More_Samplers_Than_Metal_Allows_A_Stage()
    {
        // Metal allows a stage sixteen samplers, which MoltenVK reports as
        // maxPerStageDescriptorSamplers, and a combined image sampler counts as one, so a pass of
        // more textures than that reads them through a few samplers bound apart, as the model
        // pass's lights' set does. Every other device allows more, so this is where it is held.
        var shaders = Path.Combine(AppContext.BaseDirectory, "source", "shaders");
        var loader = new SlangLoader(_folder.Path, shaders);
        var over = new List<string>();
        foreach (var file in Directory.GetFiles(shaders, "*.slang").Order(StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file);
            if (!SlangLoader.EntryPoints(source).Any(e => e.Stage == ShaderStage.Fragment)) continue;
            var program = loader.Compile(source, Path.GetFileName(file));
            var bindings = program.Bindings.Select(b => new DescriptorSetLayoutBinding((uint)b.Binding.Binding, b.Binding.Type, b.Stages)).ToArray();
            foreach (var stage in new[] { ShaderStageFlags.Vertex, ShaderStageFlags.Fragment })
                if (GraphicsDevice.OverLimits(stage.ToString(), bindings.Where(b => b.Stages.HasFlag(stage)), 16, 128, Path.GetFileName(file)) is { } refusal)
                    over.Add(refusal);
        }
        over.Should().BeEmpty();

        // And a stage past it is refused with the counts.
        var many = Enumerable.Range(0, 22).Select(i => new DescriptorSetLayoutBinding((uint)i, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment));
        GraphicsDevice.OverLimits("fragment", many, 16, 128, "a pipeline").Should()
            .StartWith("The fragment stage of a pipeline reads 22 samplers, and this GPU allows a stage 16.");
    }

    [NeedsSlangFact]
    public void The_Model_Pass_Sets_Are_Read_From_Its_Shader()
    {
        var shaders = Path.Combine(AppContext.BaseDirectory, "source", "shaders");
        var program = new SlangLoader(_folder.Path, shaders).Compile(File.ReadAllText(Path.Combine(shaders, "model.slang")), "model.slang");

        var lights = program.LayoutOf(1);
        lights.Select(b => b.Binding).Should().Equal([.. Enumerable.Range(0, 20 + 2 * LightingUboPacker.MaxProbes).Select(b => (uint)b), 31u, 32u, 33u],
            "the lighting buffer, the shadow maps, the environment and sky, the probes' cubes, the occlusion, the probes' and the environment's irradiance, "
            + "the light that bounced in the world's probes, the field they lie in, and the screen's probes and their surfaces, "
            + "what a reflection is traced through, the field, its colors and light, the lights, the window's depth, and its frame before and that frame's depth, "
            + "the two samplers the set's images are read through, and past the device's rays the probes' reach, their share of the sky "
            + "and which glow lights each screen probe sees");
        lights[0].Type.Should().Be(DescriptorType.UniformBuffer);
        // Images alone, read through the set's two samplers, since Metal allows a stage sixteen.
        lights.Skip(1).Take(5 + LightingUboPacker.MaxProbes).Should().OnlyContain(b => b.Type == DescriptorType.SampledImage);
        lights.Skip(6 + LightingUboPacker.MaxProbes).Take(1 + LightingUboPacker.MaxProbes).Should()
            .OnlyContain(b => b.Type == DescriptorType.StorageBuffer, "the GPU writes the irradiance");
        lights.Skip(7 + 2 * LightingUboPacker.MaxProbes).Select(b => b.Type).Should().Equal(DescriptorType.SampledImage,
            DescriptorType.UniformBuffer, DescriptorType.SampledImage, DescriptorType.SampledImage,
            DescriptorType.SampledImage, DescriptorType.SampledImage, DescriptorType.SampledImage,
            DescriptorType.UniformBuffer, DescriptorType.SampledImage, DescriptorType.SampledImage, DescriptorType.SampledImage,
            DescriptorType.Sampler, DescriptorType.Sampler, DescriptorType.SampledImage, DescriptorType.SampledImage, DescriptorType.StorageBuffer);
        lights.Should().OnlyContain(b => b.Stages.HasFlag(ShaderStageFlags.Fragment));
        program.LayoutOf(0).Select(b => b.Binding).Should().Equal([1u, 2u, 3u, 4u, 5u], "the material's five maps");

        // The same read back from the cache, as a program that ships without slangc does.
        var cached = new SlangLoader(_folder.Path, shaders).Compile(File.ReadAllText(Path.Combine(shaders, "model.slang")), "model.slang");
        cached.LayoutOf(1).Should().Equal(lights);
    }

    // Runs work on two threads at once each round, both let go by a barrier the moment both are
    // ready, and check between rounds while neither runs, giving every exception raised. A thread
    // whose round fails goes on to the next, and a barrier that waits a minute ends the race, so a
    // failure is reported where it would leave the other thread waiting.
    private static List<Exception> Race(int rounds, Action<int> work, Action check)
    {
        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        using var start = new Barrier(2, _ =>
        {
            try
            {
                check();
            }
            catch (Exception error)
            {
                failures.Enqueue(error);
            }
        });
        void Run(int thread)
        {
            for (int round = 0; round <= rounds; round++)
            {
                if (!start.SignalAndWait(TimeSpan.FromMinutes(1)))
                {
                    failures.Enqueue(new TimeoutException($"Thread {thread} waited a minute for the other at round {round}."));
                    return;
                }
                if (round == rounds) return;
                try
                {
                    work(thread);
                }
                catch (Exception error)
                {
                    failures.Enqueue(error);
                }
            }
        }
        Task.WaitAll(Task.Run(() => Run(0)), Task.Run(() => Run(1)));
        return [.. failures];
    }

    [Fact]
    public void Two_Writers_Of_One_Cache_Entry_At_Once_Both_Finish_And_Leave_It_Whole()
    {
        // Each round both write the entry at once, as two tests compiling one shader do, and the
        // entry is read and removed before the next.
        var path = _folder.File("shared.vertexMain.0123.spv");
        var bytes = Enumerable.Range(0, 64 * 1024).Select(i => (byte)(i * 7)).ToArray();
        var (rounds, whole) = (0, 0);
        var failures = Race(100, _ => SlangCompiler.WriteAtomically(path, bytes), () =>
        {
            if (!File.Exists(path)) return;
            rounds++;
            if (File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) whole++;
            File.Delete(path);
        });

        failures.Should().BeEmpty("a writer that finds the entry written by the other keeps it");
        (rounds, whole).Should().Be((100, 100), "every round leaves the entry whole");
        Directory.GetFiles(_folder.Path).Should().BeEmpty("each writer's own file is moved into place or removed");
    }

    [NeedsSlangFact]
    public void One_Shader_Compiled_From_Two_Threads_Against_One_Cache_Is_Cached_Whole()
    {
        // Both threads compile into an empty cache each round, a folder of the round's own, so both
        // write the entry, and a round's two results and the entry it left are the same SPIR-V.
        var (rounds, same) = (0, 0);
        string Cache() => Path.Combine(_folder.Path, $"cache-{rounds}");
        var results = new byte[2][];
        var failures = Race(10, thread => results[thread] = SlangCompiler.CompileStage(Source, "race.slang", "vertexMain", ShaderStage.Vertex, Cache()).Spirv, () =>
        {
            if (!Directory.Exists(Cache())) return;
            var entries = Directory.GetFiles(Cache(), "*.spv");
            if (entries.Length == 1 && results[0].AsSpan().SequenceEqual(results[1]) && File.ReadAllBytes(entries[0]).AsSpan().SequenceEqual(results[0])) same++;
            rounds++;
        });

        failures.Should().BeEmpty();
        (rounds, same).Should().Be((10, 10));
        results[0].Take(4).Should().Equal(SpirvMagic);
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
    public void A_Vertex_Stages_Inputs_Are_Reflected_By_Semantic_And_Kept_In_The_Cache()
    {
        // Declared out of the engine's order, a struct's fields each with a semantic of its own and
        // a system value that no stream feeds.
        const string shuffled = """
            struct Rows { float4 first : ROW_FIRST; float4 second : ROW_SECOND; };

            [shader("vertex")]
            float4 vertexMain(float2 uv : TEXCOORD0, Rows rows, float3 position : POSITION, float2 second : TEXCOORD1, uint id : SV_InstanceID) : SV_Position
            {
                return float4(position + rows.first.xyz + rows.second.xyz, uv.x + second.y + id);
            }
            """;

        var compiled = SlangCompiler.CompileStage(shuffled, "shuffled.slang", "vertexMain", ShaderStage.Vertex, _folder.Path);
        var cached = SlangCompiler.CompileStage(shuffled, "shuffled.slang", "vertexMain", ShaderStage.Vertex, _folder.Path, null, compiler: null);

        compiled.Inputs.Should().Equal(new ShaderInput("TEXCOORD0", 0), new ShaderInput("ROW_FIRST0", 1), new ShaderInput("ROW_SECOND0", 2),
            new ShaderInput("POSITION0", 3), new ShaderInput("TEXCOORD1", 4));
        cached.Inputs.Should().Equal(compiled.Inputs);
        cached.Uniforms.Should().BeEmpty("an input's line is not read as a uniform's");
    }

    [NeedsSlangFact]
    public void A_Texture_And_Its_Sampler_Declared_Apart_Are_Reflected_As_Such_And_Kept_In_The_Cache()
    {
        const string apart = """
            [[vk::binding(1, 0)]] Sampler2D combined;
            [[vk::binding(2, 0)]] Texture2D detail;
            [[vk::binding(3, 0)]] SamplerState detailSampler;

            [shader("fragment")]
            float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
            {
                return combined.Sample(uv) * detail.Sample(detailSampler, uv);
            }
            """;

        var compiled = SlangCompiler.CompileStage(apart, "apart.slang", "fragmentMain", ShaderStage.Fragment, _folder.Path);
        var cached = SlangCompiler.CompileStage(apart, "apart.slang", "fragmentMain", ShaderStage.Fragment, _folder.Path, null, compiler: null);

        compiled.Textures.Should().Equal(new ShaderTexture("combined", 1), new ShaderTexture("detail", 2, DescriptorType.SampledImage),
            new ShaderTexture("detailSampler", 3, DescriptorType.Sampler));
        cached.Textures.Should().Equal(compiled.Textures);
        compiled.Bindings!.Select(b => (b.Binding, b.Type)).Should().Equal(
            (1, DescriptorType.CombinedImageSampler), (2, DescriptorType.SampledImage), (3, DescriptorType.Sampler));
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

    [Fact]
    public void A_Module_Exported_Again_By_Its_Importer_Is_Part_Of_What_Is_Hashed()
    {
        // gi.slang exports glow.slang to the shaders that import it, and an edit to glow.slang
        // alone left them on their old SPIR-V, the line read as no import.
        var folder = Directory.CreateDirectory(Path.Combine(_folder.Path, "exported")).FullName;
        File.WriteAllText(Path.Combine(folder, "gi.slang"), "module gi;\n__exported import glow;\n");
        File.WriteAllText(Path.Combine(folder, "glow.slang"), "module glow;");

        byte[]? Glow() => SlangCompiler.ImportedFiles("import gi;", folder).Single(f => f.Path == "glow.slang").Bytes;

        SlangCompiler.ImportedFiles("import gi;", folder).Select(f => f.Path).Should().Equal("gi.slang", "glow.slang");
        var before = Glow();
        File.WriteAllText(Path.Combine(folder, "glow.slang"), "module glow; // changed");
        Glow().Should().NotEqual(before, "an edit to the module exported again changes what is hashed");
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

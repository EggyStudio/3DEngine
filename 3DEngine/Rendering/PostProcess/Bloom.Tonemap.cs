using System.Runtime.InteropServices;

namespace Engine;

internal sealed partial class BloomRenderer
{
    private static readonly ILogger TonemapLogger = Log.Category("Engine.PostProcess");

    // A program's shader drawn in the curve's place, with its fragment stage, the layout of its set
    // 0, a ring of those sets a frame slot, and its pipeline for each pass it draws into. Made the
    // first frame it is drawn, and again when a program is loaded anew under its id.
    private sealed class TonemapProgram(ShaderProgram program, IShader fragment, IDescriptorSetLayout layout) : IDisposable
    {
        public ShaderProgram Program { get; } = program;
        public IShader Fragment { get; } = fragment;
        public IDescriptorSetLayout Layout { get; } = layout;
        public List<IDescriptorSet>[] Rings { get; } = [.. Enumerable.Range(0, GpuTextures.RetireFrames).Select(_ => new List<IDescriptorSet>())];
        public Dictionary<IRenderPass, IPipeline> Pipelines { get; } = [];
        public long Frame = -1;
        public int Next;

        public void Dispose()
        {
            foreach (var pipeline in Pipelines.Values) pipeline.Dispose();
            foreach (var ring in Rings)
                foreach (var set in ring) set.Dispose();
            Fragment.Dispose();
            Layout.Dispose();
        }
    }

    private readonly Dictionary<int, TonemapProgram> _tonemapPrograms = [];
    // The table SetTonemapLookup loaded last, as uploaded.
    private (TonemapTables.Table Table, VolumeTexture Texture)? _lookup;
    // Set 0 of the engine's own composite, which binds nothing there, its inputs being in set 1.
    private IDescriptorSetLayout? _noOwnSet;
    // Bound in place of a storage buffer the shader was not given.
    private IBuffer? _noBuffer;
    private readonly HashSet<string> _ownVertexWarned = [];

    // The composite's pipeline, the engine's curve's or a program's shader's, over the fullscreen
    // triangle, with the program's own uniforms and textures in set 0, none for the engine's, and
    // the frame's inputs in set 1, which frame.slang declares for both.
    private IPipeline CompositePipeline(IGraphicsDevice gfx, IRenderPass renderPass, IShader fragment, IDescriptorSetLayout own) =>
        gfx.CreateGraphicsPipeline(new GraphicsPipelineDesc(
            renderPass, _compositeVertex!, fragment,
            BlendEnabled: false,
            Cull: CullMode.None,
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Fragment, 0, (uint)Marshal.SizeOf<CompositePush>())],
            DescriptorSetLayouts: [own, _twoTextures!],
            Blend: BlendMode.AddColors));

    // The pipeline and set 0 of the program's shader the frame's effects draw in the curve's place,
    // its uniforms and textures as the program last set them, or null for the curve, where none is
    // set or the one set has been unloaded.
    private (IPipeline Pipeline, IDescriptorSet Set)? TonemapShader(GraphicsDevice device, IRenderPass renderPass, RenderContext renderContext,
        RenderWorld renderWorld, FrameEffects effects)
    {
        var id = effects.TonemapShader;
        if (id == 0 || renderWorld.TryGet<ShaderStore>()?.Get(id) is not { } program
            || !program.Stages.TryGetValue(ShaderStage.Fragment, out var spirv) || renderContext.DynamicAllocator is not { } allocator)
            return null;
        if (!_tonemapPrograms.TryGetValue(id, out var made) || !ReferenceEquals(made.Program, program))
        {
            if (made is not null) _retired.Add((_frame, made));
            if (program.Stages.ContainsKey(ShaderStage.Vertex) && _ownVertexWarned.Add(program.Name))
                TonemapLogger.Warn($"'{program.Name}' is drawn in the curve's place over a triangle that covers the window, and its own vertex stage is not used.");
            // The shader's set 0 as it declares it, with its uniform buffer at 0 unless a texture or
            // buffer of its own took the binding.
            var bindings = ShaderProgram.Merge(program.LayoutOf(0), [new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.Fragment)]);
            _tonemapPrograms[id] = made = new TonemapProgram(program, device.CreateShader(new ShaderDesc(ShaderStage.Fragment, spirv)),
                device.CreateDescriptorSetLayout(bindings));
        }
        if (!made.Pipelines.TryGetValue(renderPass, out var pipeline))
            made.Pipelines[renderPass] = pipeline = CompositePipeline(device, renderPass, made.Fragment, made.Layout);

        // A set from the frame slot's ring, the composite being drawn once a frame, or twice where
        // FXAA reads it first.
        if (made.Frame != _frame) (made.Frame, made.Next) = (_frame, 0);
        var ring = made.Rings[(int)(_frame % made.Rings.Length)];
        if (made.Next == ring.Count) ring.Add(device.CreateDescriptorSet(made.Layout));
        var set = ring[made.Next++];

        var (uniforms, textures) = (effects.TonemapUniforms, effects.TonemapTextures);
        if (!ImmediateRenderer.ZeroTaken(program, program.Textures))
        {
            var size = (ulong)Math.Max(16, program.UniformSize);
            var allocation = allocator.Allocate(size, BufferUsage.Uniform);
            var bytes = allocator.Map(allocation);
            bytes.Clear();
            if (uniforms is { } given) bytes.CopyFrom(given.AsSpan(0, Math.Min(given.Length, bytes.Length)));
            allocator.Unmap(allocation);
            device.UpdateDescriptorSet(set, new UniformBufferBinding(allocation.Buffer, 0, allocation.Offset, size), null);
        }
        // Every texture of set 0 is the shader's own, the frame's being in set 1.
        if (renderWorld.TryGet<GpuTextures>() is { } gpuTextures)
            for (int i = 0; i < program.Textures.Count; i++)
            {
                var texture = program.Textures[i];
                var (view, sampler) = gpuTextures.ViewFor(device, textures is { } ids && i < ids.Length ? ids[i] : 0, cube: texture.Cube);
                device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(view, sampler, (uint)texture.Binding, texture.Type));
            }
        ImmediateRenderer.BindBuffers(device, renderWorld, set, program, textures, ref _noBuffer);
        return (pipeline, set);
    }

    // The table a curve looks light up in, read and uploaded the first time the curve is chosen,
    // the one SetTonemapLookup loaded, uploaded again when another is loaded, or a texel standing
    // in for a curve worked out without one.
    private VolumeTexture TableFor(GraphicsDevice device, Tonemap curve, FrameEffects effects)
    {
        if (curve == Tonemap.Lookup && effects.Lookup is { } loaded)
        {
            if (_lookup is { } made && ReferenceEquals(made.Table, loaded)) return made.Texture;
            if (_lookup is { } replaced) _retired.Add((_frame, replaced.Texture));
            _lookup = (loaded, device.CreateVolumeTexture(loaded.Size, loaded.Format, loaded.Texels));
            return _lookup.Value.Texture;
        }
        if (TonemapTables.FileOf(curve) is null)
            return _noTable ??= device.CreateVolumeTexture(1, Vortice.Vulkan.VkFormat.R16G16B16A16Sfloat, new byte[8]);
        if (_tables.TryGetValue(curve, out var table)) return table;
        var read = TonemapTables.Load(curve);
        return _tables[curve] = device.CreateVolumeTexture(read.Size, read.Format, read.Texels);
    }

    // The blue noise the dither draws its grain from (frame.slang's frameDither), the 64 by 64 table
    // build/blue-noise.py writes, made once, in each of a cube's 64 slices, so a table that changes
    // over the frames as well takes the same binding.
    private VolumeTexture BlueNoise(GraphicsDevice device)
    {
        if (_blueNoise is { } made) return made;
        var table = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "source", "shaders", "dither", "blue_noise.bin"));
        var cube = new byte[table.Length * 64];
        for (int slice = 0; slice < 64; slice++) table.CopyTo(cube, slice * table.Length);
        return _blueNoise = device.CreateVolumeTexture(64, Vortice.Vulkan.VkFormat.R8Unorm, cube);
    }

    private void DisposeTonemapPrograms()
    {
        foreach (var made in _tonemapPrograms.Values) made.Dispose();
        _tonemapPrograms.Clear();
        _noOwnSet?.Dispose();
        _noBuffer?.Dispose();
    }
}

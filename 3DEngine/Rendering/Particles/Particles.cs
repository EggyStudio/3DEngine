using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>The frame's emitters as the renderer sees them, with the window's camera and the seconds the frame covers.</summary>
internal sealed class RenderParticles
{
    /// <summary>Each emitter by its entity, with its settings, where it is, and the burst it gives off this frame.</summary>
    public List<(int Entity, ParticleEmitter Emitter, Vector3 Position, int Burst)> Emitters { get; } = [];

    /// <summary>The seconds since the frame before, which the particles are stepped by.</summary>
    public float Seconds { get; set; }

    /// <summary>The camera the window is drawn through, or null when there is none to face.</summary>
    public Matrix4x4? ViewProjection { get; set; }

    /// <summary>Where that camera is, which emitters laid over by alpha are drawn far to near from.</summary>
    public Vector3 Eye { get; set; }

    /// <summary>The camera each render target was drawn through in <c>BeginMode3D</c>, by its texture id.</summary>
    public Dictionary<int, (Matrix4x4 ViewProjection, Vector3 Eye)> Targets { get; } = [];
}

/// <summary>
/// Copies every <see cref="ParticleEmitter"/> into <see cref="RenderParticles"/>, placed by its
/// entity's world matrix, and clears the burst each one asked for, which is given off once.
/// </summary>
internal sealed class ParticleExtract : IExtractSystem
{
    /// <inheritdoc />
    public void Run(World world, RenderWorld renderWorld)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs) || ecs.Count<ParticleEmitter>() == 0)
        {
            renderWorld.Remove<RenderParticles>();
            return;
        }
        var particles = renderWorld.TryGet<RenderParticles>() ?? new RenderParticles();
        particles.Emitters.Clear();
        // A long frame, as the first or one after a stall, is stepped as a tenth of a second, so
        // the particles alive do not leap.
        particles.Seconds = world.TryGetResource<Time>(out var time) ? (float)Math.Min(time.DeltaSeconds, 0.1) : 1f / 60;
        var camera = MeshEntityDraws.WindowCamera(world, ecs);
        particles.ViewProjection = camera?.ViewProjection;
        particles.Eye = camera?.Eye ?? Vector3.Zero;
        particles.Targets.Clear();
        if (world.TryGetResource<Mode3DCamera>(out var mode3D))
            foreach (var (target, view) in mode3D.Targets) particles.Targets[target] = view;
        foreach (var (entity, emitter) in ecs.Query<ParticleEmitter>())
        {
            var position = TransformPropagation.WorldMatrix(ecs, entity).Translation;
            particles.Emitters.Add((entity, emitter, position, emitter.Burst));
            if (emitter.Burst != 0) ecs.GetRef<ParticleEmitter>(entity).Burst = 0;
        }
        renderWorld.Set(particles);
    }
}

/// <summary>
/// The GPU side of the particles: a buffer for each emitter, stepped each frame by a compute
/// dispatch recorded before the window's passes, and the pass that draws them after its meshes.
/// </summary>
/// <remarks>
/// An emitter's buffer is made the first frame it is seen and again when its
/// <see cref="ParticleEmitter.MaxParticles"/> changes, and one whose entity is gone, or replaced,
/// is destroyed <see cref="GpuTextures.RetireFrames"/> frames later, once no frame in flight reads
/// it. The births of a frame are its share of the rate, carried from frame to frame so a rate
/// below the frame rate still gives off its particles, and its burst, in a run of slots after the
/// last frame's, so the oldest particles are the ones replaced.
/// </remarks>
internal sealed class ParticleRenderer : IDisposable
{
    private sealed class State(GpuParticles gpu)
    {
        public GpuParticles Gpu { get; } = gpu;
        public IDescriptorSet? DrawSet { get; set; }
        public float Owed;
        public int Next;
        public long Seen;
        public ParticleEmitter Emitter;
        public Vector3 Position;
    }

    private readonly ReadOnlyMemory<byte> _vertexSpv, _fragmentSpv;
    private readonly DescriptorSetLayoutBinding[] _particleBindings;
    private IShader? _vertex, _fragment;
    private IDescriptorSetLayout? _particleLayout;
    private readonly Dictionary<(IRenderPass Pass, ParticleBlend Blend), IPipeline> _pipelines = [];
    private readonly Dictionary<int, State> _emitters = [];
    private readonly List<(long Frame, State State)> _retired = [];
    private readonly List<State> _drawn = [];
    private long _frame;

    /// <summary>Creates the renderer from <c>particles.slang</c>, compiled, whose third set holds the particles.</summary>
    public ParticleRenderer(ShaderProgram particles)
    {
        (_vertexSpv, _fragmentSpv) = (particles.Vertex, particles.Fragment);
        _particleBindings = particles.LayoutOf(2);
    }

    /// <summary>Records the dispatch that steps each emitter's particles this frame, outside any render pass.</summary>
    public void Step(RenderContext renderContext, RenderWorld renderWorld)
    {
        _frame++;
        RetireOld();
        _drawn.Clear();
        if (renderContext.Device is not GraphicsDevice { CanStepParticles: true } device) return;
        var frame = renderWorld.TryGet<RenderParticles>();
        foreach (var (entity, emitter, position, burst) in frame?.Emitters ?? [])
        {
            var capacity = Math.Clamp(emitter.MaxParticles, 1, 1 << 20);
            if (!_emitters.TryGetValue(entity, out var state) || state.Gpu.Capacity != capacity)
            {
                if (state is not null) _retired.Add((_frame, state));
                _emitters[entity] = state = new State(device.CreateParticles(capacity));
                device.Name(state.Gpu.Buffer, "Particles");
            }
            state.Seen = _frame;
            state.Emitter = emitter;
            state.Position = position;

            // This frame's share of the rate, the fraction left owed to the next.
            var seconds = frame!.Seconds;
            state.Owed += emitter.Emitting ? Math.Max(0, emitter.Rate) * seconds : 0;
            var born = (int)state.Owed;
            state.Owed -= born;
            born = Math.Min(capacity, born + Math.Max(0, burst));

            var step = new ParticleStep
            {
                OriginAndSeconds = new Vector4(position, seconds),
                VelocityAndSpread = new Vector4(emitter.Velocity, float.DegreesToRadians(Math.Clamp(emitter.Spread, 0, 180))),
                GravityAndLife = new Vector4(emitter.Gravity, Math.Max(0.01f, emitter.Life)),
                Variation = new Vector4(Math.Clamp(emitter.LifeVariation, 0, 1), Math.Clamp(emitter.SpeedVariation, 0, 1),
                    Seed(entity), Math.Max(0, emitter.Radius)),
                StartColor = Linear(emitter.StartColor),
                EndColor = Linear(emitter.EndColor),
                Look = new Vector4(Math.Max(0, emitter.StartSize), Math.Max(0, emitter.EndSize), Math.Max(0, emitter.Intensity), Flags(emitter)),
                First = (uint)state.Next,
                Count = (uint)born,
                Capacity = (uint)capacity,
                Drag = Math.Max(0, emitter.Drag),
            };
            state.Next = (state.Next + born) % capacity;
            device.RecordParticles(renderContext.CommandBuffer, state.Gpu, in step);
            // Laid over by alpha, they are sorted far to near from the window's camera, which the
            // draw then reads them in, render textures' cameras included.
            if (emitter.Blend == ParticleBlend.Alpha) device.RecordParticleSort(renderContext.CommandBuffer, state.Gpu, frame.Eye);
            _drawn.Add(state);
        }

        // Emitters gone this frame.
        foreach (var gone in _emitters.Where(e => e.Value.Seen != _frame).ToArray())
        {
            _retired.Add((_frame, gone.Value));
            _emitters.Remove(gone.Key);
        }
    }

    // A seed for this frame and emitter, as the bits of a float the shader reads back as a uint,
    // kept to the bits of a number between 1 and 2 so no seed is a NaN a driver might change.
    private float Seed(int entity)
    {
        var mixed = (uint)(_frame * 0x9E3779B1L) ^ (uint)entity * 0x85EBCA77u;
        return BitConverter.Int32BitsToSingle((int)((mixed & 0x007FFFFFu) | 0x3F800000u));
    }

    // Whether it is lit, whether it is textured, and the sheet's columns and rows less one, packed
    // into one float as an integer below 2^18, which a float holds exactly, since the push block
    // has no room left. particles.slang unpacks it.
    private static float Flags(in ParticleEmitter emitter)
    {
        var columns = Math.Clamp(emitter.TextureColumns, 1, 256) - 1;
        var rows = Math.Clamp(emitter.TextureRows, 1, 256) - 1;
        var textured = emitter.Texture.IsValid;
        return (emitter.Lit ? 1 : 0) | (textured ? 2 : 0) | (textured ? columns << 2 | rows << 10 : 0);
    }

    private static Vector4 Linear(Color color) =>
        new(BloomRenderer.SrgbToLinear(color.R / 255f), BloomRenderer.SrgbToLinear(color.G / 255f), BloomRenderer.SrgbToLinear(color.B / 255f), color.A / 255f);

    /// <summary>
    /// Draws the particles stepped this frame into <paramref name="pass"/>, the window's through its
    /// camera after its meshes, or a render target's through its own, lit by that view's lights
    /// where they ask to be.
    /// </summary>
    /// <remarks>
    /// Emitters that add their light come first, in any order, and those laid over by alpha after
    /// them from the farthest from the camera to the nearest, so where two overlap the nearer is in
    /// front. The particles within one were sorted far to near from the window's camera after their
    /// step, which a render target's camera draws them in too. A target is drawn through the camera
    /// of its first <c>BeginMode3D</c>, or the one its meshes were drawn through, and one drawn only
    /// in 2D has none.
    /// </remarks>
    public void Draw(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld, int target = 0)
    {
        if (_drawn.Count == 0 || renderWorld.TryGet<RenderParticles>() is not { } frame) return;
        if (renderWorld.TryGet<ModelRenderer>() is not { } models || renderWorld.TryGet<GpuTextures>() is not { } textures) return;
        // A target's camera is the one BeginMode3D drew into it through, or for a camera entity's
        // texture the one its meshes were drawn through.
        var (camera, eye) = target == 0 ? (frame.ViewProjection, frame.Eye)
            : frame.Targets.TryGetValue(target, out var flat) ? (flat.ViewProjection, flat.Eye)
            : renderWorld.TryGet<ModelDrawList>()?.ViewProjectionOf(target) is { } own ? (own, EyeOf(own)) : ((Matrix4x4?)null, Vector3.Zero);
        if (camera is not { } viewProjection) return;
        var gfx = renderContext.Device;
        _vertex ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragment ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));
        _particleLayout ??= gfx.CreateDescriptorSetLayout(_particleBindings);
        var lights = models.LightsFor(gfx, renderWorld, textures, target);

        _drawn.Sort((a, b) => a.Emitter.Blend != b.Emitter.Blend
            ? a.Emitter.Blend == ParticleBlend.Additive ? -1 : 1
            : a.Emitter.Blend == ParticleBlend.Alpha
                ? Vector3.DistanceSquared(b.Position, eye).CompareTo(Vector3.DistanceSquared(a.Position, eye))
                : 0);
        foreach (var state in _drawn)
        {
            var key = (renderPass, state.Emitter.Blend);
            if (!_pipelines.TryGetValue(key, out var pipeline))
                _pipelines[key] = pipeline = gfx.CreateGraphicsPipeline(new GraphicsPipelineDesc(
                    renderPass, _vertex, _fragment,
                    BlendEnabled: true,
                    Cull: CullMode.None,
                    PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Vertex, 0, 64)],
                    DescriptorSetLayouts: [models.MaterialSetLayout(gfx), models.LightsSetLayout(gfx), _particleLayout],
                    DepthTestEnabled: true,
                    DepthWriteEnabled: false,
                    DepthCompareOp: CompareOp.LessOrEqual,
                    Blend: state.Emitter.Blend == ParticleBlend.Additive ? BlendMode.Additive : BlendMode.Alpha));
            if (state.DrawSet is null)
            {
                state.DrawSet = gfx.CreateDescriptorSet(_particleLayout);
                gfx.UpdateDescriptorSet(state.DrawSet, new StorageBufferBinding(state.Gpu.Buffer, 0));
            }
            pass.SetPipeline(pipeline);
            // The emitter's texture as the material's base color, which the shader samples in place of the dot.
            pass.SetBindGroup(pipeline, models.TexturedMaterial(gfx, textures, state.Emitter.Texture.Id), 0);
            pass.SetBindGroup(pipeline, lights, 1);
            pass.SetBindGroup(pipeline, state.DrawSet, 2);
            pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in viewProjection)));
            pass.Draw(6, (uint)state.Gpu.Capacity);
        }
    }

    // Where a camera is, near enough, the middle of its near plane, which emitters are ordered from.
    private static Vector3 EyeOf(Matrix4x4 viewProjection)
    {
        if (!Matrix4x4.Invert(viewProjection, out var inverse)) return Vector3.Zero;
        var near = Vector4.Transform(new Vector4(0, 0, 0, 1), inverse);
        return near.W == 0 ? Vector3.Zero : new Vector3(near.X, near.Y, near.Z) / near.W;
    }

    // Destroys what was let go once the frames in flight that might read it have finished.
    private void RetireOld()
    {
        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            Release(_retired[i].State);
            _retired.RemoveAt(i);
        }
    }

    private static void Release(State state)
    {
        state.DrawSet?.Dispose();
        state.Gpu.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, state) in _retired) Release(state);
        foreach (var state in _emitters.Values) Release(state);
        _retired.Clear();
        _emitters.Clear();
        foreach (var pipeline in _pipelines.Values) pipeline.Dispose();
        _pipelines.Clear();
        _particleLayout?.Dispose();
        _vertex?.Dispose();
        _fragment?.Dispose();
    }
}

/// <summary>Render graph node that steps the frame's particles on the GPU, before anything draws.</summary>
internal sealed class ParticleNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<ParticleRenderer>()?.Step(renderContext, renderWorld);
}

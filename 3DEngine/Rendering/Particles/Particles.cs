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
        // The eye an emitter laid over by alpha was last sorted from, whose order its buffer holds.
        public Vector3? SortedFrom;
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

    // The depth colliding particles meet, the window's meshes that cast shadows at half its size,
    // made the first frame an emitter collides and again when the window's size changes, and let
    // go, once no frame in flight reads it, the first frame none does.
    private ShadowMap? _depth;
    private readonly List<(long Frame, ShadowMap Depth)> _retiredDepths = [];

    /// <summary>Creates the renderer from <c>particles.slang</c>, compiled, whose third set holds the particles.</summary>
    public ParticleRenderer(ShaderProgram particles)
    {
        (_vertexSpv, _fragmentSpv) = (particles.Vertex, particles.Fragment);
        _particleBindings = particles.LayoutOf(2);
    }

    /// <summary>Whether particles are drawn into the window this frame, stepped and seen through the window's camera.</summary>
    internal bool DrawsInWindow(RenderWorld renderWorld) => _drawn.Count > 0 && renderWorld.TryGet<RenderParticles>()?.ViewProjection is not null;

    /// <summary>
    /// Sorts the particles of each emitter laid over by alpha far to near from the eye of
    /// <paramref name="target"/>'s camera, the window's for 0, where they were last sorted from
    /// another, outside any render pass and before the view's, so each view lays its clouds over in
    /// its own order.
    /// </summary>
    internal void SortFor(RenderContext renderContext, RenderWorld renderWorld, int target)
    {
        if (_drawn.Count == 0 || renderContext.Device is not GraphicsDevice device || renderWorld.TryGet<RenderParticles>() is not { } frame) return;
        Vector3? eye = target == 0 ? frame.ViewProjection is null ? null : frame.Eye
            : frame.Targets.TryGetValue(target, out var flat) ? flat.Eye
            : renderWorld.TryGet<ModelDrawList>()?.ViewProjectionOf(target) is { } own ? EyeOf(own) : null;
        if (eye is not { } from) return;
        foreach (var state in _drawn)
            if (state.Emitter.Blend == ParticleBlend.Alpha && state.SortedFrom != from)
            {
                device.RecordParticleSort(renderContext.CommandBuffer, state.Gpu, from);
                state.SortedFrom = from;
            }
    }

    /// <summary>Records the dispatch that steps each emitter's particles this frame, outside any render pass.</summary>
    public void Step(RenderContext renderContext, RenderWorld renderWorld)
    {
        _frame++;
        RetireOld();
        _drawn.Clear();
        if (renderContext.Device is not GraphicsDevice { CanStepParticles: true } device || renderWorld.TryGet<GpuTextures>() is not { } textures) return;
        var frame = renderWorld.TryGet<RenderParticles>();
        SetView(renderContext, renderWorld, device, textures, frame?.Emitters.Any(e => e.Emitter.Collision != ParticleCollision.None) == true);
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
                Capacity = (uint)capacity | (uint)emitter.Collision << 21 | (uint)(Math.Clamp(emitter.Bounce, 0, 1) * 255 + 0.5f) << 23,
                Drag = Math.Max(0, emitter.Drag),
            };
            state.Next = (state.Next + born) % capacity;
            device.RecordParticles(renderContext.CommandBuffer, state.Gpu, in step);
            // Laid over by alpha, they are sorted far to near from the window's camera, which the
            // draw then reads them in, and again from a render texture's own before it is drawn
            // (SortFor).
            if (emitter.Blend == ParticleBlend.Alpha) device.RecordParticleSort(renderContext.CommandBuffer, state.Gpu, frame.Eye);
            state.SortedFrom = emitter.Blend == ParticleBlend.Alpha ? frame.Eye : null;
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

    // Whether it is lit, whether it is textured, the sheet's columns and rows less one, and whether
    // its frames blend, packed into one float as an integer below 2^19, which a float holds exactly,
    // since the push block has no room left. particles.slang unpacks it.
    private static float Flags(in ParticleEmitter emitter)
    {
        var columns = Math.Clamp(emitter.TextureColumns, 1, 256) - 1;
        var rows = Math.Clamp(emitter.TextureRows, 1, 256) - 1;
        var textured = emitter.Texture.IsValid;
        return (emitter.Lit ? 1 : 0) | (textured ? 2 : 0) | (textured ? columns << 2 | rows << 10 : 0)
            | (textured && emitter.BlendFrames ? 1 << 18 : 0);
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
    public void Draw(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld, int target = 0) =>
        Draw(pass, renderPass, renderContext, renderWorld, target, null, Vector3.Zero);

    /// <summary>
    /// Draws the frame's particles through <paramref name="through"/> from <paramref name="eye"/>,
    /// lit by <paramref name="target"/>'s lights, as a reflection probe's face draws them, or as
    /// <paramref name="target"/>'s camera sees them where <paramref name="through"/> is <c>null</c>.
    /// </summary>
    internal void Draw(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld, int target,
        Matrix4x4? through, Vector3 eye)
    {
        if (_drawn.Count == 0 || renderWorld.TryGet<RenderParticles>() is not { } frame) return;
        if (renderWorld.TryGet<ModelRenderer>() is not { } models || renderWorld.TryGet<GpuTextures>() is not { } textures) return;
        // A target's camera is the one BeginMode3D drew into it through, or for a camera entity's
        // texture the one its meshes were drawn through.
        var (camera, from) = through is not null ? (through, eye)
            : target == 0 ? (frame.ViewProjection, frame.Eye)
            : frame.Targets.TryGetValue(target, out var flat) ? (flat.ViewProjection, flat.Eye)
            : renderWorld.TryGet<ModelDrawList>()?.ViewProjectionOf(target) is { } own ? (own, EyeOf(own)) : ((Matrix4x4?)null, Vector3.Zero);
        eye = from;
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
        for (int i = _retiredDepths.Count - 1; i >= 0; i--)
        {
            if (_frame - _retiredDepths[i].Frame < GpuTextures.RetireFrames) continue;
            _retiredDepths[i].Depth.Dispose();
            _retiredDepths.RemoveAt(i);
        }
    }

    // Draws the depth colliding particles meet and hands the step the view it was drawn through,
    // or, with none colliding or no window view, the white texture and collision off, so the step's
    // set always holds an image it can sample.
    private void SetView(RenderContext renderContext, RenderWorld renderWorld, GraphicsDevice device, GpuTextures textures, bool colliding)
    {
        if (colliding && renderWorld.TryGet<WindowView>() is { } view && renderWorld.TryGet<SwapchainTarget>() is { } swapchain
            && renderWorld.TryGet<ModelRenderer>() is { } models && Matrix4x4.Invert(view.ViewProjection, out var inverse))
        {
            var extent = new Extent2D(Math.Max(1, swapchain.Extent.Width / 2), Math.Max(1, swapchain.Extent.Height / 2));
            if (_depth is null || _depth.Extent != extent)
            {
                if (_depth is not null) _retiredDepths.Add((_frame, _depth));
                _depth = device.CreateDepthTarget(extent.Width, extent.Height);
                device.Name(_depth.DepthView.Image, "Particle collision depth");
            }
            models.DrawDepth(renderContext, renderWorld, _depth);
            device.SetParticleView(_depth.DepthView, _depth.Sampler,
                new ParticleView { ViewProjection = view.ViewProjection, InverseViewProjection = inverse, Depth = Vector4.UnitX },
                renderWorld.TryGet<SceneFieldBinding>()?.Field);
            return;
        }

        if (_depth is not null)
        {
            _retiredDepths.Add((_frame, _depth));
            _depth = null;
        }
        var (white, sampler) = textures.ViewFor(device, 0);
        device.SetParticleView(white, sampler, default, renderWorld.TryGet<SceneFieldBinding>()?.Field);
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
        foreach (var (_, depth) in _retiredDepths) depth.Dispose();
        _retiredDepths.Clear();
        _depth?.Dispose();
        _depth = null;
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

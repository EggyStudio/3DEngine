using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>The frame's emitters as the renderer sees them, with the window's camera and the seconds the frame covers.</summary>
internal sealed class RenderParticles
{
    /// <summary>Each emitter by its entity, with its settings, its entity's world matrix, and the burst it emits this frame.</summary>
    public List<(int Entity, ParticleEmitter Emitter, Matrix4x4 World, int Burst)> Emitters { get; } = [];

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
/// entity's world matrix, and clears the burst each one asked for, which is emitted once.
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
            particles.Emitters.Add((entity, emitter, TransformPropagation.WorldMatrix(ecs, entity), emitter.Burst));
            if (emitter.Burst != 0) ecs.GetRef<ParticleEmitter>(entity).Burst = 0;
        }
        renderWorld.Set(particles);
    }
}

/// <summary>
/// The GPU side of the particles, a buffer for each emitter stepped each frame by a compute
/// dispatch recorded before the window's passes and the pass that draws them after its meshes.
/// </summary>
/// <remarks>
/// An emitter's buffer is made the first frame it is seen and again when its
/// <see cref="ParticleEmitter.MaxParticles"/> changes, and one whose entity is gone, or replaced,
/// is destroyed <see cref="GpuTextures.RetireFrames"/> frames later, once no frame in flight reads
/// it. The births of a frame are its share of the rate, carried from frame to frame so a rate
/// below the frame rate still emits its particles, and its burst, in a run of slots after the
/// last frame's, so the oldest particles are the ones replaced. The particles the program placed
/// at its <see cref="ParticleEmitter.Points"/> are a buffer of their own, which no dispatch steps,
/// made again when the points change.
/// <para>
/// The step, the sort and the draw take only the slots that may hold a live particle, counted back
/// from the newest, which the births of the frames before and the longest life one of them can draw
/// bound, so an emitter whose particles have died costs nothing until it emits again, and one with
/// a few alive costs its few, where twenty empty emitters of a voxel game read 3.5 ms of the CPU and
/// 2.1 of the GPU stepping, sorting and drawing every slot.
/// </para>
/// </remarks>
internal sealed class ParticleRenderer : IDisposable
{
    // A buffer of an emitter's particles, the set the draw reads it through, and the eye an emitter
    // laid over by alpha was last sorted from, in the space the particles are kept in, whose order
    // the buffer holds.
    private sealed class ParticleBuffer(GpuParticles gpu)
    {
        public GpuParticles Gpu { get; } = gpu;
        public IDescriptorSet? DrawSet { get; set; }
        public Vector3? SortedFrom;
        // The newest particle's slot and how many slots back from it may hold one alive, which the
        // sort and the draw take, every slot for the particles a program placed.
        public int Newest;
        public int Span = gpu.Capacity;

        public void Release()
        {
            DrawSet?.Dispose();
            Gpu.Dispose();
        }
    }

    private sealed class State(ParticleBuffer born)
    {
        // The particles born at the rate and in bursts, which the step moves.
        public ParticleBuffer Born = born;
        // The particles the program placed, and a copy of the points they were placed at.
        public ParticleBuffer? Placed;
        public Vector3[]? Points;
        public float Owed;
        public int Next;
        public long Seen;
        // The seconds the particles have been stepped by, and each run of births with the time by
        // which the longest life a particle of it can draw is over, oldest first.
        public double Clock;
        public readonly Queue<(double Until, int Count)> Runs = new();
        public ParticleEmitter Emitter;
        public Matrix4x4 World;
        public Vector3 Position => World.Translation;
        // What the born particles are placed by where they are drawn, the entity's world matrix for
        // those in its space and the identity for those in the world's.
        public Matrix4x4 BornBy => Emitter.Local ? World : Matrix4x4.Identity;
    }

    private readonly ReadOnlyMemory<byte> _vertexSpv, _fragmentSpv;
    private readonly DescriptorSetLayoutBinding[] _particleBindings;
    private IShader? _vertex, _fragment;
    private IDescriptorSetLayout? _particleLayout;
    private readonly Dictionary<(IRenderPass Pass, ParticleBlend Blend), IPipeline> _pipelines = [];
    private readonly Dictionary<int, State> _emitters = [];
    private readonly List<(long Frame, ParticleBuffer Buffer)> _retired = [];
    private readonly List<State> _drawn = [];
    private long _frame;

    /// <summary>The particle slots the last frame's dispatches stepped, every emitter's, for a test to read.</summary>
    internal int Stepped { get; private set; }

    /// <summary>The particles the last frame's draws drew, through every view, for a test to read.</summary>
    internal int Drawn { get; private set; }

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
        {
            if (state.Emitter.Blend != ParticleBlend.Alpha) continue;
            if (state.Born.Span > 0 && Within(state.BornBy, from) is var born && state.Born.SortedFrom != born) Sort(renderContext, device, state.Born, born);
            if (state.Placed is { } placed && Within(state.World, from) is var placedFrom && placed.SortedFrom != placedFrom) Sort(renderContext, device, placed, placedFrom);
        }
    }

    /// <summary>Records the dispatch that steps each emitter's particles this frame, outside any render pass.</summary>
    public void Step(RenderContext renderContext, RenderWorld renderWorld)
    {
        _frame++;
        RetireOld();
        _drawn.Clear();
        (Stepped, Drawn) = (0, 0);
        if (renderContext.Device is not GraphicsDevice { CanStepParticles: true } device || renderWorld.TryGet<GpuTextures>() is not { } textures) return;
        var frame = renderWorld.TryGet<RenderParticles>();
        SetView(renderContext, renderWorld, device, textures,
            frame?.Emitters.Any(e => e.Emitter.Collision != ParticleCollision.None && !e.Emitter.Local) == true);
        foreach (var (entity, emitter, world, burst) in frame?.Emitters ?? [])
        {
            var capacity = Math.Clamp(emitter.MaxParticles, 1, 1 << 20);
            if (!_emitters.TryGetValue(entity, out var state))
                _emitters[entity] = state = new State(Made(device, capacity));
            else if (state.Born.Gpu.Capacity != capacity)
            {
                _retired.Add((_frame, state.Born));
                (state.Born, state.Owed, state.Next) = (Made(device, capacity), 0, 0);
                state.Runs.Clear();
            }
            state.Seen = _frame;
            state.Emitter = emitter;
            state.World = world;

            // This frame's share of the rate, the fraction left owed to the next.
            var seconds = frame!.Seconds;
            state.Owed += emitter.Emitting ? Math.Max(0, emitter.Rate) * seconds : 0;
            var born = (int)state.Owed;
            state.Owed -= born;
            born = Math.Min(capacity, born + Math.Max(0, burst));

            // A run whose longest life was over by the last step died in it, since every particle
            // alive is stepped each frame, and the rest are the newest slots, the run of this
            // frame's births last.
            var before = state.Clock;
            state.Clock += seconds;
            while (state.Runs.Count > 0 && state.Runs.Peek().Until <= before) state.Runs.Dequeue();
            if (born > 0) state.Runs.Enqueue((state.Clock + LongestLife(emitter), born));
            var span = 0;
            foreach (var (_, count) in state.Runs) span = Math.Min(capacity, span + count);

            // Particles in the entity's space start about its middle there, and do not meet the
            // scene, whose depth and field are the world's.
            var step = new ParticleStep
            {
                OriginAndSeconds = new Vector4(emitter.Local ? Vector3.Zero : world.Translation, seconds),
                VelocityAndSpread = new Vector4(emitter.Velocity, float.DegreesToRadians(Math.Clamp(emitter.Spread, 0, 180))),
                GravityAndLife = new Vector4(emitter.Gravity, Math.Max(0.01f, emitter.Life)),
                Variation = new Vector4(Math.Clamp(emitter.LifeVariation, 0, 1), Math.Clamp(emitter.SpeedVariation, 0, 1),
                    Seed(entity), Math.Max(0, emitter.Radius)),
                StartColor = Linear(emitter.StartColor),
                EndColor = Linear(emitter.EndColor),
                Look = Look(emitter),
                First = (uint)state.Next,
                Count = (uint)born,
                Capacity = (uint)capacity | (uint)(emitter.Local ? ParticleCollision.None : emitter.Collision) << 21
                    | (uint)(Math.Clamp(emitter.Bounce, 0, 1) * 255 + 0.5f) << 23,
                Drag = Math.Max(0, emitter.Drag),
            };
            state.Next = (state.Next + born) % capacity;
            (state.Born.Newest, state.Born.Span) = ((state.Next + capacity - 1) % capacity, span);
            state.Born.SortedFrom = null;
            if (span > 0)
            {
                device.RecordParticles(renderContext.CommandBuffer, state.Born.Gpu, in step, state.BornBy, span);
                Stepped += span;
                // Laid over by alpha, they are sorted far to near from the window's camera, which the
                // draw then reads them in, and again from a render texture's own before it is drawn
                // (SortFor), each from the eye in the space the particles are kept in.
                if (emitter.Blend == ParticleBlend.Alpha) Sort(renderContext, device, state.Born, Within(state.BornBy, frame.Eye));
            }
            Place(renderContext, device, state, frame.Eye);
            if (span > 0 || state.Placed is not null) _drawn.Add(state);
        }

        // Emitters gone this frame.
        foreach (var gone in _emitters.Where(e => e.Value.Seen != _frame).ToArray())
        {
            _retired.Add((_frame, gone.Value.Born));
            if (gone.Value.Placed is { } placed) _retired.Add((_frame, placed));
            _emitters.Remove(gone.Key);
        }
    }

    private static ParticleBuffer Made(GraphicsDevice device, int capacity)
    {
        var gpu = device.CreateParticles(capacity);
        device.Name(gpu.Buffer, "Particles");
        return new ParticleBuffer(gpu);
    }

    // The particles the program placed at the emitter's points, made again where the points
    // changed, their header written for this frame, and sorted where they are laid over by alpha.
    private void Place(RenderContext renderContext, GraphicsDevice device, State state, Vector3 eye)
    {
        var emitter = state.Emitter;
        if (emitter.Points is not { Length: > 0 } points)
        {
            if (state.Placed is not null) _retired.Add((_frame, state.Placed));
            (state.Placed, state.Points) = (null, null);
            return;
        }
        if (state.Placed is null || state.Points is null || !points.AsSpan().SequenceEqual(state.Points))
        {
            if (state.Placed is not null) _retired.Add((_frame, state.Placed));
            state.Placed = new ParticleBuffer(device.CreatePlacedParticles(points));
            device.Name(state.Placed.Gpu.Buffer, "Placed particles");
            state.Points = [.. points];
        }
        // Every point is drawn, counted back from the last.
        var count = state.Placed.Gpu.Capacity;
        ReadOnlySpan<Vector4> header = [Linear(emitter.StartColor), Linear(emitter.EndColor), Look(emitter), new Vector4(count, 0, count - 1, 0)];
        (state.Placed.Newest, state.Placed.Span) = (count - 1, count);
        device.RecordParticleHeader(renderContext.CommandBuffer, state.Placed.Gpu, header, state.World);
        if (emitter.Blend == ParticleBlend.Alpha) Sort(renderContext, device, state.Placed, Within(state.World, eye));
        else state.Placed.SortedFrom = null;
    }

    private static void Sort(RenderContext renderContext, GraphicsDevice device, ParticleBuffer buffer, Vector3 eye)
    {
        device.RecordParticleSort(renderContext.CommandBuffer, buffer.Gpu, eye, buffer.Newest, buffer.Span);
        buffer.SortedFrom = eye;
    }

    // The longest a particle of the emitter can live, its life lengthened by the whole of its
    // variation as the step may draw it, with a hundredth more for the step's ages summed in floats.
    private static double LongestLife(in ParticleEmitter emitter) =>
        Math.Max(0.01, emitter.Life) * (1 + Math.Clamp(emitter.LifeVariation, 0, 1)) * 1.01 + 1e-3;

    // The eye in the space particles placed by placedBy are kept in, which they are sorted from.
    private static Vector3 Within(Matrix4x4 placedBy, Vector3 eye) =>
        placedBy.IsIdentity || !Matrix4x4.Invert(placedBy, out var inverse) ? eye : Vector3.Transform(eye, inverse);

    // The sizes, the brightness and how an emitter's particles are drawn, as the header holds them.
    private static Vector4 Look(in ParticleEmitter emitter) =>
        new(Math.Max(0, emitter.StartSize), Math.Max(0, emitter.EndSize), Math.Max(0, emitter.Intensity), Flags(emitter));

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
            pass.SetPipeline(pipeline);
            // The emitter's texture as the material's base color, which the shader samples in place of the dot.
            pass.SetBindGroup(pipeline, models.TexturedMaterial(renderContext, textures, state.Emitter.Texture.Id), 0);
            pass.SetBindGroup(pipeline, lights, 1);
            pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in viewProjection)));
            if (state.Born.Span > 0) DrawBuffer(pass, pipeline, gfx, state.Born);
            if (state.Placed is { } placed) DrawBuffer(pass, pipeline, gfx, placed);
        }
    }

    private void DrawBuffer(TrackedRenderPass pass, IPipeline pipeline, IGraphicsDevice gfx, ParticleBuffer buffer)
    {
        if (buffer.DrawSet is null)
        {
            buffer.DrawSet = gfx.CreateDescriptorSet(_particleLayout!);
            gfx.UpdateDescriptorSet(buffer.DrawSet, new StorageBufferBinding(buffer.Gpu.Buffer, 0));
        }
        pass.SetBindGroup(pipeline, buffer.DrawSet, 2);
        pass.Draw(6, (uint)buffer.Span);
        Drawn += buffer.Span;
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
            _retired[i].Buffer.Release();
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
        state.Born.Release();
        state.Placed?.Release();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, buffer) in _retired) buffer.Release();
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

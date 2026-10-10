using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Engine;

internal sealed partial class ModelRenderer
{
    // The lights of this frame as one view sees them, the window's at 0, as a descriptor set: one
    // of the sets of this frame's slot of a ring, a slot per frame in flight so a set the GPU may
    // still read is never written, or a set over an empty buffer when there are no lights and no
    // environment, which the shader reads as "draw unlit". Binding 0 holds the view's
    // lighting buffer, with its own cascades, binding 1 the shadow map when the view has a shadow,
    // and the white texture otherwise, binding 2 the environment map and binding 3 its sky, or a
    // black cube for each, and binding 4 the point lights' faces, or a stand-in, so all are always
    // valid.
    private IDescriptorSet LightsSet(IGraphicsDevice gfx, RenderWorld renderWorld, GpuTextures textures, int target)
    {
        var (white, whiteSampler) = textures.ViewFor(gfx, 0);
        if (renderWorld.TryGet<FrameLightingBinding>() is not { } frame
            || (frame.LightCount == 0 && !frame.HasEnvironment && target != 0 && target != ProbeCaptureLights
                && renderWorld.TryGet<BoundProbes>() is not { Slots.Count: > 0 } && GlobalIlluminationRenderer.CascadesIn(renderWorld) == 0))
        {
            if (_noLights is null)
            {
                _noLightsBuffer = gfx.CreateBuffer(new BufferDesc((ulong)LightingUboPacker.SizeBytes, BufferUsage.Uniform, CpuAccessMode.Write));
                var span = gfx.Map(_noLightsBuffer);
                span.Clear();
                gfx.Unmap(_noLightsBuffer);
                _noLights = gfx.CreateDescriptorSet(LightsLayout(gfx));
                gfx.UpdateDescriptorSet(_noLights, new UniformBufferBinding(_noLightsBuffer, 0, 0, (ulong)LightingUboPacker.SizeBytes),
                    Lit(white, whiteSampler, 1));
                gfx.UpdateDescriptorSet(_noLights, null, Lit(white, whiteSampler, AmbientOcclusionBinding));
                if (BlackCube(gfx) is { } black)
                    for (uint b = 2; b < 5 + LightingUboPacker.MaxProbes; b++)
                        if (b != 4) gfx.UpdateDescriptorSet(_noLights, null, Lit(black.View, black.Sampler, b));
                for (uint s = 0; s < LightingUboPacker.MaxProbes; s++)
                    gfx.UpdateDescriptorSet(_noLights, new StorageBufferBinding(NoIrradiance(gfx), ProbeIrradianceBinding + s));
                gfx.UpdateDescriptorSet(_noLights, new StorageBufferBinding(NoIrradiance(gfx), EnvironmentIrradianceBinding));
                if (gfx is GraphicsDevice stub)
                {
                    var none = NoPointShadowMap(stub);
                    gfx.UpdateDescriptorSet(_noLights, null, Lit(none.DepthView, none.Sampler, 4));
                    BindBounced(stub, _noLights, null, null);
                }
                BindSamplers(gfx, _noLights, white);
            }
            return _noLights;
        }

        // Once a frame, however many views draw models in it, the next slot's sets are handed out again.
        if (!ReferenceEquals(frame, _lastFrame))
        {
            _lastFrame = frame;
            _lightSlot = (_lightSlot + 1) % Math.Max(1, gfx.FramesInFlight);
            while (_lightSets.Count <= _lightSlot) _lightSets.Add([]);
            _lightSetOf.Clear();
        }
        if (_lightSetOf.TryGetValue(target, out var made)) return made;
        var slot = _lightSets[_lightSlot];
        if (slot.Count <= _lightSetOf.Count) slot.Add(gfx.CreateDescriptorSet(LightsLayout(gfx)));
        var set = _lightSetOf[target] = slot[_lightSetOf.Count];

        // A render target with a camera of its own has a buffer and a shadow of its own, and a
        // probe's faces the window's at the exposure they are captured at.
        var (binding, shadow) = target == ProbeCaptureLights && renderWorld.TryGet<BoundProbes>()?.CaptureBinding is { } capture
            ? (capture, renderWorld.TryGet<FrameShadow>())
            : target != 0 && renderWorld.TryGet<TargetShadows>() is { } targets && targets.ByTarget.TryGetValue(target, out var own)
            ? (own.Binding, own.Shadow)
            : (frame.Binding, renderWorld.TryGet<FrameShadow>());
        gfx.UpdateDescriptorSet(set, binding, shadow is not null && _shadowMap is { } map
            ? Lit(map.DepthView, map.Sampler, 1)
            : Lit(white, whiteSampler, 1));
        // The view's occlusion, the window's or a render target's own where it draws meshes through
        // a camera, which its buffer says to read, and white for a probe's faces.
        gfx.UpdateDescriptorSet(set, null, (target == 0 ? renderWorld.TryGet<AmbientOcclusionImage>()
                : renderWorld.TryGet<TargetOcclusion>()?.Images.GetValueOrDefault(target)) is { } occlusion
            ? Lit(occlusion.View, occlusion.Sampler, AmbientOcclusionBinding)
            : Lit(white, whiteSampler, AmbientOcclusionBinding));
        // And the light that bounced: the window's with its screen's probes and what its glossy
        // surfaces trace through, a render target's with screen probes of its own where it draws
        // meshes through a camera, and a probe capture's from the world's probes alone, which no
        // camera places, all of this frame's.
        if (gfx is GraphicsDevice bouncing)
            BindBounced(bouncing, set, renderWorld.TryGet<IlluminationBinding>() is { } bounced
                ? target == 0 ? bounced
                : renderWorld.TryGet<TargetIllumination>()?.ByTarget.GetValueOrDefault(target)
                  ?? bounced with { Screen = null, Depth = null, History = null, Rays = null }
                : null, renderWorld.TryGet<SceneFieldBinding>() is { On: true } built ? built.Field : null);
        BindSamplers(gfx, set, white);
        // The environment as the frame's filter left it, or black and no light where it has none.
        var environment = frame.HasEnvironment && _environmentSource is not null
            && ReferenceEquals(renderWorld.TryGet<EnvironmentMap>(), _environmentSource) ? _environment : null;
        if (environment is not null && _sky is not null)
        {
            gfx.UpdateDescriptorSet(set, null, Lit(environment.View, environment.Sampler, 2));
            gfx.UpdateDescriptorSet(set, null, Lit(_sky.View, _sky.Sampler, 3));
        }
        else if (BlackCube(gfx) is { } none)
        {
            gfx.UpdateDescriptorSet(set, null, Lit(none.View, none.Sampler, 2));
            gfx.UpdateDescriptorSet(set, null, Lit(none.View, none.Sampler, 3));
        }
        gfx.UpdateDescriptorSet(set, new StorageBufferBinding(environment?.Irradiance ?? NoIrradiance(gfx), EnvironmentIrradianceBinding));
        if (gfx is GraphicsDevice device)
        {
            var points = shadow is { PointLights.Count: > 0 } ? PointShadowMap(device, shadow.PointFaceSize) : NoPointShadowMap(device);
            gfx.UpdateDescriptorSet(set, null, Lit(points.DepthView, points.Sampler, 4));

            // Each bound probe's cube and irradiance at its slot, and the black cube and zeros past them.
            var slots = renderWorld.TryGet<BoundProbes>()?.Slots ?? [];
            var black = BlackCube(gfx)!;
            for (int s = 0; s < LightingUboPacker.MaxProbes; s++)
            {
                var probeMap = s < slots.Count ? slots[s].Map : null;
                gfx.UpdateDescriptorSet(set, null, probeMap is not null
                    ? Lit(probeMap.View, probeMap.Sampler, (uint)(5 + s))
                    : Lit(black.View, black.Sampler, (uint)(5 + s)));
                gfx.UpdateDescriptorSet(set, new StorageBufferBinding(probeMap?.Irradiance ?? NoIrradiance(gfx), ProbeIrradianceBinding + (uint)s));
            }
            ForgetProbeMaps(renderWorld);
        }
        else
            for (uint s = 0; s < LightingUboPacker.MaxProbes; s++)
                gfx.UpdateDescriptorSet(set, new StorageBufferBinding(NoIrradiance(gfx), ProbeIrradianceBinding + s));
        return set;
    }

    // Where lightset.slang binds the two samplers every image of the set is read through.
    private const uint LinearSamplerBinding = 26, NearestSamplerBinding = 27;

    // The two samplers, one blending between texels and levels and one reading the nearest
    // texel, made the first time a set binds them.
    private ISampler? _lightLinear, _lightNearest;

    private void BindSamplers(IGraphicsDevice gfx, IDescriptorSet set, IImageView any)
    {
        _lightLinear ??= gfx.CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, MipFilter: SamplerFilter.Linear));
        _lightNearest ??= gfx.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        gfx.UpdateDescriptorSet(set, null, Lit(any, _lightLinear, LinearSamplerBinding));
        gfx.UpdateDescriptorSet(set, null, Lit(any, _lightNearest, NearestSamplerBinding));
    }

    // The bindings of the lights' set by number, as the model pass declares each.
    private Dictionary<uint, DescriptorType>? _lightsTypes;

    // A texture or sampler of the lights' set written as the model pass declares its binding: the
    // image alone where the image is read through the set's shared samplers, and the sampler alone
    // for one of those, since a stage may have no more than sixteen samplers on Metal.
    private CombinedImageSamplerBinding Lit(IImageView view, ISampler sampler, uint binding)
    {
        _lightsTypes ??= _lightsBindings.ToDictionary(b => b.Binding, b => b.Type);
        return new CombinedImageSamplerBinding(view, sampler, binding,
            _lightsTypes.TryGetValue(binding, out var type) ? type : DescriptorType.CombinedImageSampler);
    }

    // Where modelpass.slang binds ambientOcclusionMap in the lights' set.
    private const uint AmbientOcclusionBinding = 9;

    // Where modelpass.slang binds the probes' bounced light and the field they lie in, the
    // screen's probes' light and surfaces, and the probes' reach.
    private const uint BouncedLightBinding = 15, BouncedFieldBinding = 16, ScreenLightBinding = 17, ScreenSurfacesBinding = 18,
        BouncedReachBinding = 31;

    // Where it binds what a glossy surface's reflection is traced through and shaded from: the
    // field, its colors and light, the lights, the window's depth, and its scene and depth the
    // frame before.
    private const uint ReflectFieldBinding = 19, ReflectAlbedoBinding = 20, ReflectGlowBinding = 21, ReflectLightsBinding = 22,
        ReflectDepthBinding = 23, ReflectHistoryBinding = 24, ReflectHistoryDepthBinding = 25;

    // A field of one cell and no cascade, screen probes of one probe holding nothing and lights of
    // none, bound where no light bounces, since the set must hold them.
    private GpuSceneField? _noBounce;
    private GpuScreenProbes? _noScreen;
    private IBuffer? _noGiLights;

    // Binds the probes' faces and their field, and what a reflection is traced through, or where
    // light does not bounce the scene's field where it is built, which subsurface.slang measures
    // the light that comes through a mesh by, or else the field of nothing.
    private void BindBounced(GraphicsDevice device, IDescriptorSet set, IlluminationBinding? bounced, GpuSceneField? built)
    {
        var screen = bounced?.Screen ?? (_noScreen ??= device.CreateScreenProbes(1, 1, 1));
        device.UpdateDescriptorSet(set, null, Lit(screen.BlendedView, screen.Sampler, ScreenLightBinding));
        device.UpdateDescriptorSet(set, null, Lit(screen.GeometryView, screen.Sampler, ScreenSurfacesBinding));
        _noScreen ??= device.CreateScreenProbes(1, 1, 1);
        var (depthView, depthSampler) = bounced?.Depth is { } depth ? (depth.View, depth.Sampler) : (_noScreen.BlendedView, _noScreen.Sampler);
        var (historyView, historySampler) = bounced?.History is { } history ? (history.View, history.Sampler) : (_noScreen.BlendedView, _noScreen.Sampler);
        device.UpdateDescriptorSet(set, null, Lit(depthView, depthSampler, ReflectDepthBinding));
        device.UpdateDescriptorSet(set, null, Lit(historyView, historySampler, ReflectHistoryBinding));
        var (historyDepthView, historyDepthSampler) = bounced?.History is { } held ? (held.DepthView, held.DepthSampler) : (_noScreen.BlendedView, _noScreen.Sampler);
        device.UpdateDescriptorSet(set, null, Lit(historyDepthView, historyDepthSampler, ReflectHistoryDepthBinding));
        GpuSceneField field;
        if (bounced is not null)
        {
            field = bounced.Field;
            device.UpdateDescriptorSet(set, null, Lit(bounced.Probes.CubesView, bounced.Probes.Sampler, BouncedLightBinding));
            device.UpdateDescriptorSet(set, null, Lit(bounced.Probes.ReachView, bounced.Probes.Sampler, BouncedReachBinding));
            device.UpdateDescriptorSet(set, new UniformBufferBinding(bounced.Probes.Lights, ReflectLightsBinding, 0, GpuIllumination.LightsBytes), null);
        }
        else
        {
            var none = _noBounce ??= device.CreateSceneField(1, 1);
            field = built ?? none;
            _noGiLights ??= device.CreateBuffer(new BufferDesc(GpuIllumination.LightsBytes, BufferUsage.Uniform, CpuAccessMode.Write));
            device.UpdateDescriptorSet(set, null, Lit(none.View, none.Sampler, BouncedLightBinding));
            device.UpdateDescriptorSet(set, null, Lit(none.View, none.Sampler, BouncedReachBinding));
            device.UpdateDescriptorSet(set, new UniformBufferBinding(_noGiLights, ReflectLightsBinding, 0, GpuIllumination.LightsBytes), null);
        }
        device.UpdateDescriptorSet(set, new UniformBufferBinding(field.Info, BouncedFieldBinding, 0, GpuSceneField.InfoBytes), null);
        device.UpdateDescriptorSet(set, null, Lit(field.View, field.Sampler, ReflectFieldBinding));
        device.UpdateDescriptorSet(set, null, Lit(field.AlbedoView, field.Sampler, ReflectAlbedoBinding));
        device.UpdateDescriptorSet(set, null, Lit(field.GlowView, field.Sampler, ReflectGlowBinding));
        // The window's meshes for the device's ray tracing, or a scene of none, where the model
        // pass was built to trace rays.
        if (TracesRays) device.BindRayScene(set, RaySceneBinding, bounced?.Rays ?? (_noRays ??= device.CreateRayScene()));
    }

    // Where lightset.slang binds the window's meshes for the device's ray tracing, the copies'
    // colors and the meshes' corners, in the model pass built to trace rays.
    private const uint RaySceneBinding = 28;

    // A scene of no meshes, bound where the model pass traces rays and none are built.
    private GpuRayScene? _noRays;

    /// <summary>Whether the model pass was built to trace rays, its lights' set holding the window's meshes for the device's ray tracing.</summary>
    internal bool TracesRays => _lightsBindings.Any(b => b.Type == DescriptorType.AccelerationStructure);

    // Where modelpass.slang binds the first probe's irradiance in the lights' set, the others after
    // it, and the environment's.
    private const uint ProbeIrradianceBinding = 10;
    private const uint EnvironmentIrradianceBinding = 14;

    // Nine zero coefficients, the irradiance of a slot no probe is bound at.
    private IBuffer? _noIrradiance;

    private IBuffer NoIrradiance(IGraphicsDevice gfx)
    {
        if (_noIrradiance is not null) return _noIrradiance;
        _noIrradiance = gfx.CreateBuffer(new BufferDesc(9 * 16, BufferUsage.Storage, CpuAccessMode.Write));
        gfx.Map(_noIrradiance).Clear();
        gfx.Unmap(_noIrradiance);
        return _noIrradiance;
    }

    private IDescriptorSetLayout MaterialLayout(IGraphicsDevice gfx) => _materialLayout ??= gfx.CreateDescriptorSetLayout(_materialBindings);

    // What the particle pass borrows of this one, whose lighting it shares: the two layouts, a
    // material whose base color is a texture, white for none, and the lights of the window or of a
    // render target. A material is found by its texture's id within a frame, so the frame's sets
    // are begun here as the model pass begins them, since in a frame that draws particles and no
    // model a texture unloaded under a living emitter was still bound through the set naming its
    // view after the view was destroyed.
    internal IDescriptorSetLayout MaterialSetLayout(IGraphicsDevice gfx) => MaterialLayout(gfx);
    internal IDescriptorSetLayout LightsSetLayout(IGraphicsDevice gfx) => LightsLayout(gfx);
    internal IDescriptorSet TexturedMaterial(RenderContext renderContext, GpuTextures textures, int texture)
    {
        BeginFrameOfSets(renderContext);
        return MaterialSet(renderContext.Device, textures, new ModelDraw(0, Matrix4x4.Identity, Matrix4x4.Identity, Color.White, texture));
    }
    internal IDescriptorSet LightsFor(IGraphicsDevice gfx, RenderWorld renderWorld, GpuTextures textures, int target) => LightsSet(gfx, renderWorld, textures, target);

    private IDescriptorSetLayout LightsLayout(IGraphicsDevice gfx) => _defaultLayout ??= gfx.CreateDescriptorSetLayout(_lightsBindings);

    // A black cube of one texel, bound where there is no environment or probe.
    private CubeMap? BlackCube(IGraphicsDevice gfx) =>
        gfx is GraphicsDevice device ? _noEnvironment ??= device.CreateCubeMap(1, 1, new Half[6 * 4]) : null;

    /// <summary>The environment map's filtered cube, once a frame has filtered it, for a test to read back.</summary>
    internal FilteredCube? Environment => _environment;

    /// <summary>
    /// Filters the environment map on the GPU in the frame that first sees it, before any pass
    /// samples it: its image uploaded with its mips, resampled into a cube prefiltered by
    /// roughness with its irradiance, and into the sky's cube
    /// (<see cref="GraphicsDevice.RecordEnvironmentFilter"/>). The cubes it replaces are kept until
    /// no frame in flight reads them.
    /// </summary>
    internal void FilterEnvironment(RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderContext.Device is not GraphicsDevice { CanFilterProbes: true } device) return;
        if (renderWorld.TryGet<EnvironmentMap>() is not { } environment || ReferenceEquals(environment, _environmentSource)) return;

        if (_environment is not null) _retiredCubes.Add((_frames, _environment));
        if (_sky is not null) _retiredCubes.Add((_frames, _sky));
        var (width, height) = ((uint)environment.Width, (uint)environment.Height);
        var image = device.CreateImage(new ImageDesc(new Extent2D(width, height), ImageFormat.R16G16B16A16_Float,
            ImageUsage.Sampled | ImageUsage.TransferDst | ImageUsage.TransferSrc, (uint)Math.Log2(Math.Max(width, height)) + 1));
        device.UploadTexture2D(image, System.Runtime.InteropServices.MemoryMarshal.AsBytes(environment.Pixels.AsSpan()), width, height, 8);
        var view = device.CreateImageView(image);
        _environment = device.CreateFilteredCube((uint)environment.Size, (uint)environment.MipLevels);
        _sky = device.CreateFilteredCube((uint)environment.SkySize, 1, irradiance: false);
        device.Name(_environment.Image, "Environment map");
        device.Name(_sky.Image, "Sky");
        var filter = device.RecordEnvironmentFilter(renderContext.CommandBuffer, view, height, _environment, _sky);
        // The image is read by this frame's filter alone.
        _retiredCubes.Add((_frames, filter));
        _retiredCubes.Add((_frames, view));
        _retiredCubes.Add((_frames, image));
        _environmentSource = environment;
    }

    // The six faces a probe is drawn into, made for the first capture and kept for the next.
    private RenderTarget[]? _probeFaces;

    // The key a probe's faces take their lights by, apart from every render target's id.
    private const int ProbeCaptureLights = int.MinValue;

    /// <summary>The width in texels of each face a probe is captured into.</summary>
    internal const int ProbeFaceSize = 64;

    // Each face's way and up, any orientation serving, since the filter gathers each direction
    // through its face's own view-projection.
    private static readonly (Vector3 Forward, Vector3 Up)[] ProbeFaceAxes =
    [
        (Vector3.UnitX, Vector3.UnitY), (-Vector3.UnitX, Vector3.UnitY), (Vector3.UnitY, -Vector3.UnitZ),
        (-Vector3.UnitY, Vector3.UnitZ), (Vector3.UnitZ, Vector3.UnitY), (-Vector3.UnitZ, Vector3.UnitY),
    ];

    /// <summary>
    /// Captures the first probe whose capture is out of date, drawing the window's meshes from its
    /// position into six faces, one face a frame, or the first render target's when the window draws
    /// none, and filters them on the GPU in the frame that draws the last of them. One probe at a
    /// time, and none while no meshes are drawn. A probe is captured twice, the second time with the
    /// first bound, so the metal in its room reflects the room in the capture rather than the sky.
    /// </summary>
    /// <remarks>
    /// All six faces in one frame cost it 3 to 5 ms while a level streamed rooms in, each bringing
    /// a probe to capture twice, and a face a frame costs a sixth of that. The filter
    /// (<see cref="GraphicsDevice.RecordProbeFilter"/>) is recorded after the last face, so its cube
    /// and irradiance are on the GPU for the frames after, with nothing read back.
    /// </remarks>
    public void CaptureProbes(RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<ReflectionProbes>() is not { } probes || renderContext.Device is not GraphicsDevice { CanFilterProbes: true } device) return;
        if (renderWorld.TryGet<ModelDrawList>() is not { IsEmpty: false } draws) return;
        var source = draws.WindowViewProjection is not null ? 0 : draws.Targets() is [var first, ..] ? first : (int?)null;
        if (source is null) return;
        if (_capture is null)
        {
            // A capture a placement or a change of lights needs comes before a refresh, and the
            // refresh of the probe captured longest ago before the others, so a probe refreshed
            // every frame keeps none of the rest waiting.
            var next = probes.ByEntity.Values.FirstOrDefault(p => !p.Capturing && (p.Captured != p.Wanted || p.Passes < ReflectionProbes.Passes))
                ?? probes.ByEntity.Values.Where(p => !p.Capturing && p.Stale).MinBy(p => p.CapturedAt);
            if (next is null) return;
            next.Capturing = true;
            var wanted = next.Wanted;
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2, 1, 0.05f, 1000);
            projection.M22 = -projection.M22;
            var viewProjections = new Matrix4x4[6];
            for (int f = 0; f < 6; f++)
                viewProjections[f] = Matrix4x4.CreateLookAt(wanted.Position, wanted.Position + ProbeFaceAxes[f].Forward, ProbeFaceAxes[f].Up) * projection;
            _capture = new ProbeCapture(next, wanted, viewProjections);
        }

        if (_probeFaces is null)
        {
            // Half floats, so a lamp or a sunlit wall comes back as bright as it was drawn.
            _probeFaces = [.. Enumerable.Range(0, 6).Select(_ => device.CreateRenderTarget(ProbeFaceSize, ProbeFaceSize, ImageFormat.R16G16B16A16_Float))];
            for (int f = 0; f < 6; f++) device.Name(_probeFaces[f].ColorView.Image, $"Reflection probe face {f}");
        }
        // Cleared as the window is, in linear light, so an opening shows what the window shows past the room.
        var clear = renderWorld.TryGet<ClearColor>() is { } windowClear
            ? new ClearColor(BloomRenderer.SrgbToLinear(windowClear.R), BloomRenderer.SrgbToLinear(windowClear.G), BloomRenderer.SrgbToLinear(windowClear.B), 1)
            : ClearColor.Black;

        var capture = _capture;
        var face = capture.Next++;
        var target = _probeFaces[face];
        var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, LoadOp.Clear, StoreOp.Store, clear));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        Draw(pass, target.RenderPass, renderContext, renderWorld, source.Value, capture.ViewProjections[face], ProbeCaptureLights);
        // The particles over the meshes, through the face from the probe's middle, lit as the meshes are.
        renderWorld.TryGet<ParticleRenderer>()?.Draw(pass, target.RenderPass, renderContext, renderWorld, ProbeCaptureLights,
            capture.ViewProjections[face], capture.Wanted.Position);
        pass.EndRenderPass();
        if (capture.Next < 6) return;

        // The last face drawn, the probe's map is filtered from all six, and the next probe waits
        // for the next frame.
        var map = device.CreateFilteredCube(ProbeMapSize);
        device.Name(map.Image, "Reflection probe");
        var filter = device.RecordProbeFilter(renderContext.CommandBuffer, [.. _probeFaces.Select(f => f.ColorView)], capture.ViewProjections,
            capture.Wanted.Position, map);
        _retiredCubes.Add((_frames, filter));
        _probeMaps.Add(map);
        capture.Probe.Done = new ReflectionProbes.Capture(map, capture.Wanted);
        _capture = null;
    }

    /// <summary>The width in texels of each face of a probe's prefiltered cube.</summary>
    internal const int ProbeMapSize = 32;

    // A probe's capture under way: its faces drawn so far, a face a frame.
    private sealed class ProbeCapture(ReflectionProbes.Probe probe, (Vector3 Position, Vector3 Size, int Capture) wanted, Matrix4x4[] viewProjections)
    {
        public ReflectionProbes.Probe Probe { get; } = probe;
        public (Vector3 Position, Vector3 Size, int Capture) Wanted { get; } = wanted;
        public Matrix4x4[] ViewProjections { get; } = viewProjections;
        public int Next;
    }

    private ProbeCapture? _capture;

    // Every probe's map the filter has made and not let go, which a probe or a capture it has not
    // taken yet holds.
    private readonly HashSet<FilteredCube> _probeMaps = [];

    // Lets the maps no probe holds go, a capture taken over by the one after it and those of probes
    // gone, once no frame in flight reads them.
    private void ForgetProbeMaps(RenderWorld renderWorld)
    {
        if (_probeMaps.Count == 0) return;
        var probes = renderWorld.TryGet<ReflectionProbes>();
        var held = probes?.ByEntity.Values.SelectMany(p => new[] { p.Map, p.Done?.Map }).OfType<FilteredCube>().ToHashSet() ?? [];
        foreach (var gone in _probeMaps.Where(m => !held.Contains(m)).ToArray())
        {
            _retiredCubes.Add((_frames, gone));
            _probeMaps.Remove(gone);
        }
    }
}

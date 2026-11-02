using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>The fog <c>SetVolumetricFog</c> sets, a world resource the renderer reads each frame.</summary>
internal sealed class VolumetricFogSettings
{
    /// <summary>The share of light the fog takes out per unit of distance at and under <see cref="Height"/>, 0 for none.</summary>
    public float Density { get; set; }

    /// <summary>The height the fog thins above.</summary>
    public float Height { get; set; }

    /// <summary>How fast it thins above <see cref="Height"/>, by e every one over this many units, 0 for as thick at any height.</summary>
    public float Falloff { get; set; }

    /// <summary>How much of the sun's light the fog scatters forward, from -1, all back toward the sun, to 1, all on along its way.</summary>
    public float Anisotropy { get; set; } = 0.5f;

    /// <summary>Whether there is fog to draw.</summary>
    public bool On => Density > 0;
}

/// <summary>
/// The fog in front of the window's camera, a volume of cells lit each frame and summed from the
/// eye (<c>fog_scatter.slang</c>, <c>fog_integrate.slang</c>), which the decode pass lays over the
/// scene's light by each pixel's depth (<c>decode.slang</c>), so bloom, the exposure, temporal
/// antialiasing and the reflections all read the scene in fog.
/// </summary>
/// <remarks>
/// <para>
/// A cell covers a tile of <see cref="Tile"/> pixels of the window and one of <see cref="Slices"/>
/// slices of the distance from the eye to the camera's far plane, or <see cref="Reach"/> units
/// where that is nearer, each slice a share of the way that grows with its distance, from a
/// thousandth of the way, so a slice is about as deep as a tile is wide at any distance. Its middle
/// is lit by the sun through the shadow map the window's shadow pass drew this frame and by the
/// world's probes where light bounces, or the ambient lights where it does not.
/// </para>
/// <para>
/// The volume is made the first frame fog is on and again when the window's size changes, and
/// the volumes it replaces are destroyed <see cref="GpuTextures.RetireFrames"/> frames later. A
/// volume of one cell, with no fog in it, stands in for the decode pass where the fog is off.
/// </para>
/// </remarks>
internal sealed class VolumetricFogRenderer : IDisposable
{
    /// <summary>The slices of the distance from the eye, <c>FogSlices</c> in <c>fog.slang</c>.</summary>
    public const uint Slices = 64;

    /// <summary>The pixels along each side of a tile a cell covers.</summary>
    public const int Tile = 16;

    /// <summary>The furthest the volume reaches, in world units, where the camera's far plane is further.</summary>
    public const float Reach = 128;

    // What decode.slang is pushed.
    [StructLayout(LayoutKind.Sequential)]
    private struct DecodePush
    {
        public Matrix4x4 InverseViewProjection;
        public Vector4 Eye, Slices;
    }

    // FogUniforms in fog.slang.
    [StructLayout(LayoutKind.Sequential)]
    private struct Uniforms
    {
        public Matrix4x4 InverseViewProjection, Cascade0, Cascade1, Cascade2, Cascade3;
        public Vector4 SunTexels, SunMap, Eye, ToSun, SunColor, Ambient, Density, Grid, Probes;
    }

    private readonly DescriptorSetLayoutBinding[] _decodeBindings;
    private IDescriptorSetLayout? _decodeLayout;
    private (GpuFog Fog, IDescriptorSet Set)? _sized, _standIn;
    private GpuSceneField? _noField;
    private readonly List<(long Frame, IDisposable Disposable)> _retired = [];
    private long _frame;
    private DecodePush _push;

    /// <summary>Creates the renderer from <c>decode.slang</c>, compiled, whose second set reads the fog.</summary>
    public VolumetricFogRenderer(ShaderProgram decode) => _decodeBindings = decode.LayoutOf(1);

    /// <summary>The layout of the set the decode pass reads the fog through, made on first use.</summary>
    public IDescriptorSetLayout DecodeLayout(GraphicsDevice device) => _decodeLayout ??= device.CreateDescriptorSetLayout(_decodeBindings);

    /// <summary>The set the decode pass reads the fog through this frame, the volume's or the stand-in's.</summary>
    public IDescriptorSet DecodeSet(GraphicsDevice device) => (_push.Eye.W > 0 && _sized is { } sized ? sized : _standIn ??= Made(device, 1, 1, 1)).Set;

    /// <summary>What the decode pass is pushed this frame, where the fog lies and whether there is any.</summary>
    public ReadOnlySpan<byte> DecodeBytes => MemoryMarshal.AsBytes(new ReadOnlySpan<DecodePush>(in _push));

    /// <summary>The size of <see cref="DecodeBytes"/>, a matrix and two vectors.</summary>
    public static uint DecodeSize => (uint)Marshal.SizeOf<DecodePush>();

    /// <summary>
    /// Lights and sums the fog's volume for the window's camera this frame where fog is on and the
    /// window shows a scene, or lets the volume go where it is off.
    /// </summary>
    public void Draw(RenderContext renderContext, RenderWorld renderWorld)
    {
        _frame++;
        Retire();
        _push = default;
        if (renderWorld.TryGet<VolumetricFogSettings>() is not { On: true } settings || renderWorld.TryGet<WindowView>() is not { } view
            || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain || renderContext.Device is not GraphicsDevice { CanDrawFog: true } device
            || !Matrix4x4.Invert(view.Unjittered, out var inverse) || !Matrix4x4.Invert(view.ViewProjection, out var shown))
        {
            if (_sized is { } made) _retired.Add((_frame, Disposing(made)));
            _sized = null;
            return;
        }

        var (width, height) = ((int)swapchain.Extent.Width, (int)swapchain.Extent.Height);
        var (across, down) = ((uint)((width + Tile - 1) / Tile), (uint)((height + Tile - 1) / Tile));
        if (_sized is not { } sized || sized.Fog.Across != across || sized.Fog.Down != down)
        {
            if (_sized is { } old) _retired.Add((_frame, Disposing(old)));
            _sized = sized = Made(device, across, down, Slices);
        }

        var far = MathF.Min(FarPlane(inverse, view.Eye), Reach);
        var start = far / 1024;
        var scale = new Vector2((float)width / (across * Tile), (float)height / (down * Tile));
        var uniforms = new Uniforms
        {
            InverseViewProjection = inverse,
            Eye = new Vector4(view.Eye, start),
            Density = new Vector4(settings.Density, settings.Height, MathF.Max(0, settings.Falloff), far),
            Grid = new Vector4(across, down, _frame % 4096, 0),
            Probes = new Vector4(0, 0, scale.X, scale.Y),
        };
        var (shadowView, shadowSampler) = Sun(renderWorld, device, settings, ref uniforms);
        var bounced = renderWorld.TryGet<IlluminationBinding>();
        if (bounced is not null)
        {
            uniforms.Ambient.W = 1;
            uniforms.Grid.W = bounced.Probes.Probes;
            (uniforms.Probes.X, uniforms.Probes.Y) = (GlobalIlluminationRenderer.ProbeSpacing, bounced.Probes.Cascades);
        }
        // Where no cascade of probes holds a cell, or light does not bounce, the ambient lights.
        foreach (var light in renderWorld.TryGet<RenderLights>()?.All ?? [])
            if (light.Kind == LightKind.Ambient) uniforms.Ambient += new Vector4(light.EmittedColor, 0);
        var field = bounced?.Field ?? renderWorld.TryGet<SceneFieldBinding>()?.Field ?? (_noField ??= device.CreateSceneField(1, 1));
        _retired.Add((_frame, device.RecordFog(renderContext.CommandBuffer, sized.Fog, MemoryMarshal.AsBytes(new ReadOnlySpan<Uniforms>(in uniforms)),
            shadowView, shadowSampler, bounced?.Probes, field)));
        _push = new DecodePush { InverseViewProjection = shown, Eye = new Vector4(view.Eye, 1), Slices = new Vector4(start, far, scale.X, scale.Y) };
    }

    // The sun, the shadowed light where it is directional or the first directional one, as global
    // illumination finds it, with its cascades where the window's shadow pass drew its map this
    // frame, and the map or a white texture standing in where there is none.
    private static (IImageView View, ISampler Sampler) Sun(RenderWorld renderWorld, GraphicsDevice device, VolumetricFogSettings settings, ref Uniforms uniforms)
    {
        var all = renderWorld.TryGet<RenderLights>()?.All ?? [];
        var shadow = renderWorld.TryGet<FrameShadow>();
        var shadowed = shadow?.Light ?? -1;
        var sun = shadowed >= 0 && shadowed < all.Count && all[shadowed].Kind == LightKind.Directional
            ? shadowed : all.FindIndex(light => light.Kind == LightKind.Directional);
        if (sun >= 0)
        {
            uniforms.ToSun = new Vector4(-all[sun].Direction, 1);
            uniforms.SunColor = new Vector4(all[sun].EmittedColor, Math.Clamp(settings.Anisotropy, -0.95f, 0.95f));
        }
        if (sun >= 0 && shadow is { } drawn && drawn.Light == sun && renderWorld.TryGet<ModelRenderer>()?.DrawnShadowMap is { } map
            && map.Extent.Width == 2 * drawn.TileSize)
        {
            var counted = Math.Min(drawn.Cascades.Count, 4);
            Span<Matrix4x4> cascades = [Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.Identity];
            Span<float> texels = [0, 0, 0, 0];
            for (int c = 0; c < counted; c++) (cascades[c], texels[c]) = (drawn.Cascades[c].ViewProjection, drawn.Cascades[c].Texel);
            (uniforms.Cascade0, uniforms.Cascade1, uniforms.Cascade2, uniforms.Cascade3) = (cascades[0], cascades[1], cascades[2], cascades[3]);
            uniforms.SunTexels = new Vector4(texels[0], texels[1], texels[2], texels[3]);
            uniforms.SunMap = new Vector4(counted, 1f / (2 * drawn.TileSize), 0, 0);
            return (map.DepthView, map.Sampler);
        }
        return renderWorld.TryGet<GpuTextures>()!.ViewFor(device, 0);
    }

    // How far the camera's far plane lies from its eye, at its middle, through the inverse of its
    // view and projection, or no finite distance where it cannot be told.
    private static float FarPlane(Matrix4x4 inverse, Vector3 eye)
    {
        var far = Vector4.Transform(new Vector4(0, 0, 1, 1), inverse);
        return MathF.Abs(far.W) < 1e-12f ? float.PositiveInfinity : Vector3.Distance(new Vector3(far.X, far.Y, far.Z) / far.W, eye);
    }

    // A volume and the decode pass's set reading it.
    private (GpuFog Fog, IDescriptorSet Set) Made(GraphicsDevice device, uint across, uint down, uint slices)
    {
        var fog = device.CreateFog(across, down, slices);
        var set = device.CreateDescriptorSet(DecodeLayout(device));
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(fog.SummedView, fog.Sampler, 0));
        return (fog, set);
    }

    private static IDisposable Disposing((GpuFog Fog, IDescriptorSet Set) made) => new Both(made.Set, made.Fog);

    private sealed class Both(IDisposable first, IDisposable second) : IDisposable
    {
        public void Dispose()
        {
            first.Dispose();
            second.Dispose();
        }
    }

    // Destroys what was let go once the frames in flight that might read it have finished.
    private void Retire()
    {
        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            _retired[i].Disposable.Dispose();
            _retired.RemoveAt(i);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, disposable) in _retired) disposable.Dispose();
        _retired.Clear();
        if (_sized is { } sized) Disposing(sized).Dispose();
        if (_standIn is { } standIn) Disposing(standIn).Dispose();
        (_sized, _standIn) = (null, null);
        _noField?.Dispose();
        _decodeLayout?.Dispose();
    }
}

/// <summary>Render graph node that lights the fog's volume for the window's camera, after the window's shadow and before its scene is decoded.</summary>
internal sealed class VolumetricFogNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<VolumetricFogRenderer>()?.Draw(renderContext, renderWorld);
}

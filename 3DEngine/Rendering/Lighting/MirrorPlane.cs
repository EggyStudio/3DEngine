using System.Numerics;

namespace Engine;

/// <summary>
/// The plane the window's glossy surfaces lying in it mirror the scene in, a world resource
/// <see cref="E3D.SetReflectionPlane"/> sets and the renderer reads each frame.
/// </summary>
internal sealed class ReflectionPlane
{
    /// <summary>The plane's normal, of unit length.</summary>
    public Vector3 Normal { get; set; }

    /// <summary>How far along <see cref="Normal"/> from the origin the plane lies.</summary>
    public float Height { get; set; }
}

/// <summary>
/// Draws the window's view mirrored in the program's plane (<see cref="ReflectionPlane"/>) into a
/// picture at half the window's size, which a glossy surface lying in the plane reads at its pixel's
/// place (<c>mirrored</c> in lights.slang).
/// </summary>
/// <remarks>
/// The window's batches are drawn again through its view-projection mirrored in the plane, turned
/// upside down so the mirror keeps each face's order of corners and the faces turned from the eye
/// are left out as they are, with its near plane laid along the plane, so what lies under a water's
/// surface is cut away rather than mirrored up through it (Lengyel's oblique depth projection, read
/// straight off the view-projection, whose depth row it replaces). They are lit as a reflection
/// probe's faces are, by the window's lights and its shadow maps, with no screen's probes, ambient
/// occlusion or traced reflection, none of which the mirrored view has, its light kept linear, and
/// the particles drawn over them; the shapes drawn by calls in the frame are not. A batch wholly
/// under the plane, as a voxel world's sections under its sea are, is passed over before it is
/// drawn, the near plane culling as it clips. The picture is
/// copied into an image with its mips, which a rough surface reads a blurred level of.
/// </remarks>
internal sealed class MirrorRenderer : IDisposable
{
    /// <summary>How far off the plane, in world units, a surface still takes its image, all of it on the plane and none that far off.</summary>
    internal const float Reach = 0.1f;

    private GpuPlanarReflection? _planar;
    private readonly List<(long Frame, IDisposable Held)> _retired = [];
    private long _frame;

    /// <summary>The picture drawn this frame, or none before the first.</summary>
    internal GpuPlanarReflection? Picture => _planar;

    /// <summary>
    /// Whether this frame draws the mirrored view, a plane set and the window drawing its scene
    /// through a camera on the side of the plane its normal points to.
    /// </summary>
    internal static bool Draws(RenderWorld renderWorld) =>
        renderWorld.TryGet<ReflectionPlane>() is { } plane && renderWorld.TryGet<WindowView>() is { } view
        && Vector3.Dot(plane.Normal, view.Eye) > plane.Height && renderWorld.TryGet<SwapchainTarget>() is not null
        && BloomRenderer.ShowsScene(renderWorld) && renderWorld.TryGet<ModelDrawList>() is { WindowViewProjection: not null };

    /// <summary>
    /// The window's view-projection mirrored in the plane of <paramref name="normal"/> lying
    /// <paramref name="height"/> along it, turned upside down, its near plane the plane itself.
    /// </summary>
    internal static Matrix4x4 Mirrored(Matrix4x4 viewProjection, Vector3 normal, float height)
    {
        var mirrored = Matrix4x4.CreateReflection(new Plane(normal, -height)) * viewProjection * Matrix4x4.CreateScale(1, -1, 1);
        if (!Matrix4x4.Invert(mirrored, out var inverse)) return mirrored;
        // The plane in clip space gives the corner of the frustum furthest from it, and the depth
        // row becomes the plane scaled to put that corner on the far plane, so depth runs from 0
        // on the plane to 1 there and a point under the plane falls before the near plane.
        var plane = new Vector4(normal, -height);
        var inClip = Vector4.Transform(plane, Matrix4x4.Transpose(inverse));
        var corner = Vector4.Transform(new Vector4(MathF.Sign(inClip.X), MathF.Sign(inClip.Y), 1, 1), inverse);
        var depth = plane / Vector4.Dot(plane, corner);
        (mirrored.M13, mirrored.M23, mirrored.M33, mirrored.M43) = (depth.X, depth.Y, depth.Z, depth.W);
        return mirrored;
    }

    /// <summary>Draws the mirrored view this frame, where <see cref="Draws"/> says so, ahead of the window's scene that reads it.</summary>
    public void Draw(RenderContext renderContext, RenderWorld renderWorld)
    {
        Retire();
        if (!Draws(renderWorld) || renderContext.Device is not GraphicsDevice device || renderWorld.TryGet<ModelRenderer>() is not { } models)
            return;
        var plane = renderWorld.TryGet<ReflectionPlane>()!;
        var view = renderWorld.TryGet<WindowView>()!;
        var window = renderWorld.TryGet<SwapchainTarget>()!.Extent;
        if (_planar is null || _planar.Window != window)
        {
            if (_planar is not null) _retired.Add((_frame, _planar));
            _planar = device.CreatePlanarReflection(window.Width, window.Height);
        }

        var viewProjection = Mirrored(view.ViewProjection, plane.Normal, plane.Height);
        var eye = Vector3.Transform(view.Eye, Matrix4x4.CreateReflection(new Plane(plane.Normal, -plane.Height)));
        // Cleared as the window is, in linear light, so what no mesh covers shows what the window shows.
        var clear = renderWorld.TryGet<ClearColor>() is { } windowClear
            ? new ClearColor(BloomRenderer.SrgbToLinear(windowClear.R), BloomRenderer.SrgbToLinear(windowClear.G), BloomRenderer.SrgbToLinear(windowClear.B), 1)
            : ClearColor.Black;
        var target = _planar.Target;
        var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, LoadOp.Clear, StoreOp.Store, clear));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        models.Draw(pass, target.RenderPass, renderContext, renderWorld, 0, viewProjection, ModelRenderer.ProbeCaptureLights, depth: true);
        renderWorld.TryGet<ParticleRenderer>()?.Draw(pass, target.RenderPass, renderContext, renderWorld, ModelRenderer.ProbeCaptureLights, viewProjection, eye);
        pass.EndRenderPass();
        device.RecordPlanarReflection(renderContext.CommandBuffer, _planar);
    }

    // Destroys a picture let go for a window of another size once no frame in flight reads it.
    private void Retire()
    {
        _frame++;
        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            _retired[i].Held.Dispose();
            _retired.RemoveAt(i);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, held) in _retired) held.Dispose();
        _retired.Clear();
        _planar?.Dispose();
        _planar = null;
    }
}

/// <summary>
/// Render graph node that draws the window's view mirrored in the program's plane, after the
/// shadow maps the window's pass reads and before that pass, which reads the mirrored picture.
/// </summary>
internal sealed class MirrorNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<MirrorRenderer>()?.Draw(renderContext, renderWorld);
}

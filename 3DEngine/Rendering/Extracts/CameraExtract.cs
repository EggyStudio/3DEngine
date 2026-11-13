using System.Numerics;

namespace Engine;

/// <summary>
/// Extracts <see cref="Camera"/> + <see cref="Transform"/> components from the ECS world
/// into render entities with <see cref="ExtractedView"/> components.
/// </summary>
/// <seealso cref="ExtractedView"/>
/// <seealso cref="CameraUniform"/>
internal sealed class CameraExtract : IExtractSystem
{
    /// <inheritdoc />
    public void Run(World world, RenderWorld renderWorld)
    {
        world.TryGetResource<EcsWorld>(out var ecs);
        if (MeshEntityDraws.WindowCamera(world, ecs) is { } window)
            renderWorld.Set(new WindowView(window.ViewProjection, window.Eye)
            {
                Jitter = world.TryGetResource<WindowJitter>(out var jitter) && world.TryGetResource<Time>(out var time)
                    ? jitter.In(time.FrameCount) : System.Numerics.Matrix4x4.Identity,
                Turned = world.TryGetResource<ModelDrawList>(out var draws) ? draws.EyeOf(window.ViewProjection)?.Turned : null,
            });
        else renderWorld.Remove<WindowView>();
        if (ecs is null) return;

        var surface = renderWorld.TryGet<RenderSurfaceInfo>();
        int surfaceW = surface?.Width > 0 ? surface!.Width : 1;
        int surfaceH = surface?.Height > 0 ? surface!.Height : 1;
        foreach (var (entity, cam) in ecs.Query<global::Engine.Camera>())
        {
            int wPixels = surfaceW, hPixels = surfaceH;
            if (cam.Target.IsValid)
            {
                wPixels = Math.Max(1, cam.Target.Texture.Width);
                hPixels = Math.Max(1, cam.Target.Texture.Height);
            }

            float aspect = hPixels > 0 ? (float)wPixels / hPixels : 1f;

            var (view, proj) = Matrices(ecs, entity, cam, aspect);

            int renderEntity = renderWorld.Spawn();
            renderWorld.Entities.Add(renderEntity, new ExtractedView
            {
                View = view,
                Projection = proj,
                Width = wPixels,
                Height = hPixels
            });
        }
    }

    /// <summary>A camera entity's view and projection matrices, with Y flipped for Vulkan.</summary>
    /// <remarks>The position and orientation are in world space, composed through parents when it has one.</remarks>
    public static (Matrix4x4 View, Matrix4x4 Projection) Matrices(EcsWorld ecs, int entity, in global::Engine.Camera cam, float aspect)
    {
        var (view, projection, _) = Turned(ecs, entity, cam, aspect);
        return (view, projection);
    }

    /// <summary>
    /// A camera entity's view and projection, and its turn alone, the view with the eye at the
    /// world's origin, which the view is the eye's translation times.
    /// </summary>
    public static (Matrix4x4 View, Matrix4x4 Projection, Matrix4x4 Turn) Turned(EcsWorld ecs, int entity, in global::Engine.Camera cam, float aspect)
    {
        Matrix4x4.Decompose(TransformPropagation.WorldMatrix(ecs, entity), out _, out var rotation, out var position);
        var turn = Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(rotation));
        var view = Matrix4x4.CreateTranslation(-position) * turn;
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(cam.FovY, aspect, cam.Near, cam.Far);
        // Flip Y for Vulkan NDC (Y points downward), preserving CCW front-face winding.
        proj.M22 = -proj.M22;
        return (view, proj, turn);
    }
}

/// <summary>
/// The camera the window's scene is drawn through this frame, the first camera entity without a
/// render texture or the frame's <c>BeginMode3D</c>, which the effects that read the scene's depth
/// work back to the world from.
/// </summary>
/// <param name="ViewProjection">Its view and projection, as the model pass takes them.</param>
/// <param name="Eye">Where it is.</param>
internal sealed record WindowView(System.Numerics.Matrix4x4 ViewProjection, System.Numerics.Vector3 Eye)
{
    /// <summary>
    /// The part of a pixel temporal antialiasing moved the camera by this frame, a move of clip
    /// space after the projection, or none.
    /// </summary>
    public System.Numerics.Matrix4x4 Jitter { get; init; } = System.Numerics.Matrix4x4.Identity;

    /// <summary>The move that takes <see cref="Jitter"/> back out, after the projection.</summary>
    public System.Numerics.Matrix4x4 Back => System.Numerics.Matrix4x4.CreateTranslation(-Jitter.Translation);

    /// <summary>
    /// Its view and projection without <see cref="Jitter"/>, which the window's depth for the
    /// effects that read it is drawn through and the history and motion blur are reprojected
    /// through, so they hold still where the camera does.
    /// </summary>
    public System.Numerics.Matrix4x4 Unjittered => ViewProjection * Back;

    /// <summary>
    /// Its view and projection made with the eye at nothing, where the frame noted the camera's eye
    /// (<see cref="ViewEye"/>), or null for a camera made without one.
    /// </summary>
    public System.Numerics.Matrix4x4? Turned { get; init; }

    /// <summary>
    /// Its view and projection taking a place against <paramref name="origin"/> to clip space, from
    /// its eye where the frame noted it (<see cref="RenderOrigin.Seen"/>), so a view of a frame
    /// before is read against this frame's origin with no float of the world's size in it.
    /// </summary>
    public System.Numerics.Matrix4x4 Against(RenderOrigin origin) =>
        origin.Seen(ViewProjection, Turned is { } turned ? new ViewEye(Eye, turned) : null);

    /// <summary>The same without <see cref="Jitter"/>, as <see cref="Unjittered"/> is.</summary>
    public System.Numerics.Matrix4x4 UnjitteredAgainst(RenderOrigin origin) => Against(origin) * Back;

    /// <summary>
    /// The inverse of <see cref="Against"/>, clip space back to a place against
    /// <paramref name="origin"/>, which takes a point of the depth drawn through it to a place with
    /// no float of the world's size in it, or null where it has none.
    /// </summary>
    public System.Numerics.Matrix4x4? InverseAgainst(RenderOrigin origin) =>
        System.Numerics.Matrix4x4.Invert(Against(origin), out var inverse) ? inverse : null;

    /// <summary>The inverse of <see cref="UnjitteredAgainst"/>, or null where it has none.</summary>
    public System.Numerics.Matrix4x4? UnjitteredInverseAgainst(RenderOrigin origin) =>
        System.Numerics.Matrix4x4.Invert(UnjitteredAgainst(origin), out var inverse) ? inverse : null;

    /// <summary>
    /// Its eye and its view and projection made with the eye at nothing without <see cref="Jitter"/>,
    /// where the frame noted the camera's eye, or null.
    /// </summary>
    public ViewEye? UnjitteredEye => Turned is { } turned ? new ViewEye(Eye, turned * Back) : null;

    /// <summary>
    /// The way from its clip space without <see cref="Jitter"/> to <paramref name="before"/>'s, the
    /// frame before's (<see cref="RenderOrigin.Reprojection"/>), or null where it has no inverse.
    /// </summary>
    public System.Numerics.Matrix4x4? To(WindowView before) =>
        RenderOrigin.Reprojection(Unjittered, UnjitteredEye, before.Unjittered, before.UnjitteredEye);

    /// <summary>
    /// Whether it is the camera <paramref name="other"/> is, its jitter aside, by the same eye and
    /// the same view and projection made with the eye at nothing where both eyes were noted, and by
    /// the same view-projection within the rounding of taking the jitter back out of each otherwise.
    /// </summary>
    public bool Same(WindowView other) => UnjitteredEye is { } here && other.UnjitteredEye is { } then
        ? here.Eye == then.Eye && Alike(here.Turned, then.Turned)
        : Alike(Unjittered, other.Unjittered);

    private static bool Alike(System.Numerics.Matrix4x4 a, System.Numerics.Matrix4x4 b)
    {
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
                if (Math.Abs(a[row, column] - b[row, column]) > 1e-6f + 1e-5f * Math.Abs(a[row, column])) return false;
        return true;
    }
}

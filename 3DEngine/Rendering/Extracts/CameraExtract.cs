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
        if (MeshEntityDraws.WindowCamera(world, ecs) is { } window) renderWorld.Set(new WindowView(window.ViewProjection, window.Eye));
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
        Matrix4x4.Decompose(TransformPropagation.WorldMatrix(ecs, entity), out _, out var rotation, out var position);
        var view = Matrix4x4.CreateTranslation(-position) * Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(rotation));
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(cam.FovY, aspect, cam.Near, cam.Far);
        // Flip Y for Vulkan NDC (Y points downward), preserving CCW front-face winding.
        proj.M22 = -proj.M22;
        return (view, proj);
    }
}

/// <summary>
/// The camera the window's scene is drawn through this frame, the first camera entity without a
/// render texture or the frame's <c>BeginMode3D</c>, which the effects that read the scene's depth
/// work back to the world from.
/// </summary>
/// <param name="ViewProjection">Its view and projection, as the model pass takes them.</param>
/// <param name="Eye">Where it is.</param>
internal sealed record WindowView(System.Numerics.Matrix4x4 ViewProjection, System.Numerics.Vector3 Eye);

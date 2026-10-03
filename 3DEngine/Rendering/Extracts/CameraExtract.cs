using System.Numerics;

namespace Engine;

/// <summary>
/// Extracts <see cref="Camera"/> + <see cref="Transform"/> components from the ECS world
/// into render entities with <see cref="ExtractedView"/> components.
/// </summary>
/// <seealso cref="ExtractedView"/>
/// <seealso cref="CameraUniform"/>
public sealed class CameraExtract : IExtractSystem
{
    /// <inheritdoc />
    public void Run(World world, RenderWorld renderWorld)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs)) return;

        var surface = renderWorld.TryGet<RenderSurfaceInfo>();
        int surfaceW = surface?.Width > 0 ? surface!.Width : 1;
        int surfaceH = surface?.Height > 0 ? surface!.Height : 1;
        var textures = renderWorld.TryGet<RenderTextures>();

        foreach (var (entity, cam) in ecs.Query<global::Engine.Camera>())
        {
            int wPixels = surfaceW, hPixels = surfaceH;
            if (!string.IsNullOrEmpty(cam.TargetName) && textures != null && textures.TryGet(cam.TargetName!, out var desc))
            {
                wPixels = Math.Max(1, desc.Width);
                hPixels = Math.Max(1, desc.Height);
            }

            float aspect = hPixels > 0 ? (float)wPixels / hPixels : 1f;

            Transform t = default;
            ecs.TryGet(entity, out t);
            var view = Matrix4x4.CreateTranslation(-t.Position) * Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(t.Rotation));
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(cam.FovY, aspect, cam.Near, cam.Far);
            // Flip Y for Vulkan NDC (Y points downward), preserving CCW front-face winding.
            proj.M22 = -proj.M22;

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
}

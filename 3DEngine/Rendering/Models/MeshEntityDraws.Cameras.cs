using System.Numerics;

namespace Engine;

// The cameras mesh entities are drawn through, apart from how they are gathered.
internal sealed partial class MeshEntityDraws
{
    // The cameras mesh entities are drawn through, the first without a render texture into the
    // window, then each with one into it, whose target is cleared to its background unless the
    // program clears it itself.
    internal static List<(Matrix4x4 ViewProjection, Vector3 Eye, int Target)> Cameras(World world, EcsWorld ecs)
    {
        var cameras = new List<(Matrix4x4, Vector3, int)>();
        var window = false;
        foreach (var (entity, camera) in ecs.Query<Camera>())
        {
            var target = camera.Target.IsValid ? camera.Target.Texture.Id : 0;
            if (target == 0 && window) continue;
            var (viewProjection, eye) = Through(world, ecs, entity, camera);
            var seen = (viewProjection, eye, target);
            if (target == 0)
            {
                window = true;
                cameras.Insert(0, seen);
            }
            else
            {
                cameras.Add(seen);
                world.TryGetResource<DrawList>(out var drawList);
                drawList?.UseTarget(target, camera.Background);
            }
        }
        if (!window && world.TryGetResource<Mode3DCamera>(out var flat) && flat.ViewProjection is { } flatViewProjection)
            cameras.Insert(0, (flatViewProjection, flat.Eye, 0));
        return cameras;
    }

    // A camera entity's view-projection and eye. The shape is its render texture's, or the
    // window's, or with no window (an offscreen run) the size the config asked for, at which
    // the frames are drawn. Assuming square there stretched every mesh entity in an offscreen
    // capture while the 2D drawing beside it was right.
    private static (Matrix4x4 ViewProjection, Vector3 Eye) Through(World world, EcsWorld ecs, int entity, in Camera camera)
    {
        var (width, height) = camera.Target.IsValid ? (camera.Target.Texture.Width, camera.Target.Texture.Height)
            : world.TryGetResource<AppWindow>(out var appWindow) ? (appWindow.Sdl.Width, appWindow.Sdl.Height)
            : world.TryGetResource<OffscreenSurface>(out var surface) ? ((int)surface.Size.Width, (int)surface.Size.Height)
            : world.TryGetResource<Config>(out var config) ? (config.WindowData.Width, config.WindowData.Height) : (1, 1);
        var aspect = height > 0 ? (float)width / height : 1f;
        var (view, projection) = CameraExtract.Matrices(ecs, entity, camera, aspect);
        return (view * projection, TransformPropagation.WorldMatrix(ecs, entity).Translation);
    }

    /// <summary>
    /// The camera the window is drawn through, as <see cref="Cameras"/> gives it first, the first
    /// camera entity without a render texture or the frame's <c>BeginMode3D</c>, or null for none.
    /// </summary>
    internal static (Matrix4x4 ViewProjection, Vector3 Eye)? WindowCamera(World world, EcsWorld? ecs)
    {
        if (ecs is not null)
            foreach (var (entity, camera) in ecs.Query<Camera>())
                if (!camera.Target.IsValid) return Through(world, ecs, entity, camera);
        return world.TryGetResource<Mode3DCamera>(out var flat) && flat.ViewProjection is { } viewProjection ? (viewProjection, flat.Eye) : null;
    }
}

/// <summary>
/// The camera of the first <c>BeginMode3D</c> of the frame drawn into the window, a world resource
/// <see cref="MeshEntityDraws"/> draws mesh entities through when no <see cref="Camera"/> entity
/// draws the window, cleared as each frame begins.
/// </summary>
internal sealed class Mode3DCamera
{
    /// <summary>The camera's view and projection, or null when no <c>BeginMode3D</c> has drawn into the window this frame.</summary>
    public Matrix4x4? ViewProjection { get; set; }

    /// <summary>Where the camera is, which the mesh entities are sorted from.</summary>
    public Vector3 Eye { get; set; }

    /// <summary>The camera itself, which the modeling window casts the pointer's ray through, or null with the view and projection.</summary>
    public Camera3D? Camera { get; set; }

    /// <summary>
    /// The first camera each render target was drawn through in 3D this frame, by its texture id,
    /// which particles are drawn into it through.
    /// </summary>
    public Dictionary<int, (Matrix4x4 ViewProjection, Vector3 Eye)> Targets { get; } = [];
}

using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    private static Entity _ambient;

    // -- Lights, which are Light entities in the ECS, so a program with light entities of its own
    // and one that makes lights here light the same models.

    /// <summary>A light from far away along <paramref name="direction"/>, as the sun's, which may cast the frame's shadow.</summary>
    public static LightHandle CreateDirectionalLight(Vector3 direction, Color color, float intensity = 1, bool castsShadows = false) =>
        Spawn(Light.Directional(Linear(color), intensity) with { CastsShadows = castsShadows }, Vector3.Zero, direction);

    /// <summary>
    /// A light shining every way from <paramref name="position"/>, reaching <paramref name="range"/>
    /// units, or every distance with 0, which may cast shadows all around it, as the twelve such
    /// lights that matter most to the camera's view do.
    /// </summary>
    /// <remarks>
    /// A model drawn around a shadowed light, as a lamp's bulb, shadows everything from it. Draw the
    /// bulb as a shape, with <see cref="DrawSphere"/>, which casts no shadow.
    /// </remarks>
    public static LightHandle CreatePointLight(Vector3 position, Color color, float intensity = 1, float range = 0, bool castsShadows = false) =>
        Spawn(Light.Point(Linear(color), intensity, range) with { CastsShadows = castsShadows }, position, -Vector3.UnitZ);

    /// <summary>
    /// A light from <paramref name="position"/> along <paramref name="direction"/>, full inside
    /// <paramref name="innerAngle"/> and gone past <paramref name="outerAngle"/> degrees, which may
    /// cast a shadow, as the ten such lights that matter most to the camera's view do.
    /// </summary>
    public static LightHandle CreateSpotLight(Vector3 position, Vector3 direction, Color color, float intensity = 1,
        float innerAngle = 25, float outerAngle = 30, float range = 0, bool castsShadows = false) =>
        Spawn(Light.Spot(Linear(color), intensity, innerAngle, outerAngle, range) with { CastsShadows = castsShadows }, position, direction);

    /// <summary>Moves a point or spot light.</summary>
    public static void SetLightPosition(LightHandle light, Vector3 position)
    {
        if (Resolve(light) is { } entity) Ecs.GetRef<Transform>(entity).Position = position;
    }

    /// <summary>Turns a directional or spot light to shine along <paramref name="direction"/>.</summary>
    public static void SetLightDirection(LightHandle light, Vector3 direction)
    {
        if (Resolve(light) is { } entity) Ecs.GetRef<Transform>(entity).Rotation = Facing(direction);
    }

    /// <summary>Sets a light's color and how bright it is.</summary>
    public static void SetLightColor(LightHandle light, Color color, float intensity = 1)
    {
        if (Resolve(light) is not { } entity) return;
        ref var l = ref Ecs.GetRef<Light>(entity);
        l.Color = Linear(color);
        l.Intensity = intensity;
    }

    /// <summary>Sets whether a light casts shadows, which the first directional light that does casts, and of spot and point lights the ten and the twelve that matter most to the camera's view.</summary>
    public static void SetLightCastsShadows(LightHandle light, bool castsShadows)
    {
        if (Resolve(light) is { } entity) Ecs.GetRef<Light>(entity).CastsShadows = castsShadows;
    }

    /// <summary>
    /// Sets how far past the camera, in world units, the sun's shadows reach (150 by default), and
    /// how far a spot or point light with no range casts them. A shorter distance spends the same
    /// shadow map on less ground, so its shadows are sharper.
    /// </summary>
    public static void SetShadowDistance(float distance) =>
        World.GetOrInsertResource(() => new ShadowSettings()).Distance = Math.Max(1, distance);

    /// <summary>
    /// Sets how many texels wide each tile of the shadow map is, the sun's cascades and the spot
    /// lights each having one, rounded to a power of two from 256 to 4096 (2048 by default). A point
    /// light's faces are a quarter of it.
    /// </summary>
    /// <remarks>
    /// A game's shadow quality setting: 4096 sharpens shadows at four times the memory and drawing
    /// of 2048, and 1024 is cheaper and softer. The map is made again at the new size the next frame.
    /// </remarks>
    public static void SetShadowMapSize(int size) =>
        World.GetOrInsertResource(() => new ShadowSettings()).TileSize =
            (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Clamp(size, 256, 4096));

    /// <summary>Removes a light.</summary>
    public static void UnloadLight(LightHandle light)
    {
        if (Resolve(light) is { } entity) Ecs.Despawn(entity);
    }

    /// <summary>
    /// Darkens the light from all around where the surfaces near a point close it off, as where a
    /// floor meets a wall or under a crate, by <paramref name="intensity"/>, looking for those
    /// surfaces within <paramref name="radius"/> world units, or turns it off with an intensity of
    /// 0, which it is by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It darkens the ambient light, the environment map and a reflection probe's light, and not a
    /// light's own, which reaches a corner as well as a wall. 1 suits most scenes, and a radius
    /// about the size of the things that stand close together, a unit for a room's furniture and a
    /// few for buildings.
    /// </para>
    /// <para>
    /// It is worked out for the window's view each frame, from the depth of its models drawn at
    /// half the window's size ahead of its pass, so a frame costs one more pass over the models
    /// that cast a shadow, and a model that casts none darkens nothing around it. Render textures
    /// and probe captures are drawn without it.
    /// </para>
    /// </remarks>
    public static void SetAmbientOcclusion(float intensity, float radius = 1)
    {
        var occlusion = World.GetOrInsertResource(static () => new AmbientOcclusionSettings());
        occlusion.Intensity = Math.Max(0, intensity);
        occlusion.Radius = Math.Max(0, radius);
    }

    /// <summary>
    /// Builds a distance field of the scene around the camera, in <paramref name="cascades"/>
    /// cascades from 1 to 8, the finest's cells <paramref name="cellSize"/> world units wide, or
    /// turns it off with 0 cascades, which it is by default. Ambient occlusion reads it beside the
    /// window's depth, the sun casts soft contact shadows through it, and particles collide with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The field holds how far each cell of a cascade is from the nearest surface of the meshes
    /// drawn into the window that cast shadows. Each cascade is 64 cells a side, twice as coarse
    /// and as wide as the one before, so the default of 0.25 reaches 16 units across in the first
    /// and 128 in the fourth, and a thing much thinner than a cell is not there to it.
    /// </para>
    /// <para>
    /// A mesh drawn in the same place for eight frames is built into the cascades around it from
    /// its triangles on the GPU, and one that moves is stamped as the box around it each frame. A
    /// cascade is built again where the camera has gone past it or a mesh came or went,
    /// <paramref name="updateBudget"/> a frame at most, the finest first, so a game sets a larger
    /// budget for a scene that changes and a smaller one for a slow GPU. The frame profile names
    /// its cost as <c>scene_field</c>.
    /// </para>
    /// </remarks>
    public static void SetSceneField(int cascades, float cellSize = SceneFieldConfig.DefaultCellSize, int updateBudget = SceneFieldConfig.DefaultUpdateBudget)
    {
        var field = World.GetOrInsertResource(static () => new SceneFieldSettings());
        field.Cascades = Math.Clamp(cascades, 0, 8);
        field.CellSize = Math.Max(1e-3f, cellSize);
        field.UpdateBudget = Math.Max(1, updateBudget);
    }

    /// <summary>
    /// Sets the light from all around that keeps the side of a model away from every other light
    /// from going black, replacing the one set before, or removes it with an intensity of 0.
    /// </summary>
    public static void SetAmbientLight(Color color, float intensity)
    {
        if (Ecs.TryResolve(_ambient, out var existing))
        {
            if (intensity <= 0)
            {
                Ecs.Despawn(existing);
                _ambient = Entity.None;
                return;
            }
            Ecs.GetRef<Light>(existing) = Light.Ambient(Linear(color), intensity);
            return;
        }
        if (intensity > 0) _ambient = Spawn(Light.Ambient(Linear(color), intensity), Vector3.Zero, -Vector3.UnitZ).Entity;
    }

    private static EcsWorld Ecs => Res<EcsWorld>();

    private static LightHandle Spawn(Light light, Vector3 position, Vector3 direction)
    {
        var entity = Ecs.Spawn();
        Ecs.Add(entity, light);
        Ecs.Add(entity, new Transform(position, Facing(direction), Vector3.One));
        return new LightHandle(Ecs.Handle(entity));
    }

    private static int? Resolve(LightHandle light) =>
        Ecs.TryResolve(light.Entity, out var entity) && Ecs.Has<Light>(entity) ? entity : null;

    /// <summary>The rotation that turns a light's -Z, the way it shines, to <paramref name="direction"/>.</summary>
    internal static Quaternion Facing(Vector3 direction)
    {
        if (direction.LengthSquared() < 1e-12f) return Quaternion.Identity;
        var to = Vector3.Normalize(direction);
        var from = -Vector3.UnitZ;
        var dot = Vector3.Dot(from, to);
        if (dot > 0.99999f) return Quaternion.Identity;
        if (dot < -0.99999f) return Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        return Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(from, to)), MathF.Acos(dot)));
    }
}

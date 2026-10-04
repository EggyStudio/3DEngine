using System.Numerics;

namespace Engine;

/// <summary>A light the flat API made, by the entity that holds its <see cref="Light"/>.</summary>
public readonly record struct LightHandle(Entity Entity)
{
    /// <summary>Whether this names a light that was made.</summary>
    public bool IsValid => !Entity.IsNone;
}

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
    /// units, or every distance with 0, which may cast shadows all around it, as the first four
    /// such lights do.
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
    /// cast a shadow, as the first such light does.
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

    /// <summary>Sets whether a light casts shadows, which the first directional, the first spot and the first four point lights that do cast.</summary>
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

    /// <summary>Removes a light.</summary>
    public static void UnloadLight(LightHandle light)
    {
        if (Resolve(light) is { } entity) Ecs.Despawn(entity);
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

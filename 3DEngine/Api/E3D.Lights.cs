using System.Numerics;

namespace Engine;

public static partial class E3D
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
        if (TryRes<PhysicalUnits>(out var units)) units.Lights.Remove(light);
    }

    /// <summary>
    /// Sets a directional light's strength as the lux it gives a surface facing it, keeping its
    /// color, a noon sun's being about 100,000 and a sun through thin cloud's some 30,000.
    /// </summary>
    /// <remarks>
    /// A light given in physical units is seen through the camera <see cref="SetCameraExposure"/>
    /// sets, or a sunny day's of EV 15 where none is set, so a white surface in a noon sun reads
    /// about white, and its color is taken for its hue alone, the lux carrying its luminance. The
    /// intensity <see cref="SetLightColor"/> gives a light afterwards is a plain one again, which
    /// the camera does not see, so a scene of plain lights and <see cref="SetExposure"/> looks as it
    /// did.
    /// </remarks>
    public static void SetLightIlluminance(LightHandle light, float lux) => SetPhysical(light, lux, PhysicalQuantity.Illuminance);

    /// <summary>
    /// Sets a point or spot light's strength as its luminous intensity in candelas along its axis,
    /// keeping its color, a candle's being 1 and a 60 watt bulb's about 70.
    /// </summary>
    /// <remarks>Seen through the camera as <see cref="SetLightIlluminance"/> says.</remarks>
    public static void SetLightLuminousIntensity(LightHandle light, float candela) => SetPhysical(light, candela, PhysicalQuantity.LuminousIntensity);

    /// <summary>
    /// Sets a point or spot light's strength as the lumens it sends out in all, over the whole
    /// sphere for a point light and over its outer cone for a spot, keeping its color, a 60 watt
    /// bulb's being about 800.
    /// </summary>
    /// <remarks>
    /// Seen through the camera as <see cref="SetLightIlluminance"/> says. A spot keeps its lumens
    /// as its cone narrows, which takes its light along the axis up, as a torch's does when its
    /// beam is focused.
    /// </remarks>
    public static void SetLightLuminousFlux(LightHandle light, float lumens) => SetPhysical(light, lumens, PhysicalQuantity.LuminousFlux);

    private static void SetPhysical(LightHandle light, float value, PhysicalQuantity quantity)
    {
        if (Resolve(light) is null || !float.IsFinite(value)) return;
        var units = World.GetOrInsertResource(static () => new PhysicalUnits());
        units.Lights[light] = (Math.Max(0, value), quantity);
        ApplyPhysical(units, light);
    }

    // A light given in physical units given its intensity through the camera, as its value and
    // color say.
    private static void ApplyPhysical(PhysicalUnits units, LightHandle light)
    {
        if (Resolve(light) is not { } entity || !units.Lights.TryGetValue(light, out var given)) return;
        ref var l = ref Ecs.GetRef<Light>(entity);
        var luminance = 0.2126f * l.Color.X + 0.7152f * l.Color.Y + 0.0722f * l.Color.Z;
        l.Intensity = units.IntensityOf(l.Kind, given.Value, given.Quantity, l.OuterAngle, luminance);
    }

    // Every light given in physical units given its intensity again, after the camera changed,
    // those since unloaded let go.
    internal static void ApplyPhysicalLights(PhysicalUnits units)
    {
        foreach (var light in units.Lights.Keys.ToArray())
            if (Resolve(light) is null) units.Lights.Remove(light);
            else ApplyPhysical(units, light);
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
    /// Sets whether the sun's shadows go on past the shadow distance to the camera's far plane
    /// through a fill cascade, which the cascades' count leaves out, on by default.
    /// </summary>
    /// <remarks>
    /// The fill is one more drawing of the scene's depth into a map of its own at the shadow map's
    /// tile size, its texels meters wide where the land it covers is a few pixels of the picture, so
    /// hills and buildings far off cast their shadows and nothing ends at a line. A program whose
    /// view ends near the shadow distance, indoors or in fog, saves the drawing by turning it off.
    /// The contact shadows kept past the shadow map's reach (<see cref="ContactShadows.PastShadowMap"/>)
    /// go on over the fill's.
    /// </remarks>
    public static void SetShadowFill(bool enabled) =>
        World.GetOrInsertResource(() => new ShadowSettings()).Fill = enabled;

    /// <summary>
    /// Sets how wide the sun looks across, in degrees, which softens its shadow's edge the further
    /// the shadow falls from what casts it, 0 by default, an edge as sharp at a pole's tip as at its
    /// foot. The sun seen from the earth is 0.53 degrees across.
    /// </summary>
    /// <remarks>
    /// Each point the sun's shadow map holds looks for what shades it over a width its depth in the
    /// map sets, and takes the light the map lets through over the width that what shades it, as
    /// far off as it is, spreads the sun's disc to, so a shadow is sharp where a caster meets the
    /// ground and soft where it falls far from it. The width is held to 24 of a cascade's texels, so
    /// past that a cascade's shadow is as soft as it gets. The spot and point lights' shadows keep
    /// their edges, and so do the contact shadows through the scene's distance field.
    /// </remarks>
    public static void SetShadowSoftness(float size) =>
        World.GetOrInsertResource(() => new ShadowSettings()).Softness = Math.Clamp(size, 0, 20);

    /// <summary>
    /// Sets where the sun's contact shadows through the scene's distance field darken its light,
    /// past the shadow map's reach alone (<see cref="ContactShadows.PastShadowMap"/>, the default),
    /// everywhere the field reaches, or nowhere.
    /// </summary>
    /// <remarks>
    /// They are traced at half the window's size through the field's cells and blurred. Everywhere,
    /// they soften the edge of a shadow the map draws hard, past it by about the field's cell, and
    /// darken a sunlit surface that a ray toward the sun passes near another, as a column's base
    /// beside it, where the map holds the sun's light whole.
    /// </remarks>
    public static void SetContactShadows(ContactShadows contactShadows) =>
        World.GetOrInsertResource(() => new ShadowSettings()).Contact = contactShadows;

    /// <summary>
    /// Sets how many texels wide each tile of the shadow map is, the sun's cascades and the spot
    /// lights each having one, rounded to a power of two from 256 to 4096 (2048 by default). A point
    /// light's faces are a quarter of it.
    /// </summary>
    /// <remarks>
    /// It is a game's shadow quality setting. A size of 4096 sharpens shadows at four times the
    /// memory and drawing of 2048, and 1024 is cheaper and softer. The map is made again at the new
    /// size the next frame.
    /// </remarks>
    public static void SetShadowMapSize(int size) =>
        World.GetOrInsertResource(() => new ShadowSettings()).TileSize =
            (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Clamp(size, 256, 4096));

    /// <summary>Removes a light.</summary>
    public static void UnloadLight(LightHandle light)
    {
        if (Resolve(light) is { } entity) Ecs.Despawn(entity);
        if (TryRes<PhysicalUnits>(out var units)) units.Lights.Remove(light);
    }

    /// <summary>
    /// Darkens the light from all around where the surfaces near a point close it off, as where a
    /// floor meets a wall or under a crate, by <paramref name="intensity"/>, looking for those
    /// surfaces within <paramref name="radius"/> world units, or turns it off with an intensity of
    /// 0, which it is by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It darkens the ambient light, the environment map, a reflection probe's light and the light
    /// that global illumination gathers, and not a light's own, which reaches a corner as well as a
    /// wall. An intensity of 1 darkens by the share of the light from all around the surfaces the
    /// window shows close off, weighed by the cosine, and more raises that share to its power. A
    /// radius about the size of the things that stand close together suits a scene without global
    /// illumination, a unit for a room's furniture and a few for buildings, and with it a few of
    /// the field's cells, since the cascades hold what lies farther, 0.5 in a room of cells 0.15
    /// across.
    /// </para>
    /// <para>
    /// It is worked out for the window's view each frame, from the depth of its models drawn at
    /// half the window's size ahead of its pass, so a frame costs one more pass over the models
    /// that cast a shadow, and a model that casts none darkens nothing around it. Each pixel reads
    /// the horizons along two slices, turned each frame, and keeps most of what it held the frame
    /// before, so a pixel settles over about ten frames. A render texture that draws models
    /// through a camera works out its own the same way ahead of its pass, and probe captures are
    /// drawn without it.
    /// </para>
    /// </remarks>
    public static void SetAmbientOcclusion(float intensity, float radius = 1)
    {
        var occlusion = World.GetOrInsertResource(static () => new AmbientOcclusionSettings());
        occlusion.Intensity = Math.Max(0, intensity);
        occlusion.Radius = Math.Max(0, radius);
    }

    /// <summary>
    /// Fills the air in front of the window's camera with fog that takes <paramref name="density"/>
    /// of the light out per unit of distance at and under <paramref name="height"/>, thinning above
    /// it by e every one over <paramref name="falloff"/> units, lit by the sun through its shadows and
    /// by the light from all around, or takes it away with a density of 0, the default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pixel is seen through the fog between it and the eye, so the far reaches of a scene fade
    /// into the fog's light, and the sun's light shows in shafts where shadows cross the fog. With a
    /// falloff of 0 the fog is as thick at any height. <paramref name="anisotropy"/> is how much of
    /// the sun's light the fog scatters on along its way, toward an eye that looks toward the sun,
    /// from 0, alike every way, toward 1.
    /// </para>
    /// <para>
    /// The fog is worked out in a volume of cells in front of the camera, one for each tile of
    /// sixteen pixels of the window by 64 slices of the distance to the camera's far plane or 128
    /// units, whichever is nearer, so beyond that the fog thickens no further. It is lit by the
    /// world's probes where global illumination is on and by the ambient lights where it is off.
    /// Particles and what is drawn after <c>EndMode3D</c> are drawn over it, and render textures and
    /// reflection probes are drawn without it.
    /// </para>
    /// </remarks>
    public static void SetVolumetricFog(float density, float height = 0, float falloff = 0, float anisotropy = 0.5f)
    {
        var fog = World.GetOrInsertResource(static () => new VolumetricFogSettings());
        fog.Density = Math.Max(0, density);
        fog.Height = height;
        fog.Falloff = Math.Max(0, falloff);
        fog.Anisotropy = Math.Clamp(anisotropy, -0.95f, 0.95f);
    }

    /// <summary>The fog as <see cref="SetVolumetricFog"/> left it, a density of 0 for none unless set.</summary>
    public static (float Density, float Height, float Falloff, float Anisotropy) GetVolumetricFog() =>
        TryRes<VolumetricFogSettings>(out var fog) ? (fog.Density, fog.Height, fog.Falloff, fog.Anisotropy) : (0, 0, 0, 0.5f);

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

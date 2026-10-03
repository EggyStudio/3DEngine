namespace Engine;

/// <summary>
/// Translates <see cref="SceneLightPayload"/> components attached by <see cref="SceneSpawner"/>
/// into runtime <see cref="Light"/> components (plus optional <see cref="LightShadow"/> /
/// <see cref="LightShaping"/> companions), then removes the payload as the idempotency
/// marker — same pattern as <see cref="SceneSpawnSystem"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered by <see cref="LightingPlugin"/> in <see cref="Stage.PreUpdate"/>, after
/// <c>SceneSpawnSystem</c>, so a single load-then-spawn frame produces fully-formed
/// (Transform, Light) entities visible to <see cref="LightExtract"/> later in the same
/// frame.
/// </para>
/// <para>
/// <b>Texture resolution:</b> dome and rect-light textures are resolved against the
/// <see cref="AssetServer"/> as <i>linear</i> mipmapped textures (env maps and area-light
/// emission maps are radiometric, not sRGB display data). When no asset server is
/// available the handle is left invalid and the renderer falls back to flat color.
/// </para>
/// </remarks>
public static class LightSpawnSystem
{
    private static readonly ILogger Logger = Log.Category("Engine.Lighting");

    /// <inheritdoc cref="SceneSpawnSystem.Run"/>
    public static void Run(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.TryGetResource<EcsWorld>(out var ecs)) return;
        world.TryGetResource<AssetServer>(out var assetServer);

        // Snapshot before mutation: Add/Remove during a Query iteration is unsafe.
        List<(int Entity, SceneLightPayload Payload)>? pending = null;
        foreach (var (entity, payload) in ecs.Query<SceneLightPayload>())
            (pending ??= new()).Add((entity, payload));
        if (pending is null) return;

        foreach (var (entity, payload) in pending)
        {
            try
            {
                var light = BuildLight(payload, assetServer);
                ecs.Add(entity, light);

                if (HasAuthoredShadow(payload.Shadow))
                    ecs.Add(entity, BuildShadow(payload.Shadow!));

                if (HasAuthoredShaping(payload))
                    ecs.Add(entity, BuildShaping(payload));

                Logger.Debug(
                    $"LightSpawnSystem: entity {entity} '{payload.Name}' -> {payload.Type} " +
                    $"(intensity={payload.Intensity}, exposure={payload.Exposure}" +
                    (payload.Shadow is not null ? ", +Shadow" : "") +
                    (HasAuthoredShaping(payload) ? ", +Shaping" : "") + ").");
            }
            catch (Exception ex)
            {
                Logger.Error($"LightSpawnSystem: failed to spawn light for entity {entity} '{payload.Name}': {ex.Message}");
            }
            finally
            {
                // Remove the payload either way so a permanently-broken light doesn't loop.
                ecs.Remove<SceneLightPayload>(entity);
            }
        }
    }

    private static Light BuildLight(SceneLightPayload p, AssetServer? assets)
    {
        return new Light
        {
            Type = MapType(p.Type),
            Color = p.Color,
            Intensity = p.Intensity,
            Exposure = p.Exposure,
            Normalize = p.Normalize,
            Diffuse = p.Diffuse,
            Specular = p.Specular,
            ColorTemperature = p.ColorTemperature,
            EnableColorTemperature = p.EnableColorTemperature,

            Radius = p.Radius,
            Width = p.Width,
            Height = p.Height,
            Length = p.Length,

            DomeTexture = LoadLinear(assets, p.DomeTexturePath),
            DomeTextureFormat = MapDomeFormat(p.DomeTextureFormat),
            DomeGuideRadius = p.DomeGuideRadius,
            RectTexture = LoadLinear(assets, p.RectTexturePath),

            GeometryPaths = p.GeometryPaths.Count > 0 ? p.GeometryPaths.ToArray() : null,
            PortalPaths   = p.PortalPaths.Count   > 0 ? p.PortalPaths.ToArray()   : null,
            FilterPaths   = p.FilterPaths.Count   > 0 ? p.FilterPaths.ToArray()   : null,
        };
    }

    private static LightShadow BuildShadow(SceneLightShadow s) => new()
    {
        Enable = s.Enable,
        Color = s.Color,
        Distance = s.Distance,
        Falloff = s.Falloff,
        FalloffGamma = s.FalloffGamma,
    };

    private static LightShaping BuildShaping(SceneLightPayload p)
    {
        // The convenience shortcuts on the payload (ConeAngle / ConeSoftness /
        // IesProfilePath) take precedence so writers that only set the shortcuts still
        // produce a well-formed LightShaping component.
        var s = p.Shaping;
        return new LightShaping
        {
            ConeAngle      = p.ConeAngle      ?? s?.ConeAngle,
            ConeSoftness   = p.ConeSoftness   ?? s?.ConeSoftness,
            FocusPower     = s?.FocusPower,
            FocusTint      = s?.FocusTint,
            IesProfilePath = p.IesProfilePath ?? s?.IesProfilePath,
            IesAngleScale  = s?.IesAngleScale,
            IesNormalize   = s?.IesNormalize,
        };
    }

    private static bool HasAuthoredShadow(SceneLightShadow? s)
        => s is not null && (s.Enable.HasValue || s.Color.HasValue || s.Distance.HasValue
                             || s.Falloff.HasValue || s.FalloffGamma.HasValue);

    private static bool HasAuthoredShaping(SceneLightPayload p)
        => p.Shaping is not null
           || p.ConeAngle.HasValue || p.ConeSoftness.HasValue
           || !string.IsNullOrEmpty(p.IesProfilePath);

    private static Handle<Texture> LoadLinear(AssetServer? assets, string? path)
    {
        if (assets is null || string.IsNullOrEmpty(path)) return Handle<Texture>.Invalid;
        return assets.LoadTextureLinear(path, generateMips: true);
    }

    private static LightType MapType(SceneLightType t) => t switch
    {
        SceneLightType.Distant  => LightType.Distant,
        SceneLightType.Sphere   => LightType.Sphere,
        SceneLightType.Rect     => LightType.Rect,
        SceneLightType.Disk     => LightType.Disk,
        SceneLightType.Cylinder => LightType.Cylinder,
        SceneLightType.Dome     => LightType.Dome,
        SceneLightType.Geometry => LightType.Geometry,
        SceneLightType.Portal   => LightType.Portal,
        SceneLightType.Plugin   => LightType.Plugin,
        _ => LightType.Sphere,
    };

    private static DomeTextureFormat? MapDomeFormat(string? token) => token switch
    {
        null or "" => null,
        "automatic"             => Engine.DomeTextureFormat.Automatic,
        "latlong"               => Engine.DomeTextureFormat.LatLong,
        "mirroredBall"          => Engine.DomeTextureFormat.MirroredBall,
        "angular"               => Engine.DomeTextureFormat.Angular,
        "cubeMapVerticalCross"  => Engine.DomeTextureFormat.CubeMapVerticalCross,
        _ => Engine.DomeTextureFormat.Automatic,
    };
}
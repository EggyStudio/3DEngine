namespace Engine;

/// <summary>
/// Turns the <see cref="SceneLightPayload"/> a spawned scene node carries into a <see cref="Light"/>
/// on its entity, and removes the payload, so each light is made once.
/// </summary>
/// <remarks>
/// Registered by <see cref="LightingPlugin"/> in <see cref="Stage.PreUpdate"/>, after
/// <c>SceneSpawnSystem</c>, so a scene spawned in a frame is lit in that frame.
/// </remarks>
public static class LightSpawnSystem
{
    /// <summary>The system.</summary>
    public static void Run(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.TryGetResource<EcsWorld>(out var ecs)) return;

        // Collected first, since adding and removing components while a query walks them is unsafe.
        List<(int Entity, SceneLightPayload Payload)>? pending = null;
        foreach (var (entity, payload) in ecs.Query<SceneLightPayload>())
            (pending ??= new()).Add((entity, payload));
        if (pending is null) return;

        foreach (var (entity, payload) in pending)
        {
            ecs.Add(entity, ToLight(payload));
            ecs.Remove<SceneLightPayload>(entity);
        }
    }

    /// <summary>The <see cref="Light"/> a payload describes.</summary>
    public static Light ToLight(SceneLightPayload payload) => new()
    {
        Kind = payload.Kind,
        Color = payload.Color,
        Intensity = payload.Intensity,
        Range = payload.Range,
        InnerAngle = payload.InnerAngle,
        OuterAngle = payload.OuterAngle,
        CastsShadows = payload.CastsShadows,
    };
}

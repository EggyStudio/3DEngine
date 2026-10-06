using System.Numerics;

namespace Engine;

/// <summary>A reflection probe the flat API made, by the entity that holds its <see cref="ReflectionProbe"/>.</summary>
public readonly record struct ReflectionProbeHandle(Entity Entity)
{
    /// <summary>Whether this names a probe that was made.</summary>
    public bool IsValid => !Entity.IsNone;
}

public static partial class Engine3D
{
    // -- Reflection probes, which are ReflectionProbe entities in the ECS, as lights are.

    /// <summary>
    /// A box of <paramref name="size"/> around <paramref name="position"/> whose surfaces reflect
    /// what is around the box's middle, as a room reflects its own walls, where they would reflect
    /// the environment map.
    /// </summary>
    /// <remarks>
    /// The probe is captured from the meshes the window draws, from the first frame that draws any,
    /// a face a frame, and filtered on the GPU in the frame that draws its sixth. It is captured
    /// twice, so it is ready twelve frames later. Until it has a capture, surfaces in the box
    /// reflect the environment map. It captures again by itself when a
    /// light reaching its box is added, removed or changed past a flicker, and
    /// <see cref="UpdateReflectionProbe"/> captures it again after its room's meshes change.
    /// </remarks>
    public static ReflectionProbeHandle CreateReflectionProbe(Vector3 position, Vector3 size, float intensity = 1)
    {
        var entity = Ecs.Spawn();
        Ecs.Add(entity, new ReflectionProbe(size, intensity));
        Ecs.Add(entity, new Transform(position));
        return new ReflectionProbeHandle(Ecs.Handle(entity));
    }

    /// <summary>Captures a probe again, as its room is now drawn.</summary>
    public static void UpdateReflectionProbe(ReflectionProbeHandle probe)
    {
        if (Resolve(probe) is { } entity) Ecs.GetRef<ReflectionProbe>(entity).Capture++;
    }

    /// <summary>
    /// Whether a probe's capture of where it is now, under the lights reaching it now, has finished
    /// both its passes, so the surfaces in its box reflect the room as it is drawn.
    /// </summary>
    /// <remarks>
    /// A probe reflects something as soon as its first pass lands, and the second, captured with the
    /// first bound, gives what it reflects of itself. Ready waits for the second, so a frame taken
    /// once it is ready shows what every later frame shows.
    /// </remarks>
    public static bool IsReflectionProbeReady(ReflectionProbeHandle probe)
    {
        if (Resolve(probe) is not { } entity || !TryRes<ReflectionProbes>(out var probes)
            || !probes.ByEntity.TryGetValue(entity, out var known) || known.Map is null || known.Captured is not { } captured) return false;
        // Against the component as it is now, which the renderer takes up at the end of the frame.
        // What was captured counts the captures a change in the lights asked for beside the
        // component's own, so a probe relit was never ready again while the two were compared bare.
        var wanted = Ecs.GetReadOnly<ReflectionProbe>(entity);
        return captured.Size == wanted.Size && captured.Capture == wanted.Capture + known.Relit && captured == known.Wanted
            && known.Passes >= ReflectionProbes.Passes;
    }

    /// <summary>Removes a probe, and its box reflects the environment map again.</summary>
    public static void UnloadReflectionProbe(ReflectionProbeHandle probe)
    {
        if (Resolve(probe) is { } entity) Ecs.Despawn(entity);
    }

    private static int? Resolve(ReflectionProbeHandle probe) =>
        Ecs.TryResolve(probe.Entity, out var entity) && Ecs.Has<ReflectionProbe>(entity) ? entity : null;
}

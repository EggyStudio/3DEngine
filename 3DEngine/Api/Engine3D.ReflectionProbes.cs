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
    /// The probe is captured from the meshes the window draws, in the first frame that draws any,
    /// and is ready a frame or two later, once the capture is read back and prefiltered. Until
    /// then surfaces in the box reflect the environment map. It captures again by itself when a
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

    /// <summary>Whether a probe has a capture of where it is now, which surfaces in its box reflect.</summary>
    public static bool IsReflectionProbeReady(ReflectionProbeHandle probe)
    {
        if (Resolve(probe) is not { } entity || !TryRes<ReflectionProbes>(out var probes)
            || !probes.ByEntity.TryGetValue(entity, out var known) || known.Map is null || known.Captured is not { } captured) return false;
        // Against the component as it is now, which the renderer takes up at the end of the frame.
        var wanted = Ecs.GetReadOnly<ReflectionProbe>(entity);
        return captured.Size == wanted.Size && captured.Capture == wanted.Capture && captured == known.Wanted;
    }

    /// <summary>Removes a probe, and its box reflects the environment map again.</summary>
    public static void UnloadReflectionProbe(ReflectionProbeHandle probe)
    {
        if (Resolve(probe) is { } entity) Ecs.Despawn(entity);
    }

    private static int? Resolve(ReflectionProbeHandle probe) =>
        Ecs.TryResolve(probe.Entity, out var entity) && Ecs.Has<ReflectionProbe>(entity) ? entity : null;
}

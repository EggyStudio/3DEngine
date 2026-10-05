using System.Numerics;

namespace Engine;

/// <summary>A particle emitter <c>CreateParticleEmitter</c> made, an entity with a <see cref="ParticleEmitter"/>.</summary>
public readonly record struct ParticleEmitterHandle(Entity Entity)
{
    /// <summary>Whether this names an emitter that was made.</summary>
    public bool IsValid => !Entity.IsNone;
}

public static partial class Engine3D
{
    // -- Particles, which are ParticleEmitter entities in the ECS, so an emitter made here and one
    // a scene places are drawn alike.

    /// <summary>
    /// Makes an emitter of particles at <paramref name="position"/>, giving them off as
    /// <paramref name="emitter"/> says, or as <see cref="ParticleEmitter.Default"/>'s small white
    /// fountain does without one.
    /// </summary>
    /// <remarks>
    /// The particles are simulated on the GPU and drawn into the window through the camera of the
    /// frame's <c>BeginMode3D</c>, or the camera entity, after the frame's models, and into each
    /// render texture drawn into in 3D, through the camera of its first <c>BeginMode3D</c>. A program
    /// needs no drawing call for them, and they keep moving whether or not it draws in 3D that frame.
    /// </remarks>
    public static ParticleEmitterHandle CreateParticleEmitter(Vector3 position, ParticleEmitter? emitter = null)
    {
        var entity = Ecs.Spawn();
        Ecs.Add(entity, emitter ?? ParticleEmitter.Default);
        Ecs.Add(entity, new Transform(position));
        return new ParticleEmitterHandle(Ecs.Handle(entity));
    }

    /// <summary>Moves an emitter, where its next particles start, those already given off going on as they were.</summary>
    public static void SetParticleEmitterPosition(ParticleEmitterHandle emitter, Vector3 position)
    {
        if (Resolve(emitter) is { } entity) Ecs.GetRef<Transform>(entity).Position = position;
    }

    /// <summary>Gives an emitter new settings, which its particles alive take from the next frame.</summary>
    public static void SetParticleEmitter(ParticleEmitterHandle emitter, ParticleEmitter settings)
    {
        if (Resolve(emitter) is { } entity) Ecs.GetRef<ParticleEmitter>(entity) = settings;
    }

    /// <summary>The settings an emitter has, or the default ones for an emitter that is gone.</summary>
    public static ParticleEmitter GetParticleEmitter(ParticleEmitterHandle emitter) =>
        Resolve(emitter) is { } entity ? Ecs.GetReadOnly<ParticleEmitter>(entity) : ParticleEmitter.Default;

    /// <summary>Turns an emitter's stream on or off, the particles alive living out their lives.</summary>
    public static void SetParticleEmitterActive(ParticleEmitterHandle emitter, bool emitting)
    {
        if (Resolve(emitter) is { } entity) Ecs.GetRef<ParticleEmitter>(entity).Emitting = emitting;
    }

    /// <summary>Gives off <paramref name="count"/> particles at once in the next frame, as an explosion or a hit does.</summary>
    public static void EmitParticles(ParticleEmitterHandle emitter, int count)
    {
        if (Resolve(emitter) is { } entity && count > 0) Ecs.GetRef<ParticleEmitter>(entity).Burst += count;
    }

    /// <summary>Removes an emitter and its particles.</summary>
    public static void UnloadParticleEmitter(ParticleEmitterHandle emitter)
    {
        if (Resolve(emitter) is { } entity) Ecs.Despawn(entity);
    }

    private static int? Resolve(ParticleEmitterHandle emitter) =>
        Ecs.TryResolve(emitter.Entity, out var entity) && Ecs.Has<ParticleEmitter>(entity) ? entity : null;
}

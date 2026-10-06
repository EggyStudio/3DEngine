using System.Numerics;

namespace Engine;

/// <summary>A particle emitter <c>CreateParticleEmitter</c> made, an entity with a <see cref="ParticleEmitter"/>.</summary>
public readonly record struct ParticleEmitterHandle(Entity Entity)
{
    /// <summary>Whether this names an emitter that was made.</summary>
    public bool IsValid => !Entity.IsNone;
}

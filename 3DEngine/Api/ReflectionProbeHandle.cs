using System.Numerics;

namespace Engine;

/// <summary>A reflection probe the flat API made, by the entity that holds its <see cref="ReflectionProbe"/>.</summary>
public readonly record struct ReflectionProbeHandle(Entity Entity)
{
    /// <summary>Whether this names a probe that was made.</summary>
    public bool IsValid => !Entity.IsNone;
}

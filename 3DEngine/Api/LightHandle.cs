using System.Numerics;

namespace Engine;

/// <summary>A light the flat API made, by the entity that holds its <see cref="Light"/>.</summary>
public readonly record struct LightHandle(Entity Entity)
{
    /// <summary>Whether this names a light that was made.</summary>
    public bool IsValid => !Entity.IsNone;
}

using System.Numerics;

namespace Engine;

/// <summary>What a <see cref="Light"/> is, which decides what of it the model pass reads.</summary>
public enum LightKind
{
    /// <summary>Light from far away along the entity's forward (-Z), as the sun's, whose position does not matter.</summary>
    Directional,

    /// <summary>Light from the entity's position in every direction, fading with the square of the distance.</summary>
    Point,

    /// <summary>A point light limited to a cone along the entity's forward (-Z).</summary>
    Spot,

    /// <summary>Light from everywhere at once, which lights every surface the same.</summary>
    Ambient,
}

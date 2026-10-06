using System.Numerics;

namespace Engine;

/// <summary>
/// A light read from a model or scene file, carried on a <see cref="SceneNode"/> until
/// <see cref="LightSpawnSystem"/> turns it into a <see cref="Light"/> on the spawned entity.
/// </summary>
/// <remarks>
/// The same fields as <see cref="Light"/>, which the model pass reads. A reader maps what
/// its format has onto them: Assimp's directional, point, spot and ambient lights map directly, and
/// an area light becomes a point light, which is the nearest thing the model pass draws.
/// </remarks>
internal sealed class SceneLightPayload
{
    /// <summary>The light's name in its file, for messages.</summary>
    public string Name { get; init; } = "Light";

    /// <summary>What the light is.</summary>
    public required LightKind Kind { get; init; }

    /// <summary>Linear RGB color, multiplied by <see cref="Intensity"/>.</summary>
    public Vector3 Color { get; init; } = Vector3.One;

    /// <summary>How strong the light is.</summary>
    public float Intensity { get; init; } = 1f;

    /// <summary>The distance a point or spot light fades to nothing at, or 0 for none.</summary>
    public float Range { get; init; }

    /// <summary>A spot's angle from its axis inside which it is full, in degrees.</summary>
    public float InnerAngle { get; init; } = 25f;

    /// <summary>A spot's angle from its axis outside which it gives nothing, in degrees.</summary>
    public float OuterAngle { get; init; } = 30f;

    /// <summary>Whether the light is to cast shadows.</summary>
    public bool CastsShadows { get; init; }
}

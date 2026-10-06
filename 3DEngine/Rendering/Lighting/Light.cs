using System.Numerics;

namespace Engine;

/// <summary>A light, placed and aimed by its entity's <see cref="Transform"/>.</summary>
/// <remarks>
/// <para>
/// The light a surface receives is <see cref="Color"/> times <see cref="Intensity"/>, by Lambert's
/// cosine for a directional light, and divided by the square of the distance for a point or spot
/// light, which is how a light of intensity 1 a meter away reads as much as a directional light of
/// intensity 1. <see cref="Range"/> brings a point or spot light smoothly to nothing at that distance,
/// so it stops costing anything past it, and 0 lets it reach every distance.
/// </para>
/// <para>
/// A spot is full inside <see cref="InnerAngle"/> and nothing outside <see cref="OuterAngle"/>, both
/// measured from its axis, and fades between them.
/// </para>
/// </remarks>
/// <seealso cref="RenderLight"/>
[SceneComponent]
public struct Light
{
    /// <summary>What the light is.</summary>
    public LightKind Kind;

    /// <summary>Linear RGB color, multiplied by <see cref="Intensity"/>.</summary>
    public Vector3 Color;

    /// <summary>How strong the light is.</summary>
    public float Intensity;

    /// <summary>The distance a point or spot light fades to nothing at, or 0 for none.</summary>
    public float Range;

    /// <summary>A spot's angle from its axis inside which it is full, in degrees.</summary>
    public float InnerAngle;

    /// <summary>A spot's angle from its axis outside which it gives nothing, in degrees.</summary>
    public float OuterAngle;

    /// <summary>Whether the light casts shadows, which the first directional light with it set does, over what the window's camera sees.</summary>
    public bool CastsShadows;

    /// <summary>A white point light of intensity 1, which a spot made from it opens to 30 degrees.</summary>
    public static Light Default => new()
    {
        Kind = LightKind.Point,
        Color = Vector3.One,
        Intensity = 1,
        InnerAngle = 25,
        OuterAngle = 30,
    };

    /// <summary>A directional light, as the sun's.</summary>
    public static Light Directional(Vector3 color, float intensity) => Default with { Kind = LightKind.Directional, Color = color, Intensity = intensity };

    /// <summary>A point light reaching <paramref name="range"/>, or every distance with 0.</summary>
    public static Light Point(Vector3 color, float intensity, float range = 0) => Default with { Color = color, Intensity = intensity, Range = range };

    /// <summary>A spot light, full inside <paramref name="innerAngle"/> and gone past <paramref name="outerAngle"/>, in degrees.</summary>
    public static Light Spot(Vector3 color, float intensity, float innerAngle, float outerAngle, float range = 0) =>
        Default with { Kind = LightKind.Spot, Color = color, Intensity = intensity, InnerAngle = innerAngle, OuterAngle = outerAngle, Range = range };

    /// <summary>Light from everywhere, which keeps the side of a model away from every other light from going black.</summary>
    public static Light Ambient(Vector3 color, float intensity) => Default with { Kind = LightKind.Ambient, Color = color, Intensity = intensity };
}

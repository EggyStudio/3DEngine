using System.Numerics;

namespace Engine;

/// <summary>
/// How much of the environment map's light reaches the scene, and its tint, apart from the sky the
/// map draws, a world resource which <see cref="E3D.SetEnvironmentLight"/> sets and the model pass
/// and the global illumination's sky read.
/// </summary>
/// <remarks>
/// A resource of its own rather than the map's, since <see cref="E3D.SetEnvironmentMap(Image, float)"/>
/// replaces the map, and the share a program set for the scene stays through a change of sky.
/// </remarks>
internal sealed class EnvironmentLight
{
    /// <summary>The share of the map's light on the scene, 1 for all of it.</summary>
    public float Intensity { get; set; } = 1;

    /// <summary>The color the map's light on the scene is multiplied by, white for none.</summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>The share times the tint decoded to linear light, which the light is multiplied by.</summary>
    public Vector3 Linear => Intensity * new Vector3(BloomRenderer.SrgbToLinear(Tint.R / 255f), BloomRenderer.SrgbToLinear(Tint.G / 255f),
        BloomRenderer.SrgbToLinear(Tint.B / 255f));

    /// <summary>What the map's light on the scene is multiplied by in <paramref name="renderWorld"/>, white where nothing was set.</summary>
    public static Vector3 In(RenderWorld renderWorld) => renderWorld.TryGet<EnvironmentLight>()?.Linear ?? Vector3.One;
}

namespace Engine;

public static partial class Engine3D
{
    /// <summary>
    /// Lights models from all around by an equirectangular image of sRGB color, a sky photo or a
    /// painted gradient, which smooth and metal surfaces reflect and rough ones scatter.
    /// </summary>
    /// <remarks>
    /// The image is prefiltered by roughness into a cube map with faces 64 texels wide, which takes
    /// a few hundred milliseconds, so it is set when a level starts. With a map set, models are lit
    /// by it and the ECS's light entities, in place of the fixed light. It is not drawn as a
    /// background.
    /// </remarks>
    /// <exception cref="ArgumentException">The image is empty.</exception>
    public static void SetEnvironmentMap(Image equirectangular, float intensity = 1) =>
        World.InsertResource(EnvironmentMap.FromEquirectangular(equirectangular, intensity));

    /// <summary>Stops lighting models by an environment map.</summary>
    public static void UnloadEnvironmentMap() => World.RemoveResource<EnvironmentMap>();
}

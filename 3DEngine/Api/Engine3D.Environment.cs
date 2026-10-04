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

    /// <summary>
    /// Lights models from all around by an equirectangular image file. A Radiance <c>.hdr</c> keeps
    /// light past white, so a sun outshines its sky, and any other image is read as sRGB color.
    /// </summary>
    /// <returns>Whether the file was read, with the reason in the log when not.</returns>
    public static bool SetEnvironmentMap(string fileName, float intensity = 1)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"SetEnvironmentMap: '{fileName}' was not found beside the program or in the working directory.");
            return false;
        }

        try
        {
            if (Path.GetExtension(path).Equals(".hdr", StringComparison.OrdinalIgnoreCase))
                World.InsertResource(EnvironmentMap.FromHdrFile(File.ReadAllBytes(path), intensity));
            else
                World.InsertResource(EnvironmentMap.FromEquirectangular(LoadImage(path), intensity));
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            ApiLogger.Warn($"SetEnvironmentMap: '{fileName}' could not be read: {ex.Message}");
            return false;
        }
    }

    /// <summary>Stops lighting models by an environment map.</summary>
    public static void UnloadEnvironmentMap() => World.RemoveResource<EnvironmentMap>();
}

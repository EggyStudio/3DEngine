using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    /// <summary>
    /// Lights models from all around by an equirectangular image of sRGB color, a sky photo or a
    /// painted gradient, which smooth and metal surfaces reflect and rough ones scatter.
    /// </summary>
    /// <remarks>
    /// The image is decoded here, and the next frame uploads it and filters it by roughness on the
    /// GPU into a cube map with faces 64 texels wide, so a map can change while a level runs. With a map set, models are lit by it and the ECS's
    /// light entities, in place of the fixed light. <see cref="DrawSkybox()"/> draws it as a
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

    private static ModelMesh _skyMesh;
    private static Shader _skyShader;
    private static bool _skyShaderFailed;

    /// <summary>
    /// Draws the environment map as the sky behind everything, through the camera of the
    /// <see cref="BeginMode3D"/> it is called inside. With no map set it draws nothing.
    /// </summary>
    /// <remarks>
    /// The sky is read from a copy of the map's image at about its own resolution, not the blurred
    /// cube that lights models, and goes through the same tonemap, so a metal's reflection matches
    /// the sky around it. <paramref name="tint"/> multiplies it, to dim it at dusk. It casts no
    /// shadow and hides <see cref="ClearBackground"/>'s color.
    /// </remarks>
    public static void DrawSkybox(Color tint)
    {
        if (_camera3D is not { } camera || !World.TryGetResource<EnvironmentMap>(out _) || SkyShader() is not { IsValid: true } shader) return;
        if (!_skyMesh.IsValid) _skyMesh = GenMeshCube(2, 2, 2);
        // Far enough out that no side is cut by the near plane, and well inside the far one.
        var world = Matrix4x4.CreateScale(100) * Matrix4x4.CreateTranslation(camera.Position);
        Res<ModelDrawList>().Add(new ModelDraw(_skyMesh.Id, world, DrawList.Transform, tint, 0, DrawList.Target, shader.Id,
            AlphaMode: MaterialAlphaMode.Opaque, CastsShadow: false));
    }

    /// <summary>Draws the environment map as the sky behind everything, untinted.</summary>
    public static void DrawSkybox() => DrawSkybox(Color.White);

    // The sky's model shader, compiled once per window from the staged sky.slang.
    private static Shader SkyShader()
    {
        if (_skyShader.IsValid || _skyShaderFailed) return _skyShader;
        var path = Path.Combine(AppContext.BaseDirectory, "source", "shaders", "sky.slang");
        _skyShader = File.Exists(path) ? LoadShaderFromMemory(File.ReadAllText(path), "sky.slang") : default;
        if (!_skyShader.IsValid)
        {
            _skyShaderFailed = true;
            ApiLogger.Warn("DrawSkybox: the sky shader is not available, so no sky is drawn.");
        }
        return _skyShader;
    }

    // The sky's mesh and shader went with the window's stores.
    private static void ForgetSky()
    {
        _skyMesh = default;
        _skyShader = default;
        _skyShaderFailed = false;
        _camera3D = null;
    }
}

using System.Numerics;

namespace Engine;

public static partial class E3D
{
    /// <summary>
    /// Lights models from all around by an equirectangular image of sRGB color, a sky photo or a
    /// painted gradient, which smooth and metal surfaces reflect and rough ones scatter.
    /// </summary>
    /// <remarks>
    /// The image is decoded here, and the next frame uploads it and filters it by roughness on the
    /// GPU into a cube map with faces 64 texels wide, so a map can change while a level runs. With a
    /// map set, models are lit by it and the ECS's light entities, where with neither they are
    /// drawn unlit. <see cref="DrawSkybox()"/> draws it as a background. A map set here takes the
    /// place of the day sky <see cref="SetSkyTime"/> starts, ending it.
    /// </remarks>
    /// <exception cref="ArgumentException">The image is empty.</exception>
    public static void SetEnvironmentMap(Image equirectangular, float intensity = 1)
    {
        World.InsertResource(EnvironmentMap.FromEquirectangular(equirectangular, intensity));
        EndDaySky();
    }

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
            EndDaySky();
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            ApiLogger.Warn($"SetEnvironmentMap: '{fileName}' could not be read. {ex.Message}");
            return false;
        }
    }

    /// <summary>Stops lighting models by an environment map, and ends the day sky where it runs.</summary>
    public static void UnloadEnvironmentMap()
    {
        World.RemoveResource<EnvironmentMap>();
        EndDaySky();
    }

    /// <summary>
    /// Sets how much of the environment map's light reaches the scene, <paramref name="intensity"/>
    /// of it times <paramref name="tint"/>, apart from the sky the map draws, 1 and white unless set.
    /// </summary>
    /// <remarks>
    /// <see cref="SetEnvironmentMap(Image, float)"/>'s intensity scales the map's light and the sky
    /// <see cref="DrawSkybox()"/> draws alike, and this the light alone, so a scene takes a third of
    /// its sky's light under a sky drawn as bright as ever, or a whiter light than the sky. The
    /// share and tint stay through a change of map. They scale the map's whole light on a surface,
    /// what a rough one scatters and a smooth one reflects, the global illumination's sky and what
    /// a reflection probe captures, so a mirror reflects the map at the share where its reflection
    /// is not traced through the window, and the drawn sky where it is.
    /// </remarks>
    public static void SetEnvironmentLight(float intensity, Color tint)
    {
        var light = World.GetOrInsertResource(static () => new EnvironmentLight());
        light.Intensity = Math.Max(0, intensity);
        light.Tint = tint;
    }

    /// <summary>The share and tint of the environment map's light on the scene as <see cref="SetEnvironmentLight"/> left them, 1 and white unless set.</summary>
    public static (float Intensity, Color Tint) GetEnvironmentLight() =>
        TryRes<EnvironmentLight>(out var light) ? (light.Intensity, light.Tint) : (1, Color.White);

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
        // The horizon as SetSkyHorizon left it, 1 in the color's w where there is one, written each
        // draw, so a program that changes it between two cameras draws each sky with its own.
        var horizon = TryRes<SkyHorizon>(out var set) ? set : null;
        SetShaderValue(shader, GetShaderLocation(shader, "horizonColor"),
            horizon is null ? Vector4.Zero : new Vector4(horizon.Color.R / 255f, horizon.Color.G / 255f, horizon.Color.B / 255f, 1));
        SetShaderValue(shader, GetShaderLocation(shader, "horizonBlend"), horizon?.BlendDegrees ?? 0);
        Res<ModelDrawList>().Add(new ModelDraw(_skyMesh.Id, world, DrawList.Transform, tint, 0, DrawList.Target, shader.Id,
            UniformSnapshot(shader), AlphaMode: MaterialAlphaMode.Opaque, CastsShadow: false));
    }

    /// <summary>Draws the environment map as the sky behind everything, untinted.</summary>
    public static void DrawSkybox() => DrawSkybox(Color.White);

    /// <summary>
    /// Draws the sky below the horizon in <paramref name="color"/>, blended into the environment
    /// map's image over <paramref name="blendDegrees"/> above it, so land a program fades into fog
    /// of that color meets the same color past the last thing drawn. A clear color draws the image
    /// as it is again.
    /// </summary>
    /// <remarks>
    /// It changes the sky <see cref="DrawSkybox()"/> draws and nothing else, so the image's lower half
    /// still lights the scene from below through the map. The color is drawn as given, apart from
    /// <see cref="DrawSkybox(Color)"/>'s tint and the map's intensity, which scale the image alone,
    /// so it matches the color the program's own fog fades into. A blend of 0 meets the image at a
    /// sharp edge, and a few degrees leave no edge where they meet. It stays through a change of map.
    /// </remarks>
    public static void SetSkyHorizon(Color color, float blendDegrees)
    {
        if (color.A == 0)
        {
            World.RemoveResource<SkyHorizon>();
            return;
        }
        var horizon = World.GetOrInsertResource(static () => new SkyHorizon());
        horizon.Color = color;
        horizon.BlendDegrees = float.IsFinite(blendDegrees) ? Math.Clamp(blendDegrees, 0, 90) : 0;
    }

    /// <summary>
    /// The color the sky is drawn in below the horizon and the degrees it blends over, as
    /// <see cref="SetSkyHorizon"/> or the day sky left them, or a clear color and 0 where none is set.
    /// </summary>
    /// <remarks>A program's own fog reads it to fade its land into the color the day sky draws at the hour.</remarks>
    public static (Color Color, float BlendDegrees) GetSkyHorizon() =>
        TryRes<SkyHorizon>(out var horizon) ? (horizon.Color, horizon.BlendDegrees) : (Color.Blank, 0);

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

using System.Numerics;

namespace Engine;

/// <summary>A custom shader for the immediate pass, by its id in the <see cref="ShaderStore"/>.</summary>
/// <remarks>A default shader has id 0, and drawing inside <see cref="Engine3D.BeginShaderMode"/> with it uses the engine's own.</remarks>
public readonly record struct Shader(int Id)
{
    /// <summary>Whether this names a shader that was loaded.</summary>
    public bool IsValid => Id > 0;
}

public static partial class Engine3D
{
    private static readonly Dictionary<int, ShaderParams> ShaderValues = [];
    private static Shader _shader;

    // -- Custom shaders. A shader is a Slang file that imports the engine's module and defines
    // fragmentMain, and vertexMain when it moves vertices itself:
    //
    //     import engine;
    //     [shader("fragment")]
    //     float4 fragmentMain(VertexOutput input) : SV_Target { ... param(0) ... }
    //
    // It applies to shapes, textures and text drawn between BeginShaderMode and EndShaderMode.

    /// <summary>Loads and compiles a Slang shader file.</summary>
    /// <returns>The shader, or an invalid one when the file cannot be read or compiled, with the reason in the log.</returns>
    public static Shader LoadShader(string fileName)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadShader: '{fileName}' was not found beside the program or in the working directory.");
            return default;
        }
        return LoadShaderFromMemory(File.ReadAllText(path), Path.GetFileName(path));
    }

    /// <summary>Compiles Slang source held in memory as a shader.</summary>
    /// <returns>The shader, or an invalid one when the source does not compile, with the reason in the log.</returns>
    public static Shader LoadShaderFromMemory(string code, string name = "shader.slang")
    {
        try
        {
            var program = new SlangLoader().Compile(code, name);
            return new Shader(World.Resource<ShaderStore>().Add(program));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            ApiLogger.Warn($"LoadShader: '{name}' did not compile: {ex.Message}");
            return default;
        }
    }

    /// <summary>Whether <paramref name="shader"/> is loaded.</summary>
    public static bool IsShaderValid(Shader shader) => shader.IsValid && World.Resource<ShaderStore>().Get(shader.Id) is not null;

    /// <summary>Frees a shader. Drawing with it afterward uses the engine's own.</summary>
    public static void UnloadShader(Shader shader)
    {
        if (!shader.IsValid) return;
        World.Resource<ShaderStore>().Remove(shader.Id);
        ShaderValues.Remove(shader.Id);
    }

    /// <summary>Draws the following shapes, textures and text with <paramref name="shader"/> until <see cref="EndShaderMode"/>.</summary>
    public static void BeginShaderMode(Shader shader)
    {
        _shader = shader;
        DrawList.SetShader(shader.Id, ShaderValues.GetValueOrDefault(shader.Id));
    }

    /// <summary>Returns to the engine's own shader.</summary>
    public static void EndShaderMode()
    {
        _shader = default;
        DrawList.SetShader(0, default);
    }

    /// <summary>Sets slot <paramref name="slot"/> (0 to 3), which the shader reads as <c>param(slot)</c>.</summary>
    /// <remarks>A value set inside the shader's mode applies to what is drawn after it, so a frame can draw with several values.</remarks>
    public static void SetShaderValue(Shader shader, int slot, Vector4 value)
    {
        if (!shader.IsValid || slot is < 0 or > 3) return;
        var values = ShaderValues.GetValueOrDefault(shader.Id).With(slot, value);
        ShaderValues[shader.Id] = values;
        if (_shader == shader) DrawList.SetShader(shader.Id, values);
    }

    /// <summary>Sets slot <paramref name="slot"/> to (<paramref name="value"/>, 0, 0, 0).</summary>
    public static void SetShaderValue(Shader shader, int slot, float value) => SetShaderValue(shader, slot, new Vector4(value, 0, 0, 0));

    /// <summary>Sets slot <paramref name="slot"/> to (x, y, 0, 0).</summary>
    public static void SetShaderValue(Shader shader, int slot, Vector2 value) => SetShaderValue(shader, slot, new Vector4(value, 0, 0));

    /// <summary>Sets slot <paramref name="slot"/> to (x, y, z, 0).</summary>
    public static void SetShaderValue(Shader shader, int slot, Vector3 value) => SetShaderValue(shader, slot, new Vector4(value, 0));
}

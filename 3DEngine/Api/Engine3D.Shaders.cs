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

    // The values of each shader's named uniforms, as the bytes of its constant buffer.
    private static readonly Dictionary<int, byte[]> UniformValues = [];

    // Locations of named uniforms start here, past the immediate pass's four slots, so one
    // SetShaderValue serves both, and those of textures further on.
    private const int NamedLocationBase = 16;
    private const int TextureLocationBase = 1 << 20;

    // The texture each of a shader's textures is set to, by its index in ShaderProgram.Textures, 0 for none.
    private static readonly Dictionary<int, int[]> TextureValues = [];
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
            return new Shader(Res<ShaderStore>().Add(program));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            ApiLogger.Warn($"LoadShader: '{name}' did not compile: {ex.Message}");
            return default;
        }
    }

    /// <summary>Whether <paramref name="shader"/> is loaded.</summary>
    public static bool IsShaderValid(Shader shader) => shader.IsValid && Res<ShaderStore>().Get(shader.Id) is not null;

    /// <summary>Frees a shader. Drawing with it afterward uses the engine's own.</summary>
    public static void UnloadShader(Shader shader)
    {
        if (!shader.IsValid) return;
        Res<ShaderStore>().Remove(shader.Id);
        ShaderValues.Remove(shader.Id);
        UniformValues.Remove(shader.Id);
        TextureValues.Remove(shader.Id);
        ForgetComputeShader(shader.Id);
    }

    /// <summary>Draws the following shapes, textures and text with <paramref name="shader"/> until <see cref="EndShaderMode"/>.</summary>
    public static void BeginShaderMode(Shader shader)
    {
        if (!Draws(shader))
        {
            ApiLogger.Warn("BeginShaderMode: a compute shader draws nothing, so the engine's own shader is used.");
            shader = default;
        }
        _shader = shader;
        DrawList.SetShader(shader.Id, ShaderValues.GetValueOrDefault(shader.Id), UniformSnapshot(shader), TextureSnapshot(shader));
    }

    // Whether a shader has a fragment stage to draw with, which a compute shader has not.
    private static bool Draws(Shader shader) =>
        !shader.IsValid || Res<ShaderStore>().Get(shader.Id)?.Stages.ContainsKey(ShaderStage.Fragment) != false;

    /// <summary>Returns to the engine's own shader.</summary>
    public static void EndShaderMode()
    {
        _shader = default;
        DrawList.SetShader(0, default);
    }

    /// <summary>
    /// The location of a uniform the shader declares at the top level, by name, for
    /// <see cref="SetShaderValue(Shader, int, Vector4)"/>, or -1 when it declares none of that name.
    /// A texture's is for <see cref="SetShaderValueTexture"/> and a compute shader's storage
    /// buffer's for <see cref="SetShaderValueBuffer"/>.
    /// </summary>
    /// <remarks>
    /// Both a model shader's and an immediate shader's uniforms are found this way. An immediate
    /// shader can read the four slots 0 to 3 through <c>param(slot)</c> as well.
    /// </remarks>
    public static int GetShaderLocation(Shader shader, string uniformName)
    {
        if (!shader.IsValid || Res<ShaderStore>().Get(shader.Id) is not { } program) return -1;
        for (int i = 0; i < program.Uniforms.Count; i++)
            if (program.Uniforms[i].Name == uniformName) return NamedLocationBase + i;
        for (int i = 0; i < program.Textures.Count; i++)
            if (program.Textures[i].Name == uniformName) return TextureLocationBase + i;
        for (int i = 0; i < program.Buffers.Count; i++)
            if (program.Buffers[i].Name == uniformName) return BufferLocationBase + i;
        return -1;
    }

    /// <summary>
    /// Sets a texture the shader samples, found by name with <see cref="GetShaderLocation"/>, as
    /// raylib's <c>SetShaderValueTexture</c>, for what is drawn after it.
    /// </summary>
    /// <remarks>
    /// A texture the shader declares at the top level, as <c>Sampler2D detail;</c>, is one of its
    /// own. The pass's own texture (<c>boundTexture</c> and a model's maps) is set by what is drawn.
    /// </remarks>
    public static void SetShaderValueTexture(Shader shader, int location, Texture2D texture)
    {
        if (!shader.IsValid || Res<ShaderStore>().Get(shader.Id) is not { } program) return;
        var index = location - TextureLocationBase;
        if (index < 0 || index >= program.Textures.Count) return;
        if (!TextureValues.TryGetValue(shader.Id, out var values)) TextureValues[shader.Id] = values = new int[program.Textures.Count];
        values[index] = texture.IsValid ? texture.Id : 0;
        if (_shader == shader) DrawList.SetShader(shader.Id, ShaderValues.GetValueOrDefault(shader.Id), UniformSnapshot(shader), TextureSnapshot(shader));
    }

    // A copy of a shader's textures for one draw, so textures set after it reach only later draws.
    private static int[]? TextureSnapshot(Shader shader) =>
        TextureValues.TryGetValue(shader.Id, out var values) ? (int[])values.Clone() : null;

    /// <summary>Sets a named uniform to a 4x4 matrix, as raylib's <c>SetShaderValueMatrix</c>.</summary>
    public static void SetShaderValueMatrix(Shader shader, int location, Matrix4x4 value) =>
        WriteUniform(shader, location, System.Runtime.InteropServices.MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in value)));

    /// <summary>Sets a named uniform to a whole number, or slot (0 to 3) to (<paramref name="value"/>, 0, 0, 0).</summary>
    public static void SetShaderValue(Shader shader, int location, int value)
    {
        if (location >= NamedLocationBase) WriteUniform(shader, location, System.Runtime.InteropServices.MemoryMarshal.AsBytes(new ReadOnlySpan<int>(in value)));
        else SetShaderValue(shader, location, new Vector4(value, 0, 0, 0));
    }

    // Writes a value's bytes into a named uniform, as many as the uniform holds, so a float written
    // into a float stays out of the field after it.
    private static void WriteUniform(Shader shader, int location, ReadOnlySpan<byte> value)
    {
        if (!shader.IsValid || Res<ShaderStore>().Get(shader.Id) is not { } program) return;
        var index = location - NamedLocationBase;
        if (index < 0 || index >= program.Uniforms.Count) return;
        var uniform = program.Uniforms[index];
        if (!UniformValues.TryGetValue(shader.Id, out var block)) UniformValues[shader.Id] = block = new byte[program.UniformSize];
        value[..Math.Min(value.Length, uniform.Size)].CopyTo(block.AsSpan(uniform.Offset));
        // Inside the shader's mode, what is drawn after takes the new values.
        if (_shader == shader) DrawList.SetShader(shader.Id, ShaderValues.GetValueOrDefault(shader.Id), UniformSnapshot(shader), TextureSnapshot(shader));
    }

    // A copy of a shader's uniform values for one draw, so values set after it reach only later draws.
    private static byte[]? UniformSnapshot(Shader shader) =>
        UniformValues.TryGetValue(shader.Id, out var block) ? (byte[])block.Clone() : null;

    /// <summary>
    /// Sets a named uniform found with <see cref="GetShaderLocation"/>, or slot <paramref name="location"/>
    /// (0 to 3), which an immediate shader reads as <c>param(slot)</c>.
    /// </summary>
    /// <remarks>
    /// A value applies to what is drawn after it, so a frame can draw with several values. A named
    /// uniform takes as many of the value's bytes as it holds, so a <c>float</c> takes the first.
    /// </remarks>
    public static void SetShaderValue(Shader shader, int slot, Vector4 value)
    {
        if (slot >= NamedLocationBase)
        {
            WriteUniform(shader, slot, System.Runtime.InteropServices.MemoryMarshal.AsBytes(new ReadOnlySpan<Vector4>(in value)));
            return;
        }
        if (!shader.IsValid || slot is < 0 or > 3) return;
        var values = ShaderValues.GetValueOrDefault(shader.Id).With(slot, value);
        ShaderValues[shader.Id] = values;
        if (_shader == shader) DrawList.SetShader(shader.Id, values, UniformSnapshot(shader), TextureSnapshot(shader));
    }

    /// <summary>Sets slot <paramref name="slot"/> to (<paramref name="value"/>, 0, 0, 0).</summary>
    public static void SetShaderValue(Shader shader, int slot, float value) => SetShaderValue(shader, slot, new Vector4(value, 0, 0, 0));

    /// <summary>Sets slot <paramref name="slot"/> to (x, y, 0, 0).</summary>
    public static void SetShaderValue(Shader shader, int slot, Vector2 value) => SetShaderValue(shader, slot, new Vector4(value, 0, 0));

    /// <summary>Sets slot <paramref name="slot"/> to (x, y, z, 0).</summary>
    public static void SetShaderValue(Shader shader, int slot, Vector3 value) => SetShaderValue(shader, slot, new Vector4(value, 0));
}

using Vortice.ShaderCompiler;

namespace Engine;

/// <summary>
/// Provides in-process GLSL → SPIR-V compilation using the shaderc library.
/// No external tools (glslc) required. Safe for runtime use, editor hot-reload, and published builds.
/// </summary>
/// <remarks>
/// <para>
/// A new <see cref="Compiler"/> instance is created per <see cref="Compile"/> call because
/// the shaderc compiler is not thread-safe.  For typical shader loading this is negligible;
/// callers needing high-throughput compilation can pool externally.
/// </para>
/// </remarks>
/// <seealso cref="ShaderStage"/>
public static class GlslCompiler
{
    private static readonly ILogger Logger = Log.Category("Engine.GlslCompiler");

    // Thread-safe: Compiler is created per call (shaderc compiler is not thread-safe).
    // For high-frequency use the caller can pool, but for shader loading this is fine.

    /// <summary>
    /// Compiles a GLSL source string to SPIR-V bytecode.
    /// </summary>
    /// <param name="glslSource">The GLSL source code.</param>
    /// <param name="fileName">A display name for error messages (e.g. "webview.vert.glsl").</param>
    /// <param name="stage">The shader stage (Vertex, Fragment).</param>
    /// <returns>The compiled SPIR-V bytecode.</returns>
    /// <exception cref="InvalidOperationException">Thrown when compilation fails.</exception>
    public static byte[] Compile(string glslSource, string fileName, ShaderStage stage)
    {
        using var compiler = new Compiler();
        var options = new CompilerOptions
        {
            SourceLanguage = SourceLanguage.GLSL,
            TargetEnv = TargetEnvironmentVersion.Vulkan_1_0,
            OptimizationLevel = OptimizationLevel.Performance,
            ShaderStage = stage switch
            {
                ShaderStage.Vertex => ShaderKind.VertexShader,
                ShaderStage.Fragment => ShaderKind.FragmentShader,
                _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported shader stage.")
            }
        };

        Logger.Debug($"Compiling GLSL → SPIR-V: {fileName} (stage={stage})...");

        var result = compiler.Compile(glslSource, fileName, options);

        if (result.Status != CompilationStatus.Success)
        {
            var errorMsg = result.ErrorMessage ?? "Unknown compilation error.";
            Logger.Error($"GLSL compilation failed for {fileName}: {errorMsg}");
            throw new InvalidOperationException($"GLSL compilation failed for '{fileName}': {errorMsg}");
        }

        var bytecode = result.Bytecode;
        Logger.Debug($"GLSL compiled successfully: {fileName} → {bytecode.Length} bytes SPIR-V.");
        return bytecode;
    }

    /// <summary>
    /// Compiles a GLSL file to SPIR-V bytecode. Reads the file from disk and compiles it.
    /// </summary>
    /// <param name="glslPath">Absolute path to the .glsl file.</param>
    /// <param name="stage">The shader stage (Vertex, Fragment).</param>
    /// <returns>The compiled SPIR-V bytecode.</returns>
    public static byte[] CompileFile(string glslPath, ShaderStage stage)
    {
        if (!File.Exists(glslPath))
            throw new FileNotFoundException($"GLSL source file not found: {glslPath}", glslPath);

        var source = File.ReadAllText(glslPath);
        return Compile(source, Path.GetFileName(glslPath), stage);
    }
}

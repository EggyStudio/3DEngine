using System.Text.RegularExpressions;

namespace Engine;

/// <summary>
/// Asset loader that compiles a <c>.slang</c> file into a <see cref="ShaderProgram"/>, one stage
/// per function marked <c>[shader("vertex")]</c> or <c>[shader("fragment")]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Compiled stages are cached in <see cref="CacheDirectory"/> (see <see cref="SlangCompiler"/>), and
/// <c>import</c> statements resolve against <see cref="ImportDirectory"/>. Both default to folders
/// under <c>source/</c> beside the running program, where the built-in shaders are staged.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// server.RegisterLoader(new SlangLoader());
/// var mesh = server.LoadSync&lt;ShaderProgram&gt;("shaders/model.slang");
/// var node = new MainPassNode(mesh.Vertex, mesh.Fragment);
/// </code>
/// </example>
/// <seealso cref="SlangCompiler"/>
/// <seealso cref="ShaderProgram"/>
internal sealed partial class SlangLoader : IAssetLoader<ShaderProgram>
{
    /// <summary>Creates a loader.</summary>
    /// <param name="cacheDirectory">Where compiled SPIR-V is kept. Defaults to <c>source/.slang-cache</c>.</param>
    /// <param name="importDirectory">Where imports resolve. Defaults to <c>source/shaders</c>.</param>
    public SlangLoader(string? cacheDirectory = null, string? importDirectory = null)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "source");
        CacheDirectory = cacheDirectory ?? Path.Combine(root, ".slang-cache");
        ImportDirectory = importDirectory ?? Path.Combine(root, "shaders");
    }

    /// <summary>Where compiled SPIR-V is kept.</summary>
    public string CacheDirectory { get; }

    /// <summary>Where <c>import</c> statements resolve.</summary>
    public string ImportDirectory { get; }

    /// <inheritdoc />
    public string[] Extensions => [".slang"];

    /// <inheritdoc />
    public async Task<AssetLoadResult<ShaderProgram>> LoadAsync(AssetLoadContext context, CancellationToken ct)
    {
        var source = await context.ReadAllTextAsync(ct);
        var fileName = context.Path.FileName;

        try
        {
            var program = Compile(source, fileName);
            return AssetLoadResult<ShaderProgram>.Ok(program);
        }
        catch (InvalidOperationException ex)
        {
            return AssetLoadResult<ShaderProgram>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// The names a built-in shader is compiled with a second time where it mentions one, each for a
    /// capability some devices lack: <c>RAY_QUERY</c> for the GPU's own ray tracing.
    /// </summary>
    internal static readonly string[] Variants = ["RAY_QUERY"];

    /// <summary>Compiles every entry point in <paramref name="source"/>.</summary>
    /// <param name="source">The Slang source.</param>
    /// <param name="fileName">The file's name, for messages and cache entries.</param>
    /// <param name="defines">Names defined for the source and every module it imports, none by default.</param>
    /// <returns>The compiled program.</returns>
    /// <exception cref="InvalidOperationException">The source has no entry points, or one failed to compile.</exception>
    public ShaderProgram Compile(string source, string fileName, IReadOnlyList<string>? defines = null)
    {
        var stages = new Dictionary<ShaderStage, byte[]>();
        var uniforms = new Dictionary<string, ShaderUniform>();
        var textures = new Dictionary<string, ShaderTexture>();
        var buffers = new Dictionary<string, ShaderTexture>();
        var images = new Dictionary<string, ShaderTexture>();
        var bindings = new Dictionary<(int Set, int Binding), (ShaderBinding Binding, ShaderStageFlags Stages)>();
        foreach (var (entryPoint, stage) in EntryPoints(source))
        {
            var compiled = SlangCompiler.CompileStage(source, fileName, entryPoint, stage, CacheDirectory, ImportDirectory,
                SlangCompiler.CompilerPath, defines ?? []);
            stages[stage] = compiled.Spirv;
            // Both stages see the same top-level uniforms, laid out the same.
            foreach (var uniform in compiled.Uniforms) uniforms[uniform.Name] = uniform;
            foreach (var texture in compiled.Textures ?? []) textures[texture.Name] = texture;
            foreach (var buffer in compiled.Buffers ?? []) buffers[buffer.Name] = buffer;
            foreach (var image in compiled.Images ?? []) images[image.Name] = image;
            // A descriptor both stages declare is read by both, so its layout names each.
            var flag = stage switch { ShaderStage.Vertex => ShaderStageFlags.Vertex, ShaderStage.Fragment => ShaderStageFlags.Fragment, _ => default };
            foreach (var binding in compiled.Bindings ?? [])
                bindings[(binding.Set, binding.Binding)] = bindings.TryGetValue((binding.Set, binding.Binding), out var seen)
                    ? (seen.Binding, seen.Stages | flag)
                    : (binding, flag);
        }

        if (stages.Count == 0)
            throw new InvalidOperationException(
                $"'{fileName}' has no function marked [shader(\"vertex\")], [shader(\"fragment\")] or [shader(\"compute\")].");

        return new ShaderProgram(fileName, stages, [.. uniforms.Values.OrderBy(u => u.Offset)], [.. textures.Values.OrderBy(t => t.Binding)],
            [.. buffers.Values.OrderBy(b => b.Binding)], [.. images.Values.OrderBy(i => i.Binding)], [.. bindings.Values]);
    }

    /// <summary>
    /// Compiles every entry point of every shader in <paramref name="shaderDirectory"/> into
    /// <paramref name="cacheDirectory"/>, so a program that ships the cache beside those shaders
    /// loads them with no compiler. Files with no entry point, such as a module others import,
    /// are passed over.
    /// </summary>
    /// <remarks>
    /// A cache entry is keyed by the shader's source and every shader in the folder it imports
    /// from, so the cache serves a program whose shader folder holds the same files.
    /// </remarks>
    /// <returns>The names of the shaders compiled.</returns>
    /// <exception cref="InvalidOperationException">A shader failed to compile, or there is no compiler.</exception>
    public static IReadOnlyList<string> Precompile(string shaderDirectory, string cacheDirectory)
    {
        var loader = new SlangLoader(cacheDirectory, shaderDirectory);
        var compiled = new List<string>();
        foreach (var file in Directory.GetFiles(shaderDirectory, "*.slang").Order(StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file);
            if (!EntryPoints(source).Any()) continue;
            loader.Compile(source, Path.GetFileName(file));
            // And its build for each capability it or a module it imports names, which a device
            // that has the capability loads in its place.
            foreach (var variant in VariantsOf(source, shaderDirectory))
                loader.Compile(source, Path.GetFileName(file), [variant]);
            compiled.Add(Path.GetFileName(file));
        }
        return compiled;
    }

    /// <summary>The names of <see cref="Variants"/> that <paramref name="source"/> or a module it imports from <paramref name="importDirectory"/> mentions.</summary>
    internal static IEnumerable<string> VariantsOf(string source, string importDirectory) =>
        Variants.Where(name => source.Contains(name, StringComparison.Ordinal)
            || SlangCompiler.ImportedFiles(source, importDirectory).Any(file => file.Bytes is { } bytes
                && System.Text.Encoding.UTF8.GetString(bytes).Contains(name, StringComparison.Ordinal)));

    /// <summary>Finds the functions marked with a stage attribute, in the order they appear.</summary>
    /// <remarks>
    /// A pattern rather than a parse, because the attribute and the function name are all the
    /// loader needs, and <c>slangc</c> reports anything malformed when it compiles the entry point.
    /// </remarks>
    internal static IEnumerable<(string EntryPoint, ShaderStage Stage)> EntryPoints(string source)
    {
        foreach (Match match in EntryPointPattern().Matches(source))
        {
            var stage = match.Groups["stage"].Value switch
            {
                "vertex" => ShaderStage.Vertex,
                "fragment" or "pixel" => ShaderStage.Fragment,
                "compute" => ShaderStage.Compute,
                _ => (ShaderStage?)null,
            };
            if (stage is not null)
                yield return (match.Groups["name"].Value, stage.Value);
        }
    }

    [GeneratedRegex("""\[shader\("(?<stage>\w+)"\)\]\s*(?:\[[^\]]*\]\s*)*[\w<>,\s]+?\s(?<name>\w+)\s*\(""")]
    private static partial Regex EntryPointPattern();
}

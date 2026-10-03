using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Engine;

/// <summary>
/// Compiles Slang source to SPIR-V by running <c>slangc</c>, and caches every result on disk so a
/// machine without the compiler can still load the shaders it was given.
/// </summary>
/// <remarks>
/// <para>
/// <c>slangc</c> is a tool rather than a linked library. Linking Slang would put tens of megabytes
/// of native code into every game for the sake of compiling while it runs, and a process does the
/// same with nothing shipped. <c>build/fetch-slang.sh</c> downloads a pinned release into
/// <c>build/tools/slang</c>.
/// </para>
/// <para>
/// The compiler is looked for in <c>ENGINE_SLANGC</c>, then on the <c>PATH</c>, then at
/// <c>build/tools/slang/bin</c> in every directory above the running program and above the
/// working directory, so an example run from the repository finds the checkout's copy.
/// </para>
/// <para>
/// A cache entry is keyed by a hash of the source, the entry point, the stage, the compiler
/// arguments, and every file the source imports or includes from the import directory, followed
/// through their own imports, each with its path written with forward slashes. Keying by what is
/// imported rather than by the whole folder lets a shipped cache serve a program that adds shaders
/// of its own beside the built-in ones, and the slashes let a cache made on one system serve
/// another.
/// </para>
/// </remarks>
public static partial class SlangCompiler
{
    private static readonly ILogger Logger = Log.Category("Engine.Slang");

    // Part of every cache key, so a change to how shaders are compiled never reads an entry
    // compiled the other way.
    private const string Arguments = "-target spirv -matrix-layout-column-major";

    private static readonly Lazy<string?> Located = new(Locate);

    /// <summary>The <c>slangc</c> this process compiles with, or <c>null</c> when none was found.</summary>
    public static string? CompilerPath => Located.Value;

    /// <summary>Whether a compiler was found, so shader edits can be compiled.</summary>
    /// <remarks>Without one, only shaders already in a cache can be loaded.</remarks>
    public static bool Available => CompilerPath is not null;

    /// <summary>Compiles one entry point of a Slang source to SPIR-V, or reads it from the cache.</summary>
    /// <param name="source">The Slang source.</param>
    /// <param name="fileName">The file's name, used for messages and for the cache entry's name.</param>
    /// <param name="entryPoint">The function to compile, such as <c>vertexMain</c>.</param>
    /// <param name="stage">The stage that function runs in.</param>
    /// <param name="cacheDirectory">Where compiled SPIR-V is kept, or <c>null</c> to keep nothing.</param>
    /// <param name="importDirectory">The directory <c>import</c> statements resolve against, or <c>null</c>.</param>
    /// <returns>SPIR-V whose entry point is named <c>main</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// The cache has no entry for this source and there is no compiler, or the compiler reported an error.
    /// </exception>
    public static byte[] Compile(string source, string fileName, string entryPoint, ShaderStage stage,
        string? cacheDirectory = null, string? importDirectory = null) =>
        CompileStage(source, fileName, entryPoint, stage, cacheDirectory, importDirectory, CompilerPath).Spirv;

    internal static byte[] Compile(string source, string fileName, string entryPoint, ShaderStage stage,
        string? cacheDirectory, string? importDirectory, string? compiler) =>
        CompileStage(source, fileName, entryPoint, stage, cacheDirectory, importDirectory, compiler).Spirv;

    /// <summary>
    /// Compiles one entry point, or reads it from the cache, with the uniforms it declares at the
    /// top level, which a program sets by name.
    /// </summary>
    /// <remarks>
    /// The uniforms come from <c>slangc -reflection-json</c> and are cached beside the SPIR-V as a
    /// <c>.uniforms</c> file. An entry cached before uniforms were kept has none.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The cache has no entry for this source and there is no compiler, or the compiler reported an error.
    /// </exception>
    public static SlangStage CompileStage(string source, string fileName, string entryPoint, ShaderStage stage,
        string? cacheDirectory = null, string? importDirectory = null) =>
        CompileStage(source, fileName, entryPoint, stage, cacheDirectory, importDirectory, CompilerPath);

    internal static SlangStage CompileStage(string source, string fileName, string entryPoint, ShaderStage stage,
        string? cacheDirectory, string? importDirectory, string? compiler)
    {
        var key = CacheKey(source, entryPoint, stage, importDirectory);
        var cached = cacheDirectory is null
            ? null
            : Path.Combine(cacheDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}.{entryPoint}.{key}.spv");
        var uniformsFile = cached is null ? null : Path.ChangeExtension(cached, ".uniforms");

        if (cached is not null && File.Exists(cached))
            return new SlangStage(File.ReadAllBytes(cached),
                File.Exists(uniformsFile) ? ReadUniforms(File.ReadAllText(uniformsFile)) : []);

        if (compiler is null)
            throw new InvalidOperationException(
                $"'{fileName}' ({entryPoint}) is not in the shader cache and slangc was not found. " +
                "Run build/fetch-slang.sh or set ENGINE_SLANGC.");

        var (bytecode, reflection) = Run(compiler, source, fileName, entryPoint, stage, importDirectory);
        var uniforms = UniformsOf(reflection);

        if (cached is not null)
        {
            Directory.CreateDirectory(cacheDirectory!);
            // Written beside and moved into place, so a reader never sees half an entry. The
            // uniforms go first, so an entry whose SPIR-V is there has them too.
            WriteAtomically(uniformsFile!, System.Text.Encoding.UTF8.GetBytes(WriteUniforms(uniforms)));
            WriteAtomically(cached, bytecode);
        }

        return new SlangStage(bytecode, uniforms);
    }

    private static void WriteAtomically(string path, byte[] bytes)
    {
        var partial = path + ".partial";
        File.WriteAllBytes(partial, bytes);
        File.Move(partial, path, overwrite: true);
    }

    /// <summary>The uniforms declared at the top level of a shader, from slangc's reflection JSON.</summary>
    /// <remarks>
    /// Slang gathers them into one constant buffer at binding 0 of set 0, with each field's offset
    /// and size in it, which is all a program needs to set one by name.
    /// </remarks>
    internal static IReadOnlyList<ShaderUniform> UniformsOf(string? reflectionJson)
    {
        if (string.IsNullOrEmpty(reflectionJson)) return [];
        using var document = System.Text.Json.JsonDocument.Parse(reflectionJson);
        var uniforms = new List<ShaderUniform>();
        if (!document.RootElement.TryGetProperty("parameters", out var parameters)) return uniforms;
        foreach (var parameter in parameters.EnumerateArray())
        {
            if (!parameter.TryGetProperty("binding", out var binding) ||
                binding.GetProperty("kind").GetString() != "uniform") continue;
            uniforms.Add(new ShaderUniform(
                parameter.GetProperty("name").GetString()!,
                binding.GetProperty("offset").GetInt32(),
                binding.GetProperty("size").GetInt32()));
        }
        return uniforms;
    }

    // A cached stage's uniforms, one "name offset size" per line.
    private static string WriteUniforms(IReadOnlyList<ShaderUniform> uniforms) =>
        string.Concat(uniforms.Select(u => $"{u.Name} {u.Offset} {u.Size}\n"));

    private static IReadOnlyList<ShaderUniform> ReadUniforms(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' '))
            .Select(parts => new ShaderUniform(parts[0], int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();

    private static (byte[] Spirv, string? Reflection) Run(string compiler, string source, string fileName, string entryPoint, ShaderStage stage,
        string? importDirectory)
    {
        // slangc reads files rather than standard input, and the source may not exist on disk at
        // all (an asset read from memory), so it is written to a directory of its own.
        var work = Directory.CreateTempSubdirectory("engine-slang-");
        try
        {
            var input = Path.Combine(work.FullName, Path.GetFileName(fileName));
            var output = Path.Combine(work.FullName, "out.spv");
            var reflection = Path.Combine(work.FullName, "out.json");
            File.WriteAllText(input, source);

            var info = new ProcessStartInfo(compiler)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            info.ArgumentList.Add(input);
            foreach (var argument in Arguments.Split(' '))
                info.ArgumentList.Add(argument);
            info.ArgumentList.Add("-entry");
            info.ArgumentList.Add(entryPoint);
            info.ArgumentList.Add("-stage");
            info.ArgumentList.Add(StageName(stage));
            if (importDirectory is not null)
            {
                info.ArgumentList.Add("-I");
                info.ArgumentList.Add(importDirectory);
            }
            info.ArgumentList.Add("-reflection-json");
            info.ArgumentList.Add(reflection);
            info.ArgumentList.Add("-o");
            info.ArgumentList.Add(output);

            var stopwatch = Stopwatch.StartNew();
            using var process = Process.Start(info)
                ?? throw new InvalidOperationException($"slangc could not be started from '{compiler}'.");
            var errors = process.StandardError.ReadToEndAsync();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || !File.Exists(output))
                throw new InvalidOperationException(
                    $"slangc failed on '{fileName}' ({entryPoint}):{Environment.NewLine}{errors.Result.Trim()}");

            var bytecode = File.ReadAllBytes(output);
            Logger.Debug($"Compiled {fileName} ({entryPoint}) to {bytecode.Length} bytes of SPIR-V in {stopwatch.ElapsedMilliseconds}ms.");
            return (bytecode, File.Exists(reflection) ? File.ReadAllText(reflection) : null);
        }
        finally
        {
            try { work.Delete(recursive: true); }
            catch (IOException) { }
        }
    }

    private static string CacheKey(string source, string entryPoint, ShaderStage stage, string? importDirectory)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        // The version names what an entry holds, so entries from before uniforms were cached
        // beside the SPIR-V are compiled again rather than read without them.
        hash.AppendData(Encoding.UTF8.GetBytes($"entries 2\n{Arguments}\n{entryPoint}\n{stage}\n"));
        hash.AppendData(Encoding.UTF8.GetBytes(source));

        foreach (var (path, bytes) in ImportedFiles(source, importDirectory))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(path));
            hash.AppendData(bytes ?? []);
        }

        return Convert.ToHexString(hash.GetHashAndReset(), 0, 8).ToLowerInvariant();
    }

    /// <summary>
    /// Every file <paramref name="source"/> imports or includes from <paramref name="importDirectory"/>,
    /// followed through their own imports, each once, in the order they are first reached: its path
    /// from the directory with forward slashes, and its bytes, or <c>null</c> for one not found,
    /// whose name still counts.
    /// </summary>
    /// <remarks>
    /// A name is looked for beside the file that names it first and in the import directory after,
    /// as slangc looks, so a shader in a subfolder that includes its neighbor is keyed by that
    /// neighbor. The source itself has no folder of its own, since it is compiled from a copy.
    /// </remarks>
    internal static IEnumerable<(string Path, byte[]? Bytes)> ImportedFiles(string source, string? importDirectory)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<(string Name, string? From)>(ImportedNames(source).Select(n => (n, (string?)null)));
        while (pending.TryDequeue(out var next))
        {
            var found = importDirectory is null ? null : Resolve(next.From, importDirectory, next.Name);
            var path = found is null ? "?" + next.Name : Path.GetRelativePath(importDirectory!, found).Replace('\\', '/');
            if (!seen.Add(path)) continue;
            if (found is null)
            {
                yield return (path, null);
                continue;
            }

            var bytes = File.ReadAllBytes(found);
            yield return (path, bytes);
            var folder = Path.GetDirectoryName(found);
            foreach (var name in ImportedNames(Encoding.UTF8.GetString(bytes)))
                pending.Enqueue((name, folder));
        }
    }

    // The names in `import module.name;`, `import "file.slang";` and `#include "file.slang"`, the
    // module names as the relative paths Slang looks for them at.
    private static IEnumerable<string> ImportedNames(string source)
    {
        foreach (Match match in ImportPattern().Matches(source))
            yield return match.Groups["module"].Success
                ? match.Groups["module"].Value.Replace('.', '/') + ".slang"
                : match.Groups["file"].Value;
    }

    // Beside the naming file first, then in the import directory. Slang writes an underscore in
    // a module's name as a dash in its file's, and accepts either.
    private static string? Resolve(string? fromFolder, string importDirectory, string name)
    {
        foreach (var folder in fromFolder is null ? [importDirectory] : new[] { fromFolder, importDirectory })
        foreach (var candidate in new[] { name, name.Replace('_', '-') })
        {
            var full = Path.GetFullPath(Path.Combine(folder, candidate));
            if (File.Exists(full)) return full;
        }
        return null;
    }

    [GeneratedRegex("""^\s*(?:import\s+(?:(?<module>[\w.]+)|"(?<file>[^"]+)")\s*;|#include\s+"(?<file>[^"]+)")""", RegexOptions.Multiline)]
    private static partial Regex ImportPattern();

    private static string StageName(ShaderStage stage) => stage switch
    {
        ShaderStage.Vertex => "vertex",
        ShaderStage.Fragment => "fragment",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported shader stage."),
    };

    private static string? Locate()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("ENGINE_SLANGC");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
            return fromEnvironment;

        var executable = OperatingSystem.IsWindows() ? "slangc.exe" : "slangc";

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (directory.Length == 0) continue;
            var candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate)) return candidate;
        }

        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "build", "tools", "slang", "bin", executable);
                if (File.Exists(candidate)) return candidate;
            }
        }

        Logger.Warn("slangc was not found, so only shaders already in a cache can be loaded. Run build/fetch-slang.sh.");
        return null;
    }
}

/// <summary>One stage compiled by <see cref="SlangCompiler"/>: its SPIR-V and its top-level uniforms.</summary>
public sealed record SlangStage(byte[] Spirv, IReadOnlyList<ShaderUniform> Uniforms);

/// <summary>A uniform a shader declares at the top level: where it sits in its constant buffer, in bytes.</summary>
public readonly record struct ShaderUniform(string Name, int Offset, int Size);

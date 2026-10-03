using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

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
/// A cache entry is keyed by a hash of the source, the entry point, the stage, every
/// <c>.slang</c> file in the import directory and the compiler arguments. Hashing the whole
/// import directory invalidates more than an exact import graph would, and it needs no parse of
/// the source to find what it imports.
/// </para>
/// </remarks>
public static class SlangCompiler
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
        Compile(source, fileName, entryPoint, stage, cacheDirectory, importDirectory, CompilerPath);

    internal static byte[] Compile(string source, string fileName, string entryPoint, ShaderStage stage,
        string? cacheDirectory, string? importDirectory, string? compiler)
    {
        var key = CacheKey(source, entryPoint, stage, importDirectory);
        var cached = cacheDirectory is null
            ? null
            : Path.Combine(cacheDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}.{entryPoint}.{key}.spv");

        if (cached is not null && File.Exists(cached))
            return File.ReadAllBytes(cached);

        if (compiler is null)
            throw new InvalidOperationException(
                $"'{fileName}' ({entryPoint}) is not in the shader cache and slangc was not found. " +
                "Run build/fetch-slang.sh or set ENGINE_SLANGC.");

        var bytecode = Run(compiler, source, fileName, entryPoint, stage, importDirectory);

        if (cached is not null)
        {
            Directory.CreateDirectory(cacheDirectory!);
            // Written beside and moved into place, so a reader never sees half an entry.
            var partial = cached + ".partial";
            File.WriteAllBytes(partial, bytecode);
            File.Move(partial, cached, overwrite: true);
        }

        return bytecode;
    }

    private static byte[] Run(string compiler, string source, string fileName, string entryPoint, ShaderStage stage,
        string? importDirectory)
    {
        // slangc reads files rather than standard input, and the source may not exist on disk at
        // all (an asset read from memory), so it is written to a directory of its own.
        var work = Directory.CreateTempSubdirectory("engine-slang-");
        try
        {
            var input = Path.Combine(work.FullName, Path.GetFileName(fileName));
            var output = Path.Combine(work.FullName, "out.spv");
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
            return bytecode;
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
        hash.AppendData(Encoding.UTF8.GetBytes($"{Arguments}\n{entryPoint}\n{stage}\n"));
        hash.AppendData(Encoding.UTF8.GetBytes(source));

        if (importDirectory is not null && Directory.Exists(importDirectory))
        {
            foreach (var file in Directory.GetFiles(importDirectory, "*.slang", SearchOption.AllDirectories)
                         .Order(StringComparer.Ordinal))
            {
                hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(importDirectory, file)));
                hash.AppendData(File.ReadAllBytes(file));
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset(), 0, 8).ToLowerInvariant();
    }

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

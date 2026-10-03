using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Engine.Files.Compiler;

public abstract partial class RuntimeAssemblyCompiler<TResult>
{
    /// <summary>
    /// Full compilation cycle: enumerate sources -> compile with Roslyn -> load into a
    /// fresh collectible <see cref="ScriptLoadContext"/> -> hand off to the domain
    /// <see cref="OnAssemblyLoaded"/> hook.
    /// </summary>
    /// <remarks>Held under <c>_compileLock</c> to serialize concurrent debounce + manual triggers.</remarks>
    protected TResult CompileAndLoad()
    {
        lock (_compileLock)
        {
            var result = new TResult();

            var csFiles = new List<string>();
            foreach (var dir in _scriptDirectories)
            {
                if (!Directory.Exists(dir)) continue;
                csFiles.AddRange(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories));
            }

            if (csFiles.Count == 0)
            {
                OnNoSourceFiles(result);
                return result;
            }

            result.Files = csFiles.Select(Path.GetFileName).ToArray()!;

            var assembly = CompileWithRoslyn(csFiles, result);
            if (assembly is null) return result;

            OnAssemblyLoaded(assembly, result);

            if (string.IsNullOrEmpty(result.Message))
            {
                result.Success = true;
                result.Message = $"Compiled {csFiles.Count} file(s) successfully (gen {_generation}).";
            }

            return result;
        }
    }

    /// <summary>In-memory Roslyn compile of <paramref name="csFiles"/>.</summary>
    /// <returns>The loaded assembly or <see langword="null"/> on failure (errors populated on <paramref name="result"/>).</returns>
    protected Assembly? CompileWithRoslyn(List<string> csFiles, TResult result)
    {
        var syntaxTrees = new List<SyntaxTree>();
        foreach (var file in csFiles)
        {
            try
            {
                var source = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(source, path: file,
                    options: new CSharpParseOptions(LanguageVersion.Latest));
                syntaxTrees.Add(tree);
            }
            catch (Exception ex)
            {
                result.Errors.Add(new RuntimeCompilationError
                {
                    FileName = Path.GetFileName(file),
                    Message = $"Failed to read: {ex.Message}",
                });
            }
        }

        if (result.Errors.Count > 0)
        {
            result.Success = false;
            result.Message = "Parse errors.";
            return null;
        }

        var gen = Interlocked.Increment(ref _generation);
        var compilation = CSharpCompilation.Create(
            $"{AssemblyNamePrefix}_Gen{gen}",
            syntaxTrees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Debug)
                .WithAllowUnsafe(true));

        // Domain hook: attach source generators (e.g. BehaviorGenerator) and replace the compilation.
        compilation = RunGenerators(compilation, result);

        if (result.Errors.Count > 0)
        {
            result.Success = false;
            result.Message = $"Generator failed with {result.Errors.Count} error(s).";
            return null;
        }

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            foreach (var diag in emitResult.Diagnostics)
            {
                if (diag.Severity != DiagnosticSeverity.Error) continue;
                var lineSpan = diag.Location.GetMappedLineSpan();
                result.Errors.Add(new RuntimeCompilationError
                {
                    FileName = Path.GetFileName(lineSpan.Path ?? ""),
                    Message = diag.GetMessage(),
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                });
            }

            result.Success = false;
            result.Message = $"Compilation failed with {result.Errors.Count} error(s).";
            return null;
        }

        ms.Position = 0;
        UnloadCurrent();

        var loadContext = new ScriptLoadContext($"{LoadContextPrefix}_Gen{gen}");
        var assembly = loadContext.LoadFromStream(ms);
        _currentContext = loadContext;

        return assembly;
    }
}
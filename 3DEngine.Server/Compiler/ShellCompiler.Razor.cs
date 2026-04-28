using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Editor.Shell;

public sealed partial class ShellCompiler
{
    private string? _razorProjectDir;

    /// <summary>
    /// Compiles a mixed set of <c>.cs</c> and <c>.razor</c> files by generating
    /// a temporary Razor SDK project and invoking <c>dotnet build</c>.
    /// Returns the path to the compiled assembly, or <see langword="null"/> on failure.
    /// </summary>
    /// <param name="csFiles">Plain C# script files.</param>
    /// <param name="razorFiles">Blazor <c>.razor</c> component files.</param>
    /// <param name="cssFiles">Optional CSS files to collect into the descriptor.</param>
    /// <param name="result">Compilation result to populate with errors/warnings.</param>
    /// <returns>Path to the compiled DLL, or <see langword="null"/> on failure.</returns>
    private string? CompileWithDotnetBuild(
        List<string> csFiles,
        List<string> razorFiles,
        List<string> cssFiles,
        ShellCompilationResult result)
    {
        var gen = Interlocked.Increment(ref _generation);
        var projectDir = GetOrCreateRazorProjectDir(gen);

        try
        {
            var srcDir = Path.Combine(projectDir, "src");
            if (Directory.Exists(srcDir))
                Directory.Delete(srcDir, recursive: true);
            Directory.CreateDirectory(srcDir);

            // Preserve relative paths so files in different subdirectories don't collide.
            foreach (var file in csFiles.Concat(razorFiles))
            {
                var relativePath = GetRelativeScriptPath(file);
                var dest = Path.Combine(srcDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
            }

            foreach (var dir in _scriptDirectories)
            {
                var imports = Path.Combine(dir, "_Imports.razor");
                if (File.Exists(imports))
                {
                    File.Copy(imports, Path.Combine(srcDir, "_Imports.razor"), overwrite: true);
                    break;
                }
            }

            var csprojPath = Path.Combine(projectDir, "EditorShells.csproj");
            File.WriteAllText(csprojPath, GenerateProjectFile(gen));

            var (exitCode, stdout, stderr) = RunDotnetBuild(projectDir);

            if (exitCode != 0)
            {
                ParseBuildErrors(stderr + "\n" + stdout, result);
                if (result.Errors.Count == 0)
                {
                    // Fallback when output didn't match the standard error format.
                    result.Errors.Add(new ShellCompilationError
                    {
                        FileName = "dotnet build",
                        Message = $"Build failed (exit code {exitCode}):\n{stderr}"
                    });
                }

                result.Success = false;
                result.Message = $"Razor build failed with {result.Errors.Count} error(s).";
                return null;
            }

            var outputDll = Path.Combine(projectDir, "bin", "Debug", "net10.0", "EditorShells.dll");
            if (!File.Exists(outputDll))
                outputDll = Path.Combine(projectDir, "bin", "Release", "net10.0", "EditorShells.dll");

            if (!File.Exists(outputDll))
            {
                result.Errors.Add(new ShellCompilationError
                {
                    FileName = "dotnet build",
                    Message = "Build succeeded but output DLL was not found."
                });
                result.Success = false;
                result.Message = "Build output not found.";
                return null;
            }

            return outputDll;
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ShellCompilationError
            {
                FileName = "dotnet build",
                Message = $"Build process failed: {ex.Message}"
            });
            result.Success = false;
            result.Message = $"Build process exception: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Creates (or reuses) a temporary directory for the Razor build project.
    /// </summary>
    private string GetOrCreateRazorProjectDir(int gen)
    {
        if (_razorProjectDir != null && Directory.Exists(_razorProjectDir))
            return _razorProjectDir;

        _razorProjectDir = Path.Combine(Path.GetTempPath(), "EditorShells_RazorBuild");
        Directory.CreateDirectory(_razorProjectDir);
        return _razorProjectDir;
    }

    /// <summary>
    /// Generates a <c>.csproj</c> file content for the temporary Razor build project.
    /// Uses <c>Microsoft.NET.Sdk.Razor</c> for full Blazor component compilation support.
    /// </summary>
    private string GenerateProjectFile(int gen)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<Project Sdk="Microsoft.NET.Sdk.Razor">""");
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <TargetFramework>net10.0</TargetFramework>");
        sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        sb.AppendLine("    <Nullable>enable</Nullable>");
        sb.AppendLine("    <NoWarn>$(NoWarn);CS1591</NoWarn>");
        sb.AppendLine($"    <AssemblyName>EditorShells</AssemblyName>");
        sb.AppendLine($"    <RootNamespace>EditorScripts</RootNamespace>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine();

        // Framework reference for Blazor components
        sb.AppendLine("  <ItemGroup>");
        sb.AppendLine("""    <FrameworkReference Include="Microsoft.AspNetCore.App" />""");
        sb.AppendLine("  </ItemGroup>");
        sb.AppendLine();

        // Assembly references (non-framework assemblies added by the user)
        var userRefs = GetUserAssemblyPaths();
        if (userRefs.Count > 0)
        {
            sb.AppendLine("  <ItemGroup>");
            foreach (var refPath in userRefs)
            {
                var name = Path.GetFileNameWithoutExtension(refPath);
                sb.AppendLine($"""    <Reference Include="{name}">""");
                sb.AppendLine($"      <HintPath>{refPath}</HintPath>");
                sb.AppendLine("    </Reference>");
            }
            sb.AppendLine("  </ItemGroup>");
        }

        sb.AppendLine("</Project>");
        return sb.ToString();
    }

    /// <summary>
    /// Extracts the file paths of user-added assembly references (non-framework assemblies
    /// like Editor.Shell, Engine.Common, etc.) for inclusion in the temporary project.
    /// </summary>
    private List<string> GetUserAssemblyPaths()
    {
        var paths = new List<string>();
        foreach (var asmRef in _userAssemblyPaths)
        {
            if (File.Exists(asmRef))
                paths.Add(asmRef);
        }
        return paths;
    }

    /// <summary>
    /// Runs <c>dotnet build</c> on the temporary project and captures stdout/stderr.
    /// </summary>
    private static (int ExitCode, string Stdout, string Stderr) RunDotnetBuild(string projectDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build --nologo -v q",
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(60_000);

        return (proc.ExitCode, stdout, stderr);
    }

    /// <summary>
    /// Parses <c>dotnet build</c> output for error messages in the standard
    /// <c>file(line,col): error CODE: message</c> format.
    /// </summary>
    private static void ParseBuildErrors(string output, ShellCompilationResult result)
    {
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Match: path(line,col): error CS1234: message
            if (trimmed.Contains(": error "))
            {
                var errorIdx = trimmed.IndexOf(": error ", StringComparison.Ordinal);
                var prefix = trimmed[..errorIdx];
                var message = trimmed[(errorIdx + 2)..]; // "error CS1234: message"

                var fileName = prefix;
                var errorLine = 0;
                var errorCol = 0;

                // Extract (line,col) from prefix
                var parenIdx = prefix.LastIndexOf('(');
                if (parenIdx >= 0)
                {
                    fileName = prefix[..parenIdx];
                    var coords = prefix[(parenIdx + 1)..].TrimEnd(')');
                    var parts = coords.Split(',');
                    if (parts.Length >= 1) int.TryParse(parts[0], out errorLine);
                    if (parts.Length >= 2) int.TryParse(parts[1], out errorCol);
                }

                result.Errors.Add(new ShellCompilationError
                {
                    FileName = Path.GetFileName(fileName),
                    Message = message,
                    Line = errorLine,
                    Column = errorCol,
                });
            }
            else if (trimmed.Contains(": warning "))
            {
                var warnIdx = trimmed.IndexOf(": warning ", StringComparison.Ordinal);
                var message = trimmed[(warnIdx + 2)..];
                result.Warnings.Add(message);
            }
        }
    }

    /// <summary>
    /// Returns the relative path of a script file within its parent script directory.
    /// Falls back to just the file name if no matching directory is found.
    /// </summary>
    private string GetRelativeScriptPath(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        foreach (var dir in _scriptDirectories)
        {
            var fullDir = Path.GetFullPath(dir);
            if (fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath[(fullDir.Length + 1)..]; // strip the directory prefix + separator
            }
        }
        return Path.GetFileName(filePath);
    }
}

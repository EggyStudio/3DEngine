using System.Diagnostics;
using System.Text;
using FluentAssertions;

namespace Engine.Tests.Scripts;

/// <summary>
/// <c>build/package-size.py</c> says each package of a version with its room under the size
/// nuget.org takes, and fails naming each one over it.
/// </summary>
/// <remarks>
/// The script is copied into a folder of its own with files standing in for packages, which it
/// measures and never opens, and given a limit of a few kilobytes, so no package of hundreds of
/// megabytes is written to find where it fails.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class PackageSizeTests : IDisposable
{
    private static readonly string Root = Api.CheatsheetTests.RepoRoot();
    private readonly TestFolder _folder = new("engine-package-size-");

    public void Dispose() => _folder.Dispose();

    [NeedsPythonFact]
    public void Each_Package_Under_The_Size_Is_Said_With_Its_Room()
    {
        Package("3DEngine.1.0.0-t.nupkg", 1000);
        Package("3DEngine.Templates.1.0.0-t.nupkg", 500);
        Package("3DEngine.0.9.0.nupkg", 100_000);

        // The script leaves 4,096 bytes of the request for its framing, so 8,192 lets 4,096 through.
        var (exit, said, errors) = Measure(8192);

        exit.Should().Be(0, errors);
        said.Should().Contain("3DEngine.1.0.0-t.nupkg 1,000 bytes, 3,096 bytes of room");
        said.Should().Contain("3DEngine.Templates.1.0.0-t.nupkg 500 bytes, 3,596 bytes of room");
        said.Should().NotContain("0.9.0", "a package of another version is not this run's");
    }

    [NeedsPythonFact]
    public void A_Package_Over_The_Size_Fails_The_Run_Naming_It()
    {
        Package("3DEngine.1.0.0-t.nupkg", 5000);
        Package("3DEngine.Templates.1.0.0-t.nupkg", 500);

        var (exit, said, errors) = Measure(8192);

        exit.Should().NotBe(0, $"a package was over the size: {said}");
        said.Should().Contain("3DEngine.1.0.0-t.nupkg 5,000 bytes, -904 bytes of room");
        errors.Should().Contain("nothing is pushed: 3DEngine.1.0.0-t.nupkg");
        errors.Should().NotContain("Templates", "the templates' package is under the size");
    }

    // A file of the size given in the copy's build/package, standing in for a package.
    private void Package(string name, int bytes)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_folder.File("work"), "build", "package")).FullName;
        File.WriteAllBytes(Path.Combine(folder, name), new byte[bytes]);
    }

    // The script's measure of the copy's packages of 1.0.0-t, its exit code, what it said and its errors.
    private (int Exit, string Said, string Errors) Measure(int limit)
    {
        var work = _folder.File("work");
        Directory.CreateDirectory(Path.Combine(work, "build"));
        File.Copy(Path.Combine(Root, "build", "package-size.py"), Path.Combine(work, "build", "package-size.py"), overwrite: true);

        var start = new ProcessStartInfo(Probes.Python.Value!)
        {
            WorkingDirectory = work,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.ArgumentList.Add(Path.Combine(work, "build", "package-size.py"));
        start.ArgumentList.Add("1.0.0-t");
        start.Environment["PACKAGE_SIZE_LIMIT"] = limit.ToString();
        start.Environment.Remove("GITHUB_STEP_SUMMARY");

        using var script = Process.Start(start)!;
        var said = script.StandardOutput.ReadToEndAsync();
        var errors = script.StandardError.ReadToEndAsync();
        script.WaitForExit(60_000).Should().BeTrue("the script ends");
        return (script.ExitCode, said.Result, errors.Result);
    }
}

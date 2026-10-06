using System.IO.Compression;
using System.Reflection.PortableExecutable;
using FluentAssertions;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Engine.Tests.Api;

namespace Engine.Tests.Package;

/// <summary>
/// The newest engine package in build/package, as <c>build/pack.sh</c> made it, opened and read for
/// what a game built on it needs: the engine, compiled ahead for each system it runs on beside its
/// portable code, its generator, its documentation, its compiled shaders, its readme, license,
/// notices and release notes, and a native library from its dependencies for each system. The pack workflow runs these before a package is offered.
/// </summary>
public sealed class PackageContentsTests
{
    [NeedsPackageFact]
    public void The_Package_Carries_The_Engine_Its_Generator_And_Its_Documentation()
    {
        using var package = Open();
        Entries(package).Should().Contain(["lib/net10.0/3DEngine.dll", "lib/net10.0/3DEngine.xml",
            "analyzers/dotnet/cs/3DEngine.Generator.dll", "analyzers/dotnet/cs/3DEngine.CodeFixes.dll"]);

        // The documentation of the flat API, which a game's editor shows on hovering a call.
        var documentation = Read(package, "lib/net10.0/3DEngine.xml");
        documentation.Should().Contain("M:Engine.Engine3D.InitWindow(", "the documentation file is the engine's");
    }

    [NeedsPackageFact]
    public void Every_Entry_Point_Of_The_Built_In_Shaders_Is_Compiled_Into_The_Cache()
    {
        using var package = Open();
        var entries = Entries(package);
        var shaders = entries.Where(e => e.StartsWith("contentFiles/any/any/source/shaders/") && e.EndsWith(".slang")).ToList();
        shaders.Should().NotBeEmpty();
        foreach (var shader in shaders)
        {
            var name = Path.GetFileNameWithoutExtension(shader);
            var entryPoints = Regex.Matches(Read(package, shader), @"^\s*\[shader\(", RegexOptions.Multiline).Count;
            var compiled = entries.Count(e => e.StartsWith($"contentFiles/any/any/source/.slang-cache/{name}.") && e.EndsWith(".spv"));
            compiled.Should().Be(entryPoints, $"{name}.slang has {entryPoints} entry points, each compiled ahead so a game needs no slangc");
        }
    }

    [NeedsPackageFact]
    public void The_Package_Carries_Its_Readme_License_Notices_And_Release_Notes()
    {
        using var package = Open();
        Entries(package).Should().Contain(["README.md", "LICENSE", "THIRD-PARTY-NOTICES.md"]);
        var spec = Spec(package);
        Element(spec, "readme").Should().Be("README.md");
        Element(spec, "license").Should().Be("MPL-2.0");
        Element(spec, "releaseNotes").Should().NotBeNullOrWhiteSpace("build/pack.sh writes the commits since the version was raised");

        // Every package the engine depends on is named in the notices with its license.
        var notices = Read(package, "THIRD-PARTY-NOTICES.md");
        foreach (var (id, _) in Dependencies(spec))
            notices.Should().Contain(id, $"{id} is a dependency of the package, so the notices name it");
    }

    [NeedsPackageFact]
    public void The_Package_Carries_The_Engine_Compiled_Ahead_For_Each_System_It_Runs_On()
    {
        using var package = Open();
        foreach (var rid in Runtimes())
        {
            var entry = $"runtimes/{rid}/lib/net10.0/3DEngine.dll";
            Entries(package).Should().Contain(entry, $"a game run from its project on {rid} takes the engine compiled ahead");
            using var bytes = new MemoryStream();
            using (var stream = package.GetEntry(entry)!.Open()) stream.CopyTo(bytes);
            bytes.Position = 0;
            using var image = new PEReader(bytes);
            image.PEHeaders.CorHeader!.ManagedNativeHeaderDirectory.Size.Should().BePositive($"{entry} is ReadyToRun, its machine code beside its IL");
            // ReadyToRun marks the system an image is for in its machine, the architecture's number
            // crossed with one for the system, 0 for Windows, 0x7B79 for Linux and 0x4644 for macOS.
            var architecture = rid.EndsWith("arm64", StringComparison.Ordinal) ? 0xAA64 : 0x8664;
            var system = rid.StartsWith("linux", StringComparison.Ordinal) ? 0x7B79 : rid.StartsWith("osx", StringComparison.Ordinal) ? 0x4644 : 0;
            ((int)image.PEHeaders.CoffHeader.Machine).Should().Be(architecture ^ system, $"{entry} is compiled for {rid}");
        }
    }

    [NeedsPackageFact]
    public void Each_System_The_Package_Runs_On_Has_Its_Native_Libraries()
    {
        using var package = Open();
        var dependencies = Dependencies(Spec(package));
        var packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                       ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

        foreach (var rid in Runtimes())
        foreach (var library in new[] { "SDL3", "cimgui", "assimp" })
        {
            var file = rid.StartsWith("win") ? $"{library}.dll" : rid.StartsWith("osx") ? $"lib{library}.dylib" : $"lib{library}.so";
            var carriers = dependencies.Where(d =>
                File.Exists(Path.Combine(packages, d.Id.ToLowerInvariant(), d.Version.ToLowerInvariant(), "runtimes", rid, "native", file)));
            carriers.Should().NotBeEmpty($"a game on {rid} loads {file}, which one of the package's dependencies carries");
        }
    }

    // The runtimes BUILDING.md's table of platforms claims, each on x64 and arm64.
    private static IEnumerable<string> Runtimes()
    {
        var building = File.ReadAllLines(Path.Combine(CheatsheetTests.RepoRoot(), ".github", "BUILDING.md"));
        var table = building.SkipWhile(l => l != "## Platforms").Skip(1).SkipWhile(l => !l.StartsWith('|')).TakeWhile(l => l.StartsWith('|')).Skip(2);
        var systems = table.Select(row => row.Split('|', StringSplitOptions.TrimEntries)[1]).ToList();
        systems.Should().NotBeEmpty();
        foreach (var system in systems)
        {
            var prefix = system switch
            {
                "Linux" => "linux",
                "Windows" => "win",
                "macOS" => "osx",
                _ => throw new InvalidOperationException($"BUILDING.md claims {system}, which this test has no runtime for"),
            };
            yield return $"{prefix}-x64";
            yield return $"{prefix}-arm64";
        }
    }

    /// <summary>The newest engine package in build/package, or none.</summary>
    internal static string? Newest()
    {
        var folder = Path.Combine(CheatsheetTests.RepoRoot(), "build", "package");
        if (!Directory.Exists(folder)) return null;
        return Directory.EnumerateFiles(folder, "3DEngine.*.nupkg")
            .Where(f => !Path.GetFileName(f).StartsWith("3DEngine.Templates.") && !f.EndsWith(".snupkg"))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static ZipArchive Open()
    {
        var newest = Newest();
        newest.Should().NotBeNull("E3D_REQUIRE_PACKAGE is set, so build/pack.sh should have made a package");
        return ZipFile.OpenRead(newest!);
    }

    private static List<string> Entries(ZipArchive package) => package.Entries.Select(e => e.FullName).ToList();

    private static string Read(ZipArchive package, string entry)
    {
        using var reader = new StreamReader(package.GetEntry(entry)!.Open());
        return reader.ReadToEnd();
    }

    private static XElement Spec(ZipArchive package) => XElement.Parse(Read(package, "3DEngine.nuspec"));

    private static string? Element(XElement spec, string name) => spec.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

    private static List<(string Id, string Version)> Dependencies(XElement spec) =>
        spec.Descendants().Where(e => e.Name.LocalName == "dependency")
            .Select(e => ((string)e.Attribute("id")!, (string)e.Attribute("version")!)).Distinct().ToList();
}

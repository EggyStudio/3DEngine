using FluentAssertions;

namespace Engine.Tests.Api;

/// <summary>
/// Each engine project's public surface against the <c>PublicApi.txt</c> checked in beside its
/// project file, the flat API's at <c>3DEngine/PublicApi.txt</c>, so a commit that adds, removes or
/// reshapes anything a game can call changes that file too and is read as such a change.
/// </summary>
/// <remarks>
/// <c>build/api.sh</c> writes the listings again from the built assemblies, through this test with
/// <c>ENGINE_WRITE_API</c> set, once a change to a surface is meant.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class PublicSurfaceTests
{
    /// <summary>The name of each engine assembly, for the listing of each.</summary>
    public static TheoryData<string> Projects() => [.. EngineAssemblies.Names];

    [Theory]
    [MemberData(nameof(Projects))]
    public void The_Public_Surface_Is_The_Listing_Checked_In(string assembly) =>
        IsTheListing(PublicSurface.Write(TestEngine.Assemblies.Single(a => a.GetName().Name == assembly)), ListingOf(assembly), $"{assembly}'s public surface");

    // The listing beside the project file whose AssemblyName is the one given.
    internal static string ListingOf(string assembly)
    {
        var engine = Path.Combine(CheatsheetTests.RepoRoot(), "3DEngine");
        foreach (var project in Directory.EnumerateFiles(engine, "*.csproj", SearchOption.AllDirectories))
            if (System.Text.RegularExpressions.Regex.Match(File.ReadAllText(project), "<AssemblyName>([^<]+)</AssemblyName>") is { Success: true } name
                && name.Groups[1].Value == assembly)
                return Path.Combine(Path.GetDirectoryName(project)!, "PublicApi.txt");
        throw new InvalidOperationException($"No project file under 3DEngine names the assembly {assembly}.");
    }

    // A surface written from the built assembly against the listing checked in at the path given,
    // which the listing becomes where ENGINE_WRITE_API is set.
    internal static void IsTheListing(string built, string listing, string what)
    {
        if (Environment.GetEnvironmentVariable("ENGINE_WRITE_API") == "1") File.WriteAllText(listing, built);

        var checkedIn = File.Exists(listing) ? File.ReadAllText(listing).ReplaceLineEndings("\n") : "";
        if (built == checkedIn) return;

        // Each line that differs, under the type it belongs to, so the failure says what changed.
        var added = Lines(built).Except(Lines(checkedIn)).ToList();
        var removed = Lines(checkedIn).Except(Lines(built)).ToList();
        var report = string.Join("\n", removed.Select(l => "- " + l).Concat(added.Select(l => "+ " + l)).Take(60));
        var file = Path.GetRelativePath(CheatsheetTests.RepoRoot(), listing);
        report.Should().BeEmpty($"{what} changed without {file}, which build/api.sh writes again");
        built.Should().Be(checkedIn, "the listing holds the same lines in the same order");
    }

    [Fact]
    public void A_Listing_Spells_Members_As_A_Signature_Does()
    {
        var listing = PublicSurface.Write(typeof(E3D).Assembly);
        listing.Should().Contain("static class Engine.E3D\n");
        listing.Should().Contain("  static void DrawCube(Vector3 position, float width, float height, float length, Color color)\n");
        listing.Should().Contain("  static PhysicsRayCollision GetRayCollisionPhysicsEx(Ray ray, float maxDistance, PhysicsBody ignore)\n");
        listing.Should().Contain("  static void SetBloom(float intensity, float threshold = 1f)\n");
        listing.Should().NotContain("<Clone>", "nothing the compiler names for itself is listed");
        listing.Should().NotContain(" internal ", "nor anything a game cannot reach");
    }

    // Each member line with its type's header in front, so a line moved between types differs.
    private static IEnumerable<string> Lines(string listing)
    {
        var type = "";
        foreach (var line in listing.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith(' ')) yield return type = line;
            else yield return type + " |" + line;
        }
    }
}

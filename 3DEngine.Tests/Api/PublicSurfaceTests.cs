using FluentAssertions;

namespace Engine.Tests.Api;

/// <summary>
/// The engine's public surface against <c>3DEngine/PublicApi.txt</c>, the listing checked in beside
/// it, so a commit that adds, removes or reshapes anything a game can call changes that file too and
/// is read as such a change.
/// </summary>
/// <remarks>
/// <c>build/api.sh</c> writes the listing again from the built assembly, through this test with
/// <c>ENGINE_WRITE_API</c> set, once a change to the surface is meant.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class PublicSurfaceTests
{
    private static string Listing() => Path.Combine(CheatsheetTests.RepoRoot(), "3DEngine", "PublicApi.txt");

    [Fact]
    public void The_Public_Surface_Is_The_Listing_Checked_In()
    {
        var built = PublicSurface.Write(typeof(Engine3D).Assembly);
        if (Environment.GetEnvironmentVariable("ENGINE_WRITE_API") == "1") File.WriteAllText(Listing(), built);

        var checkedIn = File.Exists(Listing()) ? File.ReadAllText(Listing()).ReplaceLineEndings("\n") : "";
        if (built == checkedIn) return;

        // Each line that differs, under the type it belongs to, so the failure says what changed.
        var added = Lines(built).Except(Lines(checkedIn)).ToList();
        var removed = Lines(checkedIn).Except(Lines(built)).ToList();
        var report = string.Join("\n", removed.Select(l => "- " + l).Concat(added.Select(l => "+ " + l)).Take(60));
        report.Should().BeEmpty("the public surface changed without 3DEngine/PublicApi.txt, which build/api.sh writes again");
        built.Should().Be(checkedIn, "the listing holds the same lines in the same order");
    }

    [Fact]
    public void A_Listing_Spells_Members_As_A_Signature_Does()
    {
        var listing = PublicSurface.Write(typeof(Engine3D).Assembly);
        listing.Should().Contain("static class Engine.Engine3D\n");
        listing.Should().Contain("  static void DrawCube(Vector3 position, float width, float height, float length, Color color)\n");
        listing.Should().Contain("  static RaycastHit GetRayCollisionPhysicsEx(Ray ray, float maxDistance, PhysicsBody ignore)\n");
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

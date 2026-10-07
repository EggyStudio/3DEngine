using System.Text.RegularExpressions;
using FluentAssertions;

namespace Engine.Tests.Docs;

/// <summary>
/// Every name of the public surface the 5.1 package had and this commit lacks is on the page that
/// moves a game from 5.1 to 6.0, so a commit that takes a name out says there what a game writes
/// in its place.
/// </summary>
/// <remarks>
/// The two surfaces are <c>PublicApi.txt</c> at the commit 5.1.116 was packed from and now. A line
/// lost from a type is a member, which the page names in code as <c>Type.Member</c>, or by its
/// name alone for the flat API, which a game calls without its class. A type lost whole is named
/// by itself. A call whose arguments were reordered or retyped loses its line as well, so its name
/// is on the page too.
/// </remarks>
[Trait("Category", "Unit")]
public sealed partial class UpgradingTests
{
    // The commit 5.1.116 was packed from, the package the page counts from.
    private const string FiveOne = "b43818f9";

    [NeedsHistoryFact(FiveOne, "from which docs/upgrading.md counts the names lost")]
    public void Every_Name_The_Public_Surface_Lost_Since_5_1_Is_On_The_Upgrading_Page()
    {
        var root = Api.CheatsheetTests.RepoRoot();
        var before = Surface(NormTests.Git("show", $"{FiveOne}:3DEngine/PublicApi.txt"));
        var now = Surface(File.ReadAllText(Path.Combine(root, "3DEngine", "PublicApi.txt")));
        var page = File.ReadAllText(Path.Combine(root, "docs", "upgrading.md"));
        var code = Ticked().Matches(page).Select(match => match.Groups["code"].Value).ToArray();

        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (type, members) in before)
        {
            if (!now.TryGetValue(type, out var kept))
            {
                if (!Named(code, type)) missing.Add(type);
                continue;
            }
            foreach (var line in members.Except(kept))
            {
                var name = type == "Engine3D" ? MemberName(line) : $"{type}.{MemberName(line)}";
                if (!Named(code, name)) missing.Add(name);
            }
        }

        // Joined, so the message names every one and not the first alone.
        string.Join(", ", missing).Should().BeEmpty("each name a game of 5.1 wrote that is gone is a row of docs/upgrading.md saying what it writes instead");
    }

    [Theory]
    [InlineData("  static Ray GetMouseRay(Vector2 mousePosition, Camera3D camera)", "GetMouseRay")]
    [InlineData("  void DespawnOnEnter<TState>(Entity entity, TState value) where TState : struct, Enum", "DespawnOnEnter")]
    [InlineData("  void Fatal(string message, Exception? exception = null)", "Fatal")]
    [InlineData("  South = 0", "South")]
    [InlineData("  Transform[][] FramePoses { get; init; }", "FramePoses")]
    [InlineData("  Vector2 LeftLensCenter", "LeftLensCenter")]
    [InlineData("  AddedAttribute(params Type[] types)", "AddedAttribute")]
    public void A_Line_Of_The_Listing_Is_Named_By_Its_Member(string line, string name) => MemberName(line).Should().Be(name);

    // Each type of a listing by its name, nested ones with their outer type's, generics left out,
    // with the lines of its members.
    private static Dictionary<string, HashSet<string>> Surface(string listing)
    {
        var types = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        HashSet<string>? members = null;
        foreach (var line in listing.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0))
        {
            if (line.StartsWith(' '))
            {
                members?.Add(line);
                continue;
            }
            var name = WithoutGenerics(TypeName().Match(line).Groups["name"].Value);
            members = types.TryGetValue(name, out var had) ? had : types[name] = [];
        }
        return types;
    }

    // A member's name: before its arguments for a method or a constructor, before its value for an
    // enum's member, before its accessors for a property, and last for a field.
    private static string MemberName(string line)
    {
        var text = line.Trim();
        var end = text.IndexOf('(') is >= 0 and var open ? open
            : text.IndexOf(" =", StringComparison.Ordinal) is >= 0 and var value ? value
            : text.IndexOf(" {", StringComparison.Ordinal) is >= 0 and var accessors ? accessors
            : text.Length;
        var head = WithoutGenerics(text[..end]).TrimEnd();
        return head[(head.LastIndexOf(' ') + 1)..];
    }

    // A name with its type arguments left out, the innermost first.
    private static string WithoutGenerics(string name)
    {
        for (var shorter = Generics().Replace(name, ""); shorter != name; shorter = Generics().Replace(name, ""))
            name = shorter;
        return name;
    }

    // Whether a span of code on the page names the type or member, as a whole name and not the end
    // of a longer one.
    private static bool Named(IEnumerable<string> code, string name)
    {
        var pattern = new Regex($@"(?<![\w.]){Regex.Escape(name)}(?!\w)");
        return code.Any(pattern.IsMatch);
    }

    [GeneratedRegex(@"`(?<code>[^`]+)`")]
    private static partial Regex Ticked();

    [GeneratedRegex(@"\bEngine\.(?<name>[\w.<>,? ]+?)(?:\(|\s:|\s+where\b|$)")]
    private static partial Regex TypeName();

    [GeneratedRegex(@"<[^<>]*>")]
    private static partial Regex Generics();
}

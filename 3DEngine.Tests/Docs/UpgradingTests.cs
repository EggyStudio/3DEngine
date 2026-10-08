using System.Text.RegularExpressions;
using FluentAssertions;

namespace Engine.Tests.Docs;

/// <summary>
/// Every name of the public surface the 5.1 package had and this commit lacks is on the page that
/// moves a game from 5.1 to 6.0, so a commit that takes a name out says there what a game writes
/// in its place, and every name this commit has that 5.1 lacked is in its Added section.
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
        var code = Code(File.ReadAllText(Path.Combine(root, "docs", "upgrading.md")));

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

    [NeedsHistoryFact(FiveOne, "from which docs/upgrading.md counts the names added")]
    public void Every_Name_The_Public_Surface_Gained_Since_5_1_Is_On_The_Upgrading_Page()
    {
        // The other way from the names lost: a type 5.1 lacked is named by itself, and a member
        // whose name its type lacked by its name, a call whose arguments were reordered or retyped
        // keeping a name 5.1 had and being no name added.
        var root = Api.CheatsheetTests.RepoRoot();
        var before = Surface(NormTests.Git("show", $"{FiveOne}:3DEngine/PublicApi.txt"));
        var now = Surface(File.ReadAllText(Path.Combine(root, "3DEngine", "PublicApi.txt")));
        var code = Code(File.ReadAllText(Path.Combine(root, "docs", "upgrading.md")));

        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (type, members) in now)
        {
            if (!before.TryGetValue(type, out var had))
            {
                if (!Named(code, type)) missing.Add(type);
                continue;
            }
            var names = had.Select(MemberName).ToHashSet(StringComparer.Ordinal);
            foreach (var name in members.Select(MemberName).Where(name => !names.Contains(name)).Distinct())
            {
                var written = type == "Engine3D" ? name : $"{type}.{name}";
                if (!Named(code, written)) missing.Add(written);
            }
        }

        string.Join(", ", missing).Should().BeEmpty("each name 6.0 gained is in docs/upgrading.md's Added section, with what it does");
    }

    [Fact]
    public void The_Added_Section_Names_No_Member_The_Surface_Lacks()
    {
        // Each `Type.Member` of the section whose type is the engine's, ImGui's own flags and the
        // like being another library's to keep.
        var root = Api.CheatsheetTests.RepoRoot();
        var now = Surface(File.ReadAllText(Path.Combine(root, "3DEngine", "PublicApi.txt")));
        var page = File.ReadAllText(Path.Combine(root, "docs", "upgrading.md"));
        var added = page[page.IndexOf("## Added", StringComparison.Ordinal)..];
        var lacking = Ticked().Matches(added).Select(match => match.Groups["code"].Value)
            .Select(code => Member().Match(code)).Where(match => match.Success && now.ContainsKey(match.Groups["type"].Value))
            .Where(match => !now[match.Groups["type"].Value].Select(MemberName).Contains(match.Groups["member"].Value))
            .Select(match => match.Value).Distinct();

        string.Join(", ", lacking).Should().BeEmpty("the Added section names what 6.0 has");
    }

    [NeedsHistoryFact(FiveOne, "from which the names lost since 5.1 are counted")]
    public void No_Other_Document_Writes_A_Name_Lost_Since_5_1()
    {
        // The guides' code blocks are built on the package, and their code in a line, the
        // cheatsheet's and the README's are held here, where a name of 5.1 outlived its rename.
        var root = Api.CheatsheetTests.RepoRoot();
        var before = Surface(NormTests.Git("show", $"{FiveOne}:3DEngine/PublicApi.txt"));
        var now = Surface(File.ReadAllText(Path.Combine(root, "3DEngine", "PublicApi.txt")));
        var gone = new List<string>();
        foreach (var (type, members) in before)
        {
            if (!now.TryGetValue(type, out var kept))
            {
                gone.Add(type);
                continue;
            }
            // A name the type still has, as a call whose arguments were reordered, is no name lost.
            var still = kept.Select(MemberName).ToHashSet(StringComparer.Ordinal);
            gone.AddRange(members.Select(MemberName).Where(name => !still.Contains(name)).Distinct()
                .Select(name => type == "Engine3D" ? name : $"{type}.{name}"));
        }

        var pages = Directory.GetFiles(Path.Combine(root, "docs"), "*.md").Where(page => Path.GetFileName(page) != "upgrading.md")
            .Append(Path.Combine(root, "README.md")).Append(Path.Combine(root, "CHEATSHEET.md"));
        var written = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            var code = Code(File.ReadAllText(page));
            foreach (var name in gone.Where(name => Named(code, name)))
                written.Add($"{Path.GetRelativePath(root, page)} {name}");
        }

        string.Join(", ", written).Should().BeEmpty("a document a game's author reads names what 6.0 has, and docs/upgrading.md alone what 5.1 had");
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

    // The code of a page, each fenced block whole and each span of code in a line, the fences taken
    // out first, since a fence's backticks would pair with a span's and shift every span after it.
    private static string[] Code(string page) =>
        [.. Fenced().Matches(page).Select(match => match.Value),
            .. Ticked().Matches(Fenced().Replace(page, "")).Select(match => match.Groups["code"].Value)];

    // Whether a span of code on the page names the type or member, as a whole name and not the end
    // of a longer one, nor the start of a family of them, as `ImageDraw*` is.
    private static bool Named(IEnumerable<string> code, string name)
    {
        var pattern = new Regex($@"(?<![\w.]){Regex.Escape(name)}(?![\w*])");
        return code.Any(pattern.IsMatch);
    }

    [GeneratedRegex(@"`(?<code>[^`]+)`")]
    private static partial Regex Ticked();

    // A fenced block of code, whose lines the cheatsheet's calls are.
    [GeneratedRegex(@"```[\s\S]*?```")]
    private static partial Regex Fenced();

    [GeneratedRegex(@"\bEngine\.(?<name>[\w.<>,? ]+?)(?:\(|\s:|\s+where\b|$)")]
    private static partial Regex TypeName();

    [GeneratedRegex(@"<[^<>]*>")]
    private static partial Regex Generics();

    // A span that is a type's member and nothing else, as `Config.SceneField`.
    [GeneratedRegex(@"^(?<type>[A-Z]\w*)\.(?<member>[A-Z]\w*)$")]
    private static partial Regex Member();
}

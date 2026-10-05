using System.Text.RegularExpressions;
using FluentAssertions;

namespace Engine.Tests.Docs;

/// <summary>
/// <c>docs/first-game.md</c> against the game it makes, <c>games/FirstGame</c>, whose every step is
/// a whole program under <c>steps/</c> that <c>build/first-game.sh</c> builds and runs. Each block
/// of code the page marks with a step is in that step's program as the page shows it, line for
/// line, so the page cannot show code the build does not.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class FirstGameTests
{
    private static readonly string Root = Api.CheatsheetTests.RepoRoot();
    private static string Game => Path.Combine(Root, "games", "FirstGame");

    [GeneratedRegex(@"<!-- (?<mark>step \d\d|level) -->\s*```(?<language>\w+)\n(?<code>.*?)\n```", RegexOptions.Singleline)]
    private static partial Regex MarkedBlock();

    private static IEnumerable<(string Mark, string Code)> Blocks() =>
        MarkedBlock().Matches(File.ReadAllText(Path.Combine(Root, "docs", "first-game.md")))
            .Select(m => (m.Groups["mark"].Value, m.Groups["code"].Value));

    private static string[] Lines(string text) => text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToArray();

    [Fact]
    public void Every_Block_The_Page_Marks_With_A_Step_Is_In_That_Steps_Program()
    {
        var blocks = Blocks().Where(b => b.Mark.StartsWith("step")).ToList();
        blocks.Should().HaveCountGreaterThan(10, "the page shows the code of each step");
        foreach (var (mark, code) in blocks)
        {
            var step = Path.Combine(Game, "steps", mark[5..] + ".cs");
            File.Exists(step).Should().BeTrue($"{mark} is a program under steps/");
            var program = Lines(File.ReadAllText(step));
            var shown = Lines(code);
            var found = Enumerable.Range(0, program.Length - shown.Length + 1).Any(i => program.Skip(i).Take(shown.Length).SequenceEqual(shown));
            found.Should().BeTrue($"the page's block for {mark}, starting \"{shown[0].Trim()}\", is in {mark[5..]}.cs as shown");
        }
    }

    [Fact]
    public void The_Level_The_Page_Shows_Is_The_Games_And_The_Last_Step_Is_Its_Program()
    {
        var level = Blocks().Single(b => b.Mark == "level").Code;
        Lines(level).Should().Equal(Lines(File.ReadAllText(Path.Combine(Game, "resources", "level.json"))).SkipLast(1),
            "the page's level file is the game's");
        var steps = Directory.GetFiles(Path.Combine(Game, "steps"), "*.cs").Order().ToArray();
        File.ReadAllText(steps[^1]).Should().Be(File.ReadAllText(Path.Combine(Game, "Program.cs")), "the last step is the finished game");
    }
}

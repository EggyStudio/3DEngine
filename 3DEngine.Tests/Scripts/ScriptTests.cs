using System.Text.RegularExpressions;
using FluentAssertions;

namespace Engine.Tests.Scripts;

/// <summary>
/// The shell scripts a workflow runs on Windows or macOS, and those they call, use only what GNU's
/// tools and the BSD ones macOS has both read, and what the bash 3.2 macOS ships reads.
/// </summary>
/// <remarks>
/// <c>sed -i</c> in <c>build/pack.sh</c> passed every Linux run and failed the first on macOS, whose
/// <c>sed</c> took the first file's name as its script. Windows runs them in Git's bash, whose
/// tools are GNU's, so macOS is the one that tells.
/// </remarks>
[Trait("Category", "Unit")]
public sealed partial class ScriptTests
{
    private static readonly string Root = Api.CheatsheetTests.RepoRoot();

    // Each form, how it is found and what is written in its place.
    private static readonly (string Form, Regex Pattern, string Instead)[] GnuOnly =
    [
        ("sed -i", new(@"\bsed\s+(?:-\w+\s+)*-\w*i\b"), "sed writing a file beside it, moved over it"),
        ("grep -P", new(@"\bgrep\s+(?:-\w+\s+)*-\w*P"), "grep -E, or Python"),
        ("readarray", new(@"\b(?:readarray|mapfile)\b"), "a while read loop"),
        ("date -d", new(@"\bdate\s+(?:\S+\s+)*?(?:-d|--date)\b"), "Python"),
        ("stat -c", new(@"\bstat\s+(?:-\w+\s+)*(?:-c|--format)\b"), "wc -c, or Python"),
        ("sha256sum", new(@"\b(?:sha256sum|sha1sum|md5sum)\b"), "Python's hashlib"),
        ("${x,,}", new(@"\$\{[A-Za-z_]\w*(?:,,?|\^\^?)\}"), "tr"),
    ];

    [Fact]
    public void The_Scripts_Run_On_Windows_And_MacOS_Use_Only_What_Both_Systems_Tools_Read()
    {
        var scripts = ScriptsRunElsewhere();
        scripts.Should().Contain(new[] { "build/play-game.sh", "build/pack.sh", "build/fetch-slang.sh" }, "the jobs run them, and pack.sh by way of play-game.sh");

        var found = new List<string>();
        foreach (var script in scripts)
        {
            var lines = File.ReadAllLines(Path.Combine(Root, script));
            for (int i = 0; i < lines.Length; i++)
                foreach (var (form, pattern, instead) in GnuOnly)
                    if (pattern.IsMatch(Code(lines[i])))
                        found.Add($"{script}:{i + 1} has {form}, where {instead} reads on both");
        }

        Assert.True(found.Count == 0, "Scripts run on macOS use what only GNU's tools or bash 4 read:\n  " + string.Join("\n  ", found));
    }

    [Theory]
    [InlineData("sed -i \"s/PACKED_VERSION/$version/\" build/templates/content/*/.template.config/template.json", "sed -i")]
    [InlineData("sed -Ei 's/a/b/' file", "sed -i")]
    [InlineData("count=$(grep -oP '\\d+' file)", "grep -P")]
    [InlineData("readarray -t lines < file", "readarray")]
    [InlineData("then=$(date -d yesterday +%s)", "date -d")]
    [InlineData("size=$(stat -c %s file)", "stat -c")]
    [InlineData("sha256sum package.nupkg", "sha256sum")]
    [InlineData("echo \"${game,,}\"", "${x,,}")]
    [InlineData("sed -e 's/a/b/' file > file.new && mv file.new file", null)]
    [InlineData("count=$(grep -oE '[0-9]+' file)", null)]
    [InlineData("now=$(date +%s)", null)]
    [InlineData("size=$(wc -c < file)", null)]
    [InlineData("echo \"${game}\" \"${#games[@]}\"", null)]
    [InlineData("# sed -i is left out, which macOS reads otherwise", null)]
    public void Each_Form_Is_Found_Where_A_Line_Has_It_And_Nowhere_Else(string line, string? form)
    {
        GnuOnly.Where(entry => entry.Pattern.IsMatch(Code(line))).Select(entry => entry.Form).Should().Equal(form is null ? Array.Empty<string>() : new[] { form });
    }

    // The shell scripts the jobs of test.yml that run on Windows or macOS name, and those each of
    // them names in turn, by path from the root.
    private static SortedSet<string> ScriptsRunElsewhere()
    {
        var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "test.yml"));
        var jobs = Regex.Split(workflow, @"^  [\w-]+:\n", RegexOptions.Multiline);
        var pending = new Stack<string>(jobs
            .Where(job => Regex.IsMatch(job, @"^\s+runs-on:\s*(windows|macos)", RegexOptions.Multiline))
            .SelectMany(job => Called(job)));

        var scripts = new SortedSet<string>(StringComparer.Ordinal);
        while (pending.TryPop(out var script))
            if (scripts.Add(script))
                foreach (var line in File.ReadLines(Path.Combine(Root, script)))
                    foreach (var called in Called(Code(line)))
                        pending.Push(called);
        return scripts;
    }

    private static IEnumerable<string> Called(string text) =>
        CalledScript().Matches(text).Select(m => m.Value.StartsWith("./", StringComparison.Ordinal) ? m.Value[2..] : m.Value);

    // A line without its comment, so a script that names a form to say it is left out is not
    // taken as using it. A # inside ${...} counts a length and begins no comment.
    private static string Code(string line) => Regex.Replace(line, @"(^|\s)#.*", "");

    [GeneratedRegex(@"(?<![\w/.])(?:build/[\w.-]+\.sh|\./e3d)\b")]
    private static partial Regex CalledScript();
}

using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Engine.Tests.Scripts;

/// <summary>
/// The shell scripts a workflow runs on Windows or macOS, and those they call, use only what GNU's
/// tools and the BSD ones macOS has both read, and what the bash 3.2 macOS ships reads; and the
/// pack's release notes on a scratch history (<c>build/release-notes.sh</c>).
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

    [NeedsWebPFact]
    public void A_Picture_Of_Another_Shape_Is_Filled_To_The_Size_Given_By_Whichever_Encoder_There_Is()
    {
        // build/webp.sh fits a capture with cwebp where it is installed, as the macOS runner has no
        // ImageMagick, and with ImageMagick where it is not. A square, a four by three and a strip,
        // the shapes captures had before the gallery's were fitted, are each cut from a side or
        // around the middle and scaled to 800 by 450.
        using var folder = new TestFolder("engine-webp-test-");
        foreach (var (width, height, gravity) in new[] { (600, 600, "north"), (640, 480, "center"), (1000, 300, "East") })
        {
            var png = Path.Combine(folder.Path, $"{width}x{height}.png");
            var rgba = new byte[width * height * 4];
            for (int i = 0; i < rgba.Length; i += 4) (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = ((byte)(i / 4 % width), (byte)(i / 4 / width), 128, 255);
            PngWriter.Write(png, rgba, width, height);
            var webp = Path.ChangeExtension(png, ".webp");

            var start = new ProcessStartInfo("bash") { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build/webp.sh", png, webp, "lossy", "800x450", gravity }) start.ArgumentList.Add(argument);
            using var script = Process.Start(start)!;
            var said = script.StandardError.ReadToEnd();
            script.WaitForExit();

            script.ExitCode.Should().Be(0, $"{Probes.WebP.Value} fits {width} by {height} to the size: {said}");
            NormTests.WebPSize(File.ReadAllBytes(webp)).Should().Be((800, 450), $"{Probes.WebP.Value} fills the size given from {width} by {height}");
        }
    }

    // The shell scripts the jobs of build.yml that run on Windows or macOS name, and those each of
    // them names in turn, by path from the root.
    private static SortedSet<string> ScriptsRunElsewhere()
    {
        var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "build.yml"));
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

    // build/release-notes.sh on a scratch history, with nuget.org's flat container given as a
    // folder. The notes start after the commit the newest package of the minor was built from, and
    // where the feed answers nothing, lists no version of the minor or names a commit the history
    // does not have, after the commit that last changed build/version.txt, saying why. A commit
    // whose every file lies under .github/ is left out, as the review's records are.
    [NeedsBashFact]
    public void The_Release_Notes_Start_After_The_Commit_The_Newest_Package_Of_The_Minor_Was_Built_From_Or_Say_Why_Not()
    {
        using var folder = new TestFolder("engine-release-notes-");
        var history = folder.File("history");
        Directory.CreateDirectory(Path.Combine(history, "build"));
        File.Copy(Path.Combine(Root, "build", "release-notes.sh"), Path.Combine(history, "build", "release-notes.sh"));

        // A program run in the scratch history, what it wrote and its exit code.
        (int Code, string Said) Run(string program, string? feed, params string[] arguments)
        {
            var start = new ProcessStartInfo(program) { WorkingDirectory = history, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            start.Environment["GIT_AUTHOR_NAME"] = start.Environment["GIT_COMMITTER_NAME"] = "test";
            start.Environment["GIT_AUTHOR_EMAIL"] = start.Environment["GIT_COMMITTER_EMAIL"] = "test@example.com";
            if (feed is not null) start.Environment["E3D_RELEASE_FEED"] = feed;
            using var process = Process.Start(start)!;
            var said = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, said);
        }

        // A commit of the form COMMITS.md gives, three marks and the sentence, changing a file of its
        // own, the one named or build/version.txt, answering its hash.
        string Commit(string sentence, string? version = null, string file = "work.txt")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(history, file))!);
            if (version is not null) File.WriteAllText(Path.Combine(history, "build", "version.txt"), version + "\n");
            else File.AppendAllText(Path.Combine(history, file), sentence + "\n");
            Run("git", null, "add", "-A").Code.Should().Be(0);
            var made = Run("git", null, "commit", "-q", "-m", "\u200e \u200e \u200e", "-m", sentence);
            made.Code.Should().Be(0, made.Said);
            return Run("git", null, "rev-parse", "HEAD").Said.Trim();
        }

        // The feed's index of the versions and each version's nuspec naming the commit it was built
        // from, as nuget.org's flat container lays them out, at a file:// address.
        string Feed(string name, params (string Version, string Commit)[] versions)
        {
            var feed = folder.File(name);
            Directory.CreateDirectory(feed);
            File.WriteAllText(Path.Combine(feed, "index.json"), $"{{\"versions\":[{string.Join(",", versions.Select(v => $"\"{v.Version}\""))}]}}");
            foreach (var (version, commit) in versions)
            {
                Directory.CreateDirectory(Path.Combine(feed, version));
                File.WriteAllText(Path.Combine(feed, version, "3dengine.nuspec"),
                    $"<package><metadata><id>3DEngine</id><version>{version}</version><repository type=\"git\" url=\"https://github.com/EggyStudio/3DEngine\" commit=\"{commit}\" /></metadata></package>");
            }
            return new Uri(feed).AbsoluteUri;
        }

        (string Said, string[] Notes) Notes(string feed)
        {
            var (code, said) = Run("bash", feed, "build/release-notes.sh", "notes.txt");
            code.Should().Be(0, said);
            return (said, File.ReadAllLines(Path.Combine(history, "notes.txt")));
        }

        Run("git", null, "init", "-q").Code.Should().Be(0);
        var older = Commit("The minor before.", "5.1");
        Commit("The minor is raised.", "5.2");
        Commit("The first change.");
        var packed = Commit("The second change.");
        Commit("The third change.");
        Commit("The review records the third change.", file: ".github/REVIEW.md");
        Commit("The workflow builds it.", file: ".github/workflows/build.yml");
        string[] since = ["The third change.", "The second change.", "The first change."];

        // 5.2.10 is the newest, read as a number where as text 5.2.9 would sort after it.
        var (said, notes) = Notes(Feed("feed", ("5.1.4", older), ("5.2.9", older), ("5.2.10", packed)));
        notes.Should().Equal(["The third change."], said);
        said.Should().Contain($"after {packed}, where 5.2.10 was built");

        (said, notes) = Notes(Feed("older", ("5.1.4", older)));
        notes.Should().Equal(since, said);
        said.Should().Contain("since the feed lists no version of 5.2");

        (said, notes) = Notes(new Uri(folder.File("none")).AbsoluteUri);
        notes.Should().Equal(since, said);
        said.Should().Contain("since the feed answered nothing");

        var elsewhere = new string('a', 40);
        (said, notes) = Notes(Feed("elsewhere", ("5.2.3", elsewhere)));
        notes.Should().Equal(since, said);
        said.Should().Contain($"since 5.2.3 was built from {elsewhere}, which this checkout does not have");
    }
}

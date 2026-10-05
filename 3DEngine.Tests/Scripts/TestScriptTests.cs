using System.Diagnostics;
using System.Text;
using FluentAssertions;

namespace Engine.Tests.Scripts;

/// <summary>
/// <c>build/test.py</c>, which runs the suite in the workflow and for a working session, writes a
/// page within its limits whatever the run held, and says a process that is lost and runs the suite
/// again in parts after it.
/// </summary>
/// <remarks>
/// The path taken after a loss runs on no day the suite passes, so these tests keep it working. A
/// stand-in for <c>dotnet</c> (<c>dotnet_standin.py</c>) hangs, grows or dies where the suite would
/// run whole and passes its parts, so they need no <c>dotnet</c> and no suite.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class TestScriptTests : IDisposable
{
    private static readonly string Root = Api.CheatsheetTests.RepoRoot();
    private readonly TestFolder _folder = new("engine-test-script-");

    public void Dispose() => _folder.Dispose();

    private static readonly string[] Causes =
        ["alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel", "india", "juliett", "kilo", "lima"];

    [NeedsPythonFact]
    public void A_Page_Of_500_Failures_Of_12_Causes_Beside_100000_Lines_Of_Output_Keeps_To_Its_Limits()
    {
        // The most frequent cause first, the last two past the ten a page shows.
        int[] counts = [100, 80, 70, 60, 50, 40, 30, 25, 20, 12, 8, 5];
        var results = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?><TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>");
        int test = 0;
        for (int cause = 0; cause < Causes.Length; cause++)
            for (int i = 0; i < counts[cause]; i++, test++)
                results.Append($"<UnitTestResult testName=\"Engine.Tests.Area.Class{test % 7}.Test_{test}\" outcome=\"Failed\"><Output><ErrorInfo>")
                    .Append($"<Message>System.InvalidOperationException : the {Causes[cause]} system broke on frame {test} reading /tmp/run{test}/state.bin")
                    .Append(string.Concat(Enumerable.Range(1, 7).Select(line => $"\n  a place it names, {line} of 7")))
                    .Append("</Message>")
                    .Append($"<StackTrace>   at Engine.{char.ToUpperInvariant(Causes[cause][0])}{Causes[cause][1..]}System.Run(Int32 frame) in /src/Systems/Run.cs:line {test}\n")
                    .Append($"   at Engine.Tests.Area.Class{test % 7}.Test_{test}() in /src/Tests/Class.cs:line 12</StackTrace>")
                    .Append("</ErrorInfo></Output></UnitTestResult>");
        results.Append("</Results></TestRun>");
        File.WriteAllText(_folder.File("results.trx"), results.ToString());

        var output = new StringBuilder();
        for (int i = 0; i < 100_000; i++)
            output.AppendLine(i % 5 < 3 ? $"[ {i * 0.016:0.0000}s] [ERROR] [Engine.Schedule] A system threw on frame {i}" : $"[INFO ] read asset {i} of the level");
        File.WriteAllText(_folder.File("output.txt"), output.ToString());

        var (exit, _) = Script("--read", _folder.Path);

        exit.Should().Be(1, "tests failed");
        var page = File.ReadAllLines(_folder.File("digest.md"));
        page.Length.Should().BeLessThanOrEqualTo(200);
        page.Should().OnlyContain(line => line.Length <= 240);
        var text = string.Join("\n", page);
        text.Should().Contain("500 failed, of 12 causes");
        foreach (var cause in Causes[..10]) text.Should().Contain($"the {cause} system broke", "each of the first ten causes is named");
        text.Should().NotContain("the kilo system").And.NotContain("the lima system");
        text.Should().Contain("**100 × System.InvalidOperationException** at `Engine.AlphaSystem.Run`", "a cause counts the tests it failed, with the engine's first frame");
        text.Should().Contain("60,000 ×", "a line the output repeats with its numbers changing is one line with its count");
        text.Should().Contain("a place it names, 4 of 7").And.NotContain("a place it names, 5 of 7").And.Contain("(3 lines more)",
            "a cause shows the first five lines of its message and counts the rest");
        File.Exists(_folder.File("digest.json")).Should().BeTrue();

        // As a run on GitHub gives them, where the annotations are all a reader who is not signed
        // in sees: ten errors, each a cause whole, and a notice with the head and the repeated lines.
        var (_, annotated) = Script(new Dictionary<string, string> { ["GITHUB_ACTIONS"] = "true", ["GITHUB_STEP_SUMMARY"] = _folder.File("summary.md") }, "--read", _folder.Path);
        var lines = annotated.Split('\n');
        var errors = lines.Where(line => line.StartsWith("::error ", StringComparison.Ordinal)).ToList();
        errors.Should().HaveCount(10);
        errors.Should().OnlyContain(line => line.Contains("%0Aat Engine.", StringComparison.Ordinal) && line.Contains("`Engine.Tests.Area.Class", StringComparison.Ordinal),
            "each carries its cause's frames and tests");
        lines.Should().ContainSingle(line => line.StartsWith("::notice ", StringComparison.Ordinal) && line.Contains("500 failed") && line.Contains("60,000 ×"));
        File.ReadAllText(_folder.File("summary.md")).Should().Contain("500 failed, of 12 causes", "the page is the job's summary");
    }

    [NeedsPythonTheory]
    [InlineData("hang", "ended at its time limit")]
    [InlineData("grow", "ended at its memory limit")]
    [InlineData("die", "lost to a crash")]
    public void A_Lost_Process_Is_Said_First_And_The_Suite_Runs_Again_In_Parts(string mode, string said)
    {
        var (exit, log) = Script(mode, "--dotnet", $"{Probes.Python.Value} 3DEngine.Tests/Scripts/dotnet_standin.py",
            "--results", _folder.Path, "--timeout-minutes", "0.1", "--memory-mb", "200");

        exit.Should().Be(1, "a process was lost");
        var page = File.ReadAllLines(_folder.File("digest.md"));
        page[0].Should().Contain("80 passed, 0 failed, 0 skipped, 0 without a result", "the parts ran every test the suite lists");
        page.First(line => line.StartsWith("### ", StringComparison.Ordinal)).Should().StartWith($"### Lost: the suite, {said}");
        page.Should().Contain(line => line.StartsWith("- Api: 35 passed", StringComparison.Ordinal))
            .And.Contain(line => line.StartsWith("- Rendering: 40 passed", StringComparison.Ordinal))
            .And.Contain(line => line.StartsWith("- everything else: 5 passed", StringComparison.Ordinal));
        log.TrimEnd().Should().EndWith("end of the page " + new string('=', 30), "the page ends the log");
    }

    // Runs build/test.py with the arguments given, the first being E3D_STANDIN's value where it
    // is not an option, and returns its exit code and what it printed.
    private static (int Exit, string Log) Script(params string[] arguments) => Script(new Dictionary<string, string>(), arguments);

    private static (int Exit, string Log) Script(Dictionary<string, string> environment, params string[] arguments)
    {
        var start = new ProcessStartInfo(Probes.Python.Value!) { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine("build", "test.py"));
        var rest = arguments.AsSpan();
        if (!arguments[0].StartsWith("--", StringComparison.Ordinal))
        {
            start.Environment["E3D_STANDIN"] = arguments[0];
            rest = rest[1..];
        }
        foreach (var argument in rest) start.ArgumentList.Add(argument);
        start.Environment.Remove("GITHUB_ACTIONS");
        start.Environment.Remove("GITHUB_STEP_SUMMARY");
        foreach (var (name, value) in environment) start.Environment[name] = value;

        using var script = Process.Start(start)!;
        var log = script.StandardOutput.ReadToEndAsync();
        var errors = script.StandardError.ReadToEndAsync();
        script.WaitForExit(120_000).Should().BeTrue("the script ends its processes at their limits");
        return (script.ExitCode, log.Result + errors.Result);
    }
}

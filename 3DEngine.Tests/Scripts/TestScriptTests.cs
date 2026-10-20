using System.Diagnostics;
using System.Text;
using FluentAssertions;

namespace Engine.Tests.Scripts;

/// <summary>
/// <c>build/test.py</c>, which runs the suite in the workflow and on a contributor's machine, writes a
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

        // Each line ended as on Windows, whose output the page is read from as well.
        var output = new StringBuilder();
        for (int i = 0; i < 100_000; i++)
            output.Append(i % 5 < 3 ? $"[ {i * 0.016:0.0000}s] [ERROR] [Engine.Schedule] A system threw on frame {i}" : $"[INFO ] read asset {i} of the level").Append("\r\n");
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
        // in sees, ten errors, each a cause whole, and a notice with the head and the repeated lines.
        var (_, annotated) = Script(new Dictionary<string, string> { ["GITHUB_ACTIONS"] = "true", ["GITHUB_STEP_SUMMARY"] = _folder.File("summary.md") }, "--read", _folder.Path);
        var lines = Lines(annotated);
        var errors = lines.Where(line => line.StartsWith("::error ", StringComparison.Ordinal)).ToList();
        errors.Should().HaveCount(10);
        errors.Should().OnlyContain(line => line.Contains("%0Aat Engine.", StringComparison.Ordinal) && line.Contains("`Engine.Tests.Area.Class", StringComparison.Ordinal),
            "each carries its cause's frames and tests");
        lines.Should().ContainSingle(line => line.StartsWith("::notice ", StringComparison.Ordinal) && line.Contains("500 failed") && line.Contains("60,000 ×"));
        File.ReadAllText(_folder.File("summary.md")).Should().Contain("500 failed, of 12 causes", "the page is the job's summary");
    }

    [NeedsPythonFact]
    public void The_Repeated_Lines_Are_Warnings_Errors_And_Lines_Of_No_Level_And_None_Leaves_The_Section_Out()
    {
        File.WriteAllText(_folder.File("results.trx"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>"
            + "<UnitTestResult testName=\"Engine.Tests.Area.Class.Test\" outcome=\"Passed\" /></Results></TestRun>");
        // The engine's banner, as each app's start logs it, and an info line of no use repeated.
        var banners = new StringBuilder();
        for (int i = 0; i < 2190; i++)
            banners.AppendLine($"[ {i * 0.016:0.0000}s] [INFO ] [Engine] {new string('=', 56)}")
                .AppendLine($"[ {i * 0.016:0.0000}s] [DEBUG] [Engine.Assets] read asset {i} of the level");
        File.WriteAllText(_folder.File("output.txt"), banners.ToString());

        Script("--read", _folder.Path);
        File.ReadAllText(_folder.File("digest.md")).Should().NotContain("Repeated most", "no line logged as a warning or an error repeated");

        var errors = new StringBuilder(banners.ToString());
        for (int frame = 0; frame < 3; frame++)
            errors.AppendLine($"[ {frame * 0.016:0.0000}s] [ERROR] [Engine.Schedule] System 'Spin' from Game threw on frame {frame}")
                .AppendLine("System.InvalidOperationException: the spin has no wheel");
        File.WriteAllText(_folder.File("output.txt"), errors.ToString());

        Script("--read", _folder.Path);
        var page = File.ReadAllText(_folder.File("digest.md"));
        // Each repeated line shown as the last of its kind, so one that counts as it goes says how
        // far it got.
        page.Should().Contain("### Repeated most in the output")
            .And.Contain("- 3 × `[ 0.0320s] [ERROR] [Engine.Schedule] System 'Spin' from Game threw on frame 2`")
            .And.Contain("- 3 × `System.InvalidOperationException: the spin has no wheel`", "a line with no level, as an exception's, counts as well")
            .And.NotContain("====").And.NotContain("read asset");
    }

    /// <summary>
    /// Two cases of a theory whose arguments are cut to the same name are both counted, as the
    /// suite's own tally counts them, and the same cases read again from a part are not, as
    /// BevyCSharp's 1f68fde8 counts them.
    /// </summary>
    [NeedsPythonFact]
    public void Two_Cases_Of_A_Theory_Cut_To_One_Name_Are_Counted_Apart()
    {
        const string cut = "Engine.Tests.Scripts.ScriptTests.Compiles(script: &quot;[Behavior] public partial struct&quot;···)";
        var cases = $"<UnitTestResult testName=\"{cut}\" outcome=\"Passed\" /><UnitTestResult testName=\"{cut}\" outcome=\"Passed\" />";
        foreach (var file in new[] { "results-the-suite.trx", "results-ScriptTests.trx" })
            File.WriteAllText(_folder.File(file),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>"
                + cases + "</Results></TestRun>");

        Script("--read", _folder.Path);
        File.ReadAllLines(_folder.File("digest.md"))[0].Should().Contain("2 passed, 0 failed, 0 skipped");
    }

    [NeedsPythonTheory]
    [InlineData("hang", "ended at its time limit")]
    [InlineData("grow", "ended at its memory limit")]
    [InlineData("die", "lost to a crash")]
    [InlineData("die-unsaid", "lost to a crash")]
    public void A_Lost_Process_Is_Said_First_And_The_Suite_Runs_Again_In_Parts(string mode, string said)
    {
        // dotnet-dump's stand-in reads the minidump a death leaves. The growing stand-in passes the
        // memory limit in about a second here, and on a slow macOS runner it held 196 MB of the 200
        // when the six seconds the hang is given ran out, so the page said the time limit. It is
        // given a minute, so the memory limit comes first on any runner.
        var minutes = mode == "grow" ? "1" : "0.1";
        var (exit, log) = Script(new Dictionary<string, string> { ["E3D_DOTNET_DUMP"] = $"{Probes.Python.Value} 3DEngine.Tests/Scripts/dotnet_dump_standin.py" },
            mode, "--dotnet", $"{Probes.Python.Value} 3DEngine.Tests/Scripts/dotnet_standin.py",
            "--results", _folder.Path, "--timeout-minutes", minutes, "--memory-mb", "200");

        exit.Should().Be(mode == "hang" ? 0 : 1, mode == "hang" ? "the parts gave every test of a run lost to its time a result" : "a process was lost to a fault");
        var page = File.ReadAllLines(_folder.File("digest.md"));
        page[0].Should().Contain("80 passed, 0 failed, 0 skipped, 0 without a result", "the parts ran every test the suite lists");
        page.First(line => line.StartsWith("### ", StringComparison.Ordinal)).Should().StartWith($"### Lost: the suite, {said}");
        page.Should().Contain(line => line.StartsWith("- Api: 35 passed", StringComparison.Ordinal))
            .And.Contain(line => line.StartsWith("- Rendering: 40 passed", StringComparison.Ordinal))
            .And.Contain(line => line.StartsWith("- everything else: 5 passed", StringComparison.Ordinal));
        log.TrimEnd().Should().EndWith("end of the page " + new string('=', 30), "the page ends the log");
        if (mode == "die")
        {
            // createdump says the thread the crash came on and its signal, which the dump is read at.
            page.Should().Contain("    dotnet-dump read `testhost-4242.dmp`, its crash on thread OS id 0x1a2e on signal 11, SIGSEGV, a read or write of memory not mapped, "
                    + "as createdump said, a thread the runtime does not run, as a driver's or a native library's",
                    "the thread createdump names is read, not the one dotnet-dump marks current")
                .And.Contain("    at libMoltenVK.dylib!MVKBuffer::flushToDevice")
                .And.Contain("    at libMoltenVK.dylib!MVKQueueSubmission::execute", "with its native frames and their modules");
            File.ReadAllText(Path.Combine(_folder.Path, "dumps", "testhost-4242.dmp.txt")).Should().Contain("> setthread --tid 6702 ; clrstack -f");
        }
        if (mode == "die-unsaid")
        {
            page.Should().Contain("The test had got as far as `[leak test] app 37 of 100`", "a test's own progress says where a crash came")
                .And.Contain("It left the minidump `testhost-4242.dmp` among the results, under `dumps`");
            page.Should().Contain("    dotnet-dump read `testhost-4242.dmp`, written for thread 1, OS id 0x1a2c, a thread the runtime does not run, as a driver's or a native library's",
                    "with no word from createdump the dump is read at the thread it was written for")
                .And.Contain("    It holds no managed frames, and the threads that do were in")
                .And.Contain("      0x1a2b: 3DEngine.dll!Engine.GraphicsDevice.DestroyLogicalDevice() in GraphicsDevice.Device.cs:204"
                    + " < 3DEngine.dll!Engine.GraphicsDevice.Dispose() in GraphicsDevice.cs:202 < 3DEngine.dll!Engine.Renderer.Dispose()",
                    "a fault on a thread of no managed code says what each managed thread was in");
            page.Should().NotContain(line => line.Contains("0x1a2d:"), "a thread with no frames of its own is left out");
            File.ReadAllText(Path.Combine(_folder.Path, "dumps", "testhost-4242.dmp.txt")).Should().Contain("> clrstack -all -f",
                "and the whole of what it read is kept beside the dump");
        }
    }

    [NeedsPythonFact]
    public void A_Whole_Run_Lost_Near_Its_End_Runs_Again_Only_The_Parts_Holding_Tests_It_Gave_No_Result()
    {
        // A whole run that ends every test of Api and NormTests, one of them failing, and ten of
        // Rendering's forty, before its time runs out, as a Windows runner of a slow processor ran
        // out its hour 1,705 tests in, and every part run again from the start took the job past its
        // limit with no page written.
        var (exit, log) = Script("most", "--dotnet", $"{Probes.Python.Value} 3DEngine.Tests/Scripts/dotnet_standin.py",
            "--results", _folder.Path, "--timeout-minutes", "0.1");

        exit.Should().Be(1, "a process was lost and a test failed");
        var page = File.ReadAllLines(_folder.File("digest.md"));
        page[0].Should().Contain("79 passed, 1 failed, 0 skipped, 0 without a result", "the tests that ended in the whole run count as they ended");
        Lines(log).Should().Contain("Api: every test ended in the whole run, so it is not run again")
            .And.Contain("everything else: every test ended in the whole run, so it is not run again");
        page.Should().Contain(line => line.StartsWith("- Rendering: 40 passed", StringComparison.Ordinal), "the part holding tests without a result runs again")
            .And.NotContain(line => line.StartsWith("- Api:", StringComparison.Ordinal));
        page.SkipWhile(line => line != "Its slowest tests that ended:").Skip(1).Take(5).Should().Equal(
            "    `Engine.Tests.Rendering.Drawing.Draws_9` 1 m 3 s", "    `Engine.Tests.Rendering.Drawing.Draws_8` 56 s",
            "    `Engine.Tests.Rendering.Drawing.Draws_7` 49 s", "    `Engine.Tests.Rendering.Drawing.Draws_6` 42 s",
            "    `Engine.Tests.Rendering.Drawing.Draws_5` 35 s");
        string.Join("\n", page).Should().Contain("1 failed, of 1 cause").And.Contain("Expected the rule to hold, but it did not.",
            "a failure the whole run printed is counted with its message");
    }

    [NeedsPythonFact]
    public void A_Whole_Run_Lost_To_Its_Time_Whose_Parts_Give_Every_Test_A_Passing_Result_Passes_With_A_Notice()
    {
        // As the pack run of 448aa3fc's Windows suite ran out its hour 1,302 tests in, its parts
        // passed every test and the step ended with exit code 1, so the pack job was skipped on a
        // suite with nothing failing.
        var (exit, log) = Script(new Dictionary<string, string> { ["GITHUB_ACTIONS"] = "true", ["GITHUB_STEP_SUMMARY"] = _folder.File("summary.md") },
            "hang", "--dotnet", $"{Probes.Python.Value} 3DEngine.Tests/Scripts/dotnet_standin.py", "--results", _folder.Path, "--timeout-minutes", "0.1");

        exit.Should().Be(0, "every test has a result and none failed");
        var lines = Lines(log);
        lines.Should().NotContain(line => line.StartsWith("::error ", StringComparison.Ordinal), "a run lost to its time alone is no failure");
        lines.Should().ContainSingle(line => line.StartsWith("::notice title=Lost to its time", StringComparison.Ordinal)).Which.Should()
            .Contain("the suite").And.Contain("ended at its time limit", "the loss is said with its figures");
    }

    [NeedsPythonFact]
    public void A_Suite_Run_Again_Shuffled_By_A_Seed_Names_The_Seed_Where_It_Fails()
    {
        var (exit, log) = Script(new Dictionary<string, string> { ["GITHUB_ACTIONS"] = "true", ["GITHUB_STEP_SUMMARY"] = _folder.File("summary.md") },
            "shuffle-fails", "--dotnet", $"{Probes.Python.Value} 3DEngine.Tests/Scripts/dotnet_standin.py", "--results", _folder.Path, "--shuffle-seed", "20261020");

        exit.Should().Be(1, "the shuffled run failed a test");
        File.ReadAllLines(_folder.File("digest.md"))[0].Should().Contain("80 passed, 0 failed", "the run in the order systems were added passed");
        var page = File.ReadAllLines(Path.Combine(_folder.Path, "shuffled", "digest.md"));
        page[0].Should().StartWith("## Tests on").And.Contain("shuffled by the seed 20261020").And.Contain("79 passed, 1 failed");
        page.Should().Contain("Each stage's systems were shuffled by the seed 20261020, and `build/test.py --shuffle-seed 20261020` runs the suite in the same order again.");
        Lines(log).Should().Contain(line => line.StartsWith("::error title=Shuffled by the seed 20261020::", StringComparison.Ordinal)
            && line.Contains("build/test.py --shuffle-seed 20261020"), "the annotation names the seed to run again");
    }

    [NeedsPythonFact]
    public void A_Suite_That_Failed_In_The_Order_Added_Is_Not_Run_Again_Shuffled()
    {
        var (exit, log) = Script("most", "--dotnet", $"{Probes.Python.Value} 3DEngine.Tests/Scripts/dotnet_standin.py",
            "--results", _folder.Path, "--timeout-minutes", "0.1", "--shuffle-seed", "7");

        exit.Should().Be(1);
        Lines(log).Should().Contain("The suite failed in the order its systems were added, so it is not run again shuffled by the seed 7.");
        Directory.Exists(Path.Combine(_folder.Path, "shuffled")).Should().BeFalse();
    }

    // Runs build/test.py with the arguments given, the first being E3D_STANDIN's value where it
    // is not an option, and returns its exit code and what it printed.
    private static (int Exit, string Log) Script(params string[] arguments) => Script(new Dictionary<string, string>(), arguments);

    private static (int Exit, string Log) Script(Dictionary<string, string> environment, params string[] arguments)
    {
        var start = Utf8(new ProcessStartInfo(Probes.Python.Value!) { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true });
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

    /// <summary>
    /// A Python script's output read as the UTF-8 the scripts write, where a Windows console's own
    /// code page would read the page's "×" as other characters.
    /// </summary>
    internal static ProcessStartInfo Utf8(ProcessStartInfo start)
    {
        start.StandardOutputEncoding = start.StandardErrorEncoding = new UTF8Encoding(false);
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        return start;
    }

    /// <summary>A script's output as lines, each without the carriage return Windows ends it with.</summary>
    internal static string[] Lines(string output) => output.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
}

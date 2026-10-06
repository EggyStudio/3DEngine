using System.Diagnostics;
using FluentAssertions;

namespace Engine.Tests.Scripts;

/// <summary>
/// <c>build/step.py</c>, which runs each step of the build workflow's examples job, says what failed
/// in a step that fails having said nothing, and <c>build/raylib-bench/compare.py</c> says the pairs
/// it measured for the first time where a reader who is not signed in sees them (REVIEW.md, Verdict 28).
/// </summary>
/// <remarks>
/// Three examples jobs in a row ended with <c>Process completed with exit code 4</c> and nothing
/// else, from <c>status=$(./e3d command manor.status)</c>, whose error went into the variable. The
/// lines these write are cut to the test page's width by <c>build/page.py</c>, which both share.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class StepScriptTests : IDisposable
{
    private static readonly string Root = Api.CheatsheetTests.RepoRoot();
    private readonly TestFolder _folder = new("engine-step-script-");

    public StepScriptTests() => Directory.CreateDirectory(_folder.File("sessions"));

    public void Dispose() => _folder.Dispose();

    private const string Workflow = """
        jobs:
          examples:
            steps:
              - uses: actions/checkout@v7

              - run: build/fetch-slang.sh

              # The walk.
              - name: Build and walk the first-person game from the package
                env:
                  ENGINE_VULKAN_VALIDATION: '1'
                run: |
                  echo opening
                  cp manor.log sessions/Manor.log
                  pad() { sh -c 'exit 3'; }
                  ok=$(echo fine)
                  echo "$ok"
                  status=$(sh -c 'echo "error [NO_SESSION] No app is serving." >&2; exit 4')
                  echo never

              - name: Pad
                run: |
                  pad() { sh -c 'exit 3'; }
                  pad South
        """;

    [NeedsPythonAndBashFact]
    public void A_Step_Whose_Command_Fails_Into_A_Variable_Is_Named_With_The_Command_Its_Code_And_The_Logs_Warnings()
    {
        // Written by the step, as the app it opens writes it, so it is a log of the step's own.
        File.WriteAllLines(_folder.File("manor.log"),
        [
            "[    0.29s] [INFO ] [Engine] opening",
            "[    0.30s] [WARN ] [Engine.Sound.Sdl] no audio device, so sound goes to SDL's dummy driver",
            "[    3.10s] [FATAL] [Engine.Application] Unhandled exception: " + new string('x', 400),
        ]);
        var (exit, log, summary) = Step(StepOf("Build and walk the first-person game from the package"));

        exit.Should().Be(4, "the step ends with the code of the command that failed");
        log.Should().Contain("fine").And.NotContain("never", "what the step prints is passed on as it comes, and it ends at the first command that fails");
        var error = log.Split('\n').Should().ContainSingle(line => line.StartsWith("::error ", StringComparison.Ordinal)).Subject;
        error.Should().StartWith("::error title=Build and walk the first-person game from the package%3A exit code 4::",
            "the step is named as the workflow names it");
        error.Should().Contain("`status=$(sh -c 'echo \"error [NO_SESSION] No app is serving.\" >&2; exit 4')` on line 6 of the step ended with exit code 4.")
            .And.Contain("%0A    error [NO_SESSION] No app is serving.", "the step's last lines carry the error the variable took nothing of")
            .And.Contain("Manor.log, its last lines at a warning or worse:%0A    [WARN ] [Engine.Sound.Sdl] no audio device")
            .And.NotContain("[INFO ]");
        error.Split("%0A").Should().OnlyContain(line => line.Length <= 240 + 120, "a line of the error is cut to the page's width");
        summary.Should().Contain("### Build and walk the first-person game from the package: exit code 4", "the error is the job's summary too");
    }

    [NeedsPythonAndBashFact]
    public void A_Function_That_Fails_Is_Named_And_A_Step_That_Says_Its_Own_Error_Is_Given_None()
    {
        var (exit, log, _) = Step(StepOf("Pad"));
        exit.Should().Be(3);
        log.Should().Contain("::error title=Pad%3A exit code 3::`sh -c 'exit 3'` on line 1 of the step ended with exit code 3.",
            "a function's command fails inside it, and the step's line is the function's");

        File.WriteAllText(_folder.File("said.sh"), "echo \"::error::Manor: the autopilot did not find every lantern\"\nexit 1\n");
        (exit, log, _) = Step(_folder.File("said.sh"));
        exit.Should().Be(1);
        log.Split('\n').Should().ContainSingle(line => line.StartsWith("::error", StringComparison.Ordinal), "the step said what failed itself");

        File.WriteAllText(_folder.File("passes.sh"), "echo walked\n");
        (exit, log, _) = Step(_folder.File("passes.sh"));
        exit.Should().Be(0);
        log.Trim().Should().Be("walked");
    }

    [NeedsPythonAndBashFact]
    public void A_Step_Not_In_The_Workflow_Is_Named_By_Its_First_Line_And_One_With_No_Name_As_GitHub_Names_It()
    {
        File.WriteAllText(_folder.File("slang.sh"), "build/fetch-slang.sh\n");
        var (exit, log, _) = Step(_folder.File("slang.sh"));
        exit.Should().Be(127, "no such command is found");
        log.Should().Contain("::error title=Run build/fetch-slang.sh%3A exit code 127::");

        File.WriteAllText(_folder.File("elsewhere.sh"), "echo first\nexit 5\n");
        (_, log, _) = Step(_folder.File("elsewhere.sh"));
        log.Should().Contain("::error title=The step beginning `echo first`%3A exit code 5::The step ended with exit code 5 by its own exit, no command failing.");
    }

    [NeedsPythonFact]
    public void The_Pairs_Measured_For_The_First_Time_Are_No_More_Than_Ten_Notices_Holding_Every_Pair()
    {
        var names = Enumerable.Range(0, 215).Select(i => $"shapes_example_{i}").ToArray();
        var program = "import compare\n"
                      + $"names = [{string.Join(", ", names.Select(n => $"'{n}'"))}]\n"
                      + "compare.notices(names, {name: '1.5' for name in names})\n";
        var start = new ProcessStartInfo(Probes.Python.Value!) { WorkingDirectory = Path.Combine(Root, "build", "raylib-bench"), RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(program);
        start.Environment["GITHUB_ACTIONS"] = "true";
        start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        using var python = Process.Start(start)!;
        var output = python.StandardOutput.ReadToEndAsync();
        var errors = python.StandardError.ReadToEndAsync();
        python.WaitForExit(60_000).Should().BeTrue();
        python.ExitCode.Should().Be(0, errors.Result);

        var notices = output.Result.Split('\n').Where(line => line.StartsWith("::notice ", StringComparison.Ordinal)).ToList();
        notices.Should().HaveCount(10, "GitHub shows ten notices of a step");
        notices[0].Should().StartWith("::notice title=Measured for the first time%2C 1 of 10::shapes_example_0\t1.5%0A");
        var pairs = notices.SelectMany(line => line[(line.IndexOf("::", 9, StringComparison.Ordinal) + 2)..].Split("%0A")).ToList();
        pairs.Should().Equal(names.Select(name => $"{name}\t1.5"), "every pair is in one notice, in order");
    }

    // Runs build/step.py on the script, as GitHub runs it, against the workflow above and the
    // folder's session logs, and returns its exit code, what it printed and the summary it wrote.
    private (int Exit, string Log, string Summary) Step(string script)
    {
        File.WriteAllText(_folder.File("build.yml"), Workflow);
        var start = new ProcessStartInfo(Probes.Python.Value!) { WorkingDirectory = _folder.Path, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { Path.Combine(Root, "build", "step.py"), script, "--workflow", _folder.File("build.yml"), "--sessions", _folder.File("sessions") })
            start.ArgumentList.Add(argument);
        start.Environment["GITHUB_ACTIONS"] = "true";
        start.Environment["GITHUB_STEP_SUMMARY"] = _folder.File("summary.md");
        using var step = Process.Start(start)!;
        var log = step.StandardOutput.ReadToEndAsync();
        var errors = step.StandardError.ReadToEndAsync();
        step.WaitForExit(60_000).Should().BeTrue();
        var summary = File.Exists(_folder.File("summary.md")) ? File.ReadAllText(_folder.File("summary.md")) : "";
        return (step.ExitCode, log.Result.Replace("\r", "") + errors.Result, summary);
    }

    // The script of the named step, as GitHub writes it to a file to run.
    private string StepOf(string name)
    {
        var lines = Workflow.Split('\n');
        var at = Array.FindIndex(lines, line => line.Trim() == $"- name: {name}");
        var run = Array.FindIndex(lines, at, line => line.Trim() == "run: |");
        var indent = lines[run].Length - lines[run].TrimStart().Length;
        var block = lines.Skip(run + 1).TakeWhile(line => line.Trim().Length == 0 || line.Length - line.TrimStart().Length > indent).ToList();
        var depth = block.Where(line => line.Trim().Length > 0).Min(line => line.Length - line.TrimStart().Length);
        var path = _folder.File(name.Replace(' ', '-') + ".sh");
        File.WriteAllText(path, string.Join("\n", block.Select(line => line.Length >= depth ? line[depth..] : "")).TrimEnd() + "\n");
        return path;
    }
}

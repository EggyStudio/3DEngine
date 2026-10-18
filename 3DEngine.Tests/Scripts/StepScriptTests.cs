using System.Diagnostics;
using FluentAssertions;

namespace Engine.Tests.Scripts;

/// <summary>
/// <c>build/step.py</c>, which runs each step of the Linux jobs on the package, says what failed
/// in a step that fails having said nothing, and <c>build/raylib-bench/compare.py</c> says the pairs
/// it measured for the first time and the pairs that fail where a reader who is not signed in sees
/// them (REVIEW.md, Verdicts 28 and 66).
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
        var error = TestScriptTests.Lines(log).Should().ContainSingle(line => line.StartsWith("::error ", StringComparison.Ordinal)).Subject;
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
        TestScriptTests.Lines(log).Should().ContainSingle(line => line.StartsWith("::error", StringComparison.Ordinal), "the step said what failed itself");

        File.WriteAllText(_folder.File("passes.sh"), "echo walked\n");
        (exit, log, _) = Step(_folder.File("passes.sh"));
        exit.Should().Be(0);
        log.Trim().Should().Be("walked");
    }

    [NeedsPythonAndBashFact]
    public void A_Step_Not_In_The_Workflow_Is_Named_By_Its_First_Line_And_One_With_No_Name_As_GitHub_Names_It()
    {
        File.WriteAllText(_folder.File("slang.sh"), "build/fetch-slang.sh\n");
        // No such command is found, whose code is the shell's, 127 from bash 5 and 1 from the bash
        // 3.2 macOS has, so the error is held to saying the one the step ended with.
        var (exit, log, _) = Step(_folder.File("slang.sh"));
        exit.Should().NotBe(0);
        log.Should().Contain($"::error title=Run build/fetch-slang.sh%3A exit code {exit}::");

        File.WriteAllText(_folder.File("elsewhere.sh"), "echo first\nexit 5\n");
        (_, log, _) = Step(_folder.File("elsewhere.sh"));
        log.Should().Contain("::error title=The step beginning `echo first`%3A exit code 5::The step ended with exit code 5 by its own exit, no command failing.");
    }

    [NeedsPythonFact]
    public void A_Measure_Where_Raylib_Drew_No_Frame_For_Any_Pair_Says_Why_The_First_Drew_None()
    {
        // The examples job of 22bbf15a, where every program failed to load SDL and every pair was
        // recorded as having no frame (REVIEW.md, Verdict 31).
        var program = "import compare\n"
                      + "print('\\n'.join(compare.nothing_drawn(('core_basic_window', ['it ended with 127', "
                      + "'error while loading shared libraries: libSDL3.so.0: cannot open shared object file']), 221)))\n";
        var start = TestScriptTests.Utf8(new ProcessStartInfo(Probes.Python.Value!) { WorkingDirectory = Path.Combine(Root, "build", "raylib-bench"), RedirectStandardOutput = true, RedirectStandardError = true });
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(program);
        start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        using var python = Process.Start(start)!;
        var output = python.StandardOutput.ReadToEndAsync();
        var errors = python.StandardError.ReadToEndAsync();
        python.WaitForExit(60_000).Should().BeTrue();
        python.ExitCode.Should().Be(0, errors.Result);

        TestScriptTests.Lines(output.Result.TrimEnd()).Should().Equal(
            "raylib's program drew no frame for any of the 221 pairs, so the measure saw nothing and records nothing.",
            "The first, core_basic_window, drew none because:",
            "  it ended with 127",
            "  error while loading shared libraries: libSDL3.so.0: cannot open shared object file");
    }

    [NeedsPythonFact]
    public void The_Pairs_Measured_For_The_First_Time_Are_No_More_Than_Nine_Notices_Holding_Every_Pair()
    {
        var names = Enumerable.Range(0, 215).Select(i => $"shapes_example_{i}").ToArray();
        var program = "import compare\n"
                      + $"names = [{string.Join(", ", names.Select(n => $"'{n}'"))}]\n"
                      + "compare.notices(names, {name: '1.5' for name in names})\n";
        var start = TestScriptTests.Utf8(new ProcessStartInfo(Probes.Python.Value!) { WorkingDirectory = Path.Combine(Root, "build", "raylib-bench"), RedirectStandardOutput = true, RedirectStandardError = true });
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(program);
        start.Environment["GITHUB_ACTIONS"] = "true";
        start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        using var python = Process.Start(start)!;
        var output = python.StandardOutput.ReadToEndAsync();
        var errors = python.StandardError.ReadToEndAsync();
        python.WaitForExit(60_000).Should().BeTrue();
        python.ExitCode.Should().Be(0, errors.Result);

        var notices = TestScriptTests.Lines(output.Result).Where(line => line.StartsWith("::notice ", StringComparison.Ordinal)).ToList();
        notices.Should().HaveCount(9, "GitHub shows ten notices of a step, one of them the processor's");
        notices[0].Should().StartWith("::notice title=Measured for the first time%2C 1 of 9::shapes_example_0\t1.5%0A");
        var pairs = notices.SelectMany(line => line[(line.IndexOf("::", 9, StringComparison.Ordinal) + 2)..].Split("%0A")).ToList();
        pairs.Should().Equal(names.Select(name => $"{name}\t1.5"), "every pair is in one notice, in order");
    }

    [NeedsPythonFact]
    public void A_Measure_Says_The_Pairs_That_Fail_Their_Shares_As_Errors_With_The_Shares_And_Their_Room()
    {
        // The captures job of 704952c5, whose measure failed and whose page named no pair, compare.py
        // having printed them alone (REVIEW.md, Verdict 66).
        var program = "import compare\n"
                      + "compare.ROOMS['models_loading'] = 3.0\n"
                      + "failures = [f'{name}: {compare.held(name, share, recorded)}' for name, share, recorded in "
                      + "[('shapes_basic_shapes', '5.0', '3.0'), ('models_loading', '7.5', '4.0'), ('text_raylib_fonts', 'none', '2.0')]]\n"
                      + "assert compare.held('shapes_basic_shapes', '3.9', '3.0') is None\n"
                      + "compare.errors(failures, 'AMD EPYC 9V45 96-Core Processor, gfni among its flags')\n"
                      + "names = [f'shapes_example_{i}' for i in range(400)]\n"
                      + "compare.errors([f'{name}: 12.5% apart where 3.0% was recorded, past its room of 1 point' for name in names], 'AMD EPYC 7763 64-Core Processor, gfni not among its flags')\n";
        var start = TestScriptTests.Utf8(new ProcessStartInfo(Probes.Python.Value!) { WorkingDirectory = Path.Combine(Root, "build", "raylib-bench"), RedirectStandardOutput = true, RedirectStandardError = true });
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(program);
        start.Environment["GITHUB_ACTIONS"] = "true";
        start.Environment["GITHUB_STEP_SUMMARY"] = _folder.File("summary.md");
        start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        using var python = Process.Start(start)!;
        var output = python.StandardOutput.ReadToEndAsync();
        var errors = python.StandardError.ReadToEndAsync();
        python.WaitForExit(60_000).Should().BeTrue();
        python.ExitCode.Should().Be(0, errors.Result);

        var said = TestScriptTests.Lines(output.Result).Where(line => line.StartsWith("::error ", StringComparison.Ordinal)).ToList();
        said[0].Should().Be("::error title=3 pair(s) do not hold to the share recorded for them::"
                            + "on AMD EPYC 9V45 96-Core Processor, gfni among its flags, more than 1 point above it, or past the room the record gives, or no longer drawn:"
                            + "%0Ashapes_basic_shapes: 5.0%25 apart where 3.0%25 was recorded, past its room of 1 point"
                            + "%0Amodels_loading: 7.5%25 apart where 4.0%25 was recorded, past its room of 3 points"
                            + "%0Atext_raylib_fonts: no frame from raylib's program where 2.0%25 apart was recorded",
            "each pair is named with the share measured, the share recorded and its room, or what it drew in place of a frame");
        File.ReadAllText(_folder.File("summary.md")).Should().Contain("### 3 pair(s) do not hold to the share recorded for them")
            .And.Contain("    models_loading: 7.5% apart where 4.0% was recorded, past its room of 3 points", "and the page's summary says them too");

        var many = said.Skip(1).ToList();
        many.Should().HaveCountLessThanOrEqualTo(10, "GitHub shows ten errors of a step")
            .And.OnlyContain(line => line.Length <= 4096, "and keeps an error's first 4,096 characters");
        many[0].Should().StartWith($"::error title=400 pair(s) do not hold to the share recorded for them%2C 1 of {many.Count}::");
        many.SelectMany(line => line[(line.IndexOf("::", 9, StringComparison.Ordinal) + 2)..].Split("%0A")).Skip(1).Take(150)
            .Should().Equal(Enumerable.Range(0, 150).Select(i => $"shapes_example_{i}: 12.5%25 apart where 3.0%25 was recorded, past its room of 1 point"),
                "the pairs are shared out in order");
    }

    [NeedsPythonAndBashFact]
    public void A_Step_Whose_Game_Died_Says_How_After_Its_Last_Lines_And_Names_Games_Still_Running_And_None_That_Ended_Of_Their_Own()
    {
        // As build/open-game.sh leaves them for a game that died, its process id and the lines its
        // shell wrote, the first naming the process, for another game a line that names another
        // process, as one an earlier opening wrote late, which is not this opening's, for a third
        // the line of one that ended of its own, which costs the error nothing, and for a fourth
        // the id of a process that still runs. The measure of 704952c5 listed 21 games that ended
        // of their own, and its error was cut before the step's last lines (REVIEW.md, Verdict 66).
        File.WriteAllText(_folder.File("died.sh"), """
            sh -c 'exit 0' & gone="$!"
            wait "$gone"
            echo "$gone" > sessions/Summit.pid
            printf 'its process %s ended with exit code 132, SIGILL, an illegal instruction, on AMD EPYC 7763 64-Core Processor\n' "$gone" > sessions/Summit.ended
            printf '#0  0x00007f6ffce9a000 in ?? () from /lib/x86_64-linux-gnu/libLLVM-17.so.1\n=> 0x7f6ffce9a000:\tud2\n' >> sessions/Summit.ended
            echo "$gone" > sessions/Hopper.pid
            echo "its process 1 ended with exit code 0, of its own" > sessions/Hopper.ended
            sh -c 'exit 0' & fine="$!"
            wait "$fine"
            echo "$fine" > sessions/Tempo.pid
            echo "its process $fine ended with exit code 0, of its own, on AMD EPYC 9V45 96-Core Processor, gfni among its flags" > sessions/Tempo.ended
            sleep 120 < /dev/null > /dev/null 2>&1 &
            echo "$!" > sessions/Wick.pid
            echo "the last line"
            status=$(sh -c 'echo "error [SESSION_UNREACHABLE] Summit accepted the connection and then said nothing." >&2; exit 4')
            """);
        try
        {
            var (exit, log, _) = Step(_folder.File("died.sh"));

            exit.Should().Be(4);
            var error = TestScriptTests.Lines(log).Should().ContainSingle(line => line.StartsWith("::error ", StringComparison.Ordinal)).Subject;
            error.Should().MatchRegex(@"%0ASummit, its process \d+ ended with exit code 132, SIGILL, an illegal instruction, on AMD EPYC 7763 64-Core Processor\.%0A", "the game's death is said with its signal and the processor")
                .And.Contain("%0A    #0  0x00007f6ffce9a000 in ?? () from /lib/x86_64-linux-gnu/libLLVM-17.so.1%0A    => 0x7f6ffce9a000:\tud2", "and what gdb read from its core")
                .And.MatchRegex(@"%0AWick, its process \d+ still runs\.", "a game the step left running is named")
                .And.NotContain("Hopper", "a line naming another process is not this opening's")
                .And.NotContain("Tempo", "a game that ended of its own costs the error nothing");
            error.IndexOf("Summit, its process", StringComparison.Ordinal).Should().BeGreaterThan(error.IndexOf("    the last line", StringComparison.Ordinal),
                "the step's last lines come before the games' endings, so the endings cannot crowd them out");
        }
        finally
        {
            if (int.TryParse(File.ReadAllText(_folder.File("sessions/Wick.pid")).Trim(), out var wick))
                try { Process.GetProcessById(wick).Kill(); } catch (ArgumentException) { } catch (InvalidOperationException) { }
        }
    }

    // Runs build/step.py on the script, as GitHub runs it, against the workflow above and the
    // folder's session logs, and returns its exit code, what it printed and the summary it wrote.
    private (int Exit, string Log, string Summary) Step(string script)
    {
        File.WriteAllText(_folder.File("build.yml"), Workflow);
        var start = TestScriptTests.Utf8(new ProcessStartInfo(Probes.Python.Value!) { WorkingDirectory = _folder.Path, RedirectStandardOutput = true, RedirectStandardError = true });
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

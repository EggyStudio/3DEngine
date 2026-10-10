using System.Diagnostics;
using FluentAssertions;

namespace Engine.Tests.Scripts;

/// <summary>
/// <c>build/soak-check.py</c> names each game whose soak fails, with what climbed and its numbers or
/// the command that ended it, in an error a reader who is not signed in sees (REVIEW.md, Verdict 30).
/// </summary>
/// <remarks>
/// The examples job of <c>22bbf15a</c> failed its soak with the step's own line, "a game grew, or
/// could not be played, over two minutes", and nothing that named the game.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class SoakCheckTests : IDisposable
{
    private static readonly string Root = Api.CheatsheetTests.RepoRoot();
    private readonly TestFolder _folder = new("engine-soak-check-");

    public SoakCheckTests() => Directory.CreateDirectory(_folder.File("sessions"));

    public void Dispose() => _folder.Dispose();

    // Readings ten seconds apart, the heap each one's, and the rest steady.
    private string Readings(string name, params long[] heaps)
    {
        var path = _folder.File(name + ".csv");
        File.WriteAllLines(path, heaps.Select((heap, i) =>
            $"{10 * i} managed {heap / 2} heap {heap} gen2 {i} resident {(400L << 20) + i * (1L << 20)} entities 90 entityIds 91 buffers 34"));
        return path;
    }

    [NeedsPythonFact]
    public void A_Game_Whose_Heap_Climbs_Is_Named_With_The_Heap_And_Its_Numbers()
    {
        var steady = Readings("pusher", 100 << 20, 100 << 20, 100 << 20, 100 << 20, 100 << 20, 100 << 20, 100 << 20, 100 << 20);
        var climbing = Readings("swarm", 100 << 20, 110 << 20, 120 << 20, 130 << 20, 140 << 20, 160 << 20, 180 << 20, 200 << 20);

        var (exit, log) = Check(steady, climbing);

        exit.Should().Be(1);
        var errors = TestScriptTests.Lines(log).Where(line => line.StartsWith("::error ", StringComparison.Ordinal)).ToList();
        errors.Should().ContainSingle("only the game that climbed fails").Which.Should()
            .StartWith("::error title=Soak%2C swarm::swarm: heap climbed from 125829120 to 167772160 at its least, past its bound of")
            .And.Contain("swarm: resident memory 402 MB at the first reading kept and 407 MB at the last");
    }

    [NeedsPythonFact]
    public void A_Game_That_Could_Not_Be_Played_Through_Is_Named_With_The_Command_Its_Code_And_Its_Logs_Warnings()
    {
        var readings = Readings("tempo", 100 << 20, 100 << 20, 100 << 20);
        File.WriteAllText(_folder.File("tempo.failed"), "4 Tempo ./e3d command input.key Enter 2 --name Tempo --quiet --timeout 600\n");
        File.WriteAllLines(Path.Combine(_folder.File("sessions"), "Tempo.log"),
        [
            "[    0.30s] [INFO ] [Engine] opening",
            "[   41.00s] [ERROR] [Engine.Graphics] the device was lost",
        ]);

        var (exit, log) = Check(readings);

        exit.Should().Be(1);
        var error = TestScriptTests.Lines(log).Should().ContainSingle(line => line.StartsWith("::error ", StringComparison.Ordinal)).Subject;
        error.Should().StartWith("::error title=Soak%2C tempo::tempo could not be played through, `./e3d command input.key Enter 2 --name Tempo --quiet --timeout 600` ending with 4");
        error.Should().Contain("[ERROR] [Engine.Graphics] the device was lost").And.Contain("tempo: 3 readings, too few to judge");
    }

    [NeedsPythonFact]
    public void Games_That_Hold_Steady_Pass_With_No_Error()
    {
        var (exit, log) = Check(Readings("hopper", 100 << 20, 101 << 20, 100 << 20, 102 << 20, 101 << 20, 100 << 20, 101 << 20, 102 << 20));

        exit.Should().Be(0);
        log.Should().NotContain("::error");
    }

    [NeedsPythonFact]
    public void A_Value_That_Swings_Higher_In_The_Second_Half_Without_Its_Least_Rising_Holds()
    {
        // Manor's buffers on four cores, its walk streaming rooms of more cells and fewer in and out,
        // here as its heap.
        var (exit, log) = Check(Readings("manor", 373 << 20, 416 << 20, 543 << 20, 567 << 20, 572 << 20, 537 << 20, 521 << 20,
            540 << 20, 567 << 20, 618 << 20, 606 << 20, 569 << 20));

        exit.Should().Be(0, log);
    }

    [NeedsPythonFact]
    public void A_Streaming_Games_Buffers_Are_Bounded_By_The_Most_Its_Whole_Walk_Holds()
    {
        // Manor on lavapipe at a core, two turns in two minutes, its walk entering the house in the
        // second half with nothing leaking: its buffers' least climbs past the first half's bound
        // and stays under the most a whole walk holds, 162. The same climb in a game that streams
        // nothing fails, and so does Manor's past its whole walk.
        string Buffers(string name, params int[] buffers)
        {
            var path = _folder.File(name + ".csv");
            File.WriteAllLines(path, buffers.Select((count, i) =>
                $"{10 * i} managed {50 << 20} heap {100 << 20} gen2 {i} resident {400L << 20} entities 400 entityIds 480 buffers {count}"));
            return path;
        }
        int[] walk = [53, 100, 96, 111, 133, 155, 156, 160, 153, 153, 152, 149, 162];

        Check(Buffers("manor", walk)).Exit.Should().Be(0, "the house's models are the world's");
        Check(Buffers("pusher", walk)).Exit.Should().Be(1, "a game that streams nothing has no whole walk to grow into");
        Check(Buffers("manor", [.. walk.Select((count, i) => i < 7 ? count : count + 40)])).Exit.Should().Be(1, "past the most its whole walk holds");
    }

    private (int Exit, string Log) Check(params string[] readings)
    {
        var start = TestScriptTests.Utf8(new ProcessStartInfo(Probes.Python.Value!) { WorkingDirectory = _folder.Path, RedirectStandardOutput = true, RedirectStandardError = true });
        start.ArgumentList.Add(Path.Combine(Root, "build", "soak-check.py"));
        foreach (var path in readings) start.ArgumentList.Add(path);
        start.ArgumentList.Add("--sessions");
        start.ArgumentList.Add(_folder.File("sessions"));
        start.Environment["GITHUB_ACTIONS"] = "true";
        using var check = Process.Start(start)!;
        var log = check.StandardOutput.ReadToEndAsync();
        var errors = check.StandardError.ReadToEndAsync();
        check.WaitForExit(60_000).Should().BeTrue();
        return (check.ExitCode, log.Result.Replace("\r", "") + errors.Result);
    }
}

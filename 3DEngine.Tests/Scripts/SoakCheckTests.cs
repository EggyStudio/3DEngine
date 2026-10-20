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
        var steady = Readings("slide", 100 << 20, 100 << 20, 100 << 20, 100 << 20, 100 << 20, 100 << 20, 100 << 20, 100 << 20);
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
        // second half with nothing leaking. Its buffers' least climbs past the first half's bound
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
        Check(Buffers("slide", walk)).Exit.Should().Be(1, "a game that streams nothing has no whole walk to grow into");
        Check(Buffers("manor", [.. walk.Select((count, i) => i < 7 ? count : count + 40)])).Exit.Should().Be(1, "past the most its whole walk holds");
    }

    [NeedsPythonFact]
    public void Manors_And_Swarms_Climbs_On_The_Pack_Run_Are_Bounded_By_What_A_Whole_Walk_Or_Turn_Holds()
    {
        // The least of each half on the pack run of 683e6e9d, where nothing leaked. Manor's walk
        // entered rooms the first half never reached, its images going from 81 to 90, its descriptor
        // sets from 82 to 95, its entity ids from 399 to 478 and its assets from 81 to 95, and
        // Swarm's turns on a slow device reached later waves, its entity ids, the most alive at
        // once, going from 108 to 130. Each passes under what a whole walk or turn holds and fails
        // past it, and the same climb in a game with neither fails.
        string Walked(string name, (int Images, int Sets, int Ids, int Assets) before, (int Images, int Sets, int Ids, int Assets) after)
        {
            var path = _folder.File(name + ".csv");
            File.WriteAllLines(path, Enumerable.Range(0, 8).Select(i =>
            {
                var (images, sets, ids, assets) = i < 5 ? before : after;
                return $"{10 * i} managed {50 << 20} heap {100 << 20} gen2 {i} resident {400L << 20} entities 300 entityIds {ids} " +
                       $"assets {assets} buffers 150 images {images} descriptorSets {sets}";
            }));
            return path;
        }

        Check(Walked("manor", (81, 82, 399, 81), (90, 95, 478, 95))).Exit.Should().Be(0, "the rooms the second half reached are the level's");
        Check(Walked("manor", (81, 82, 399, 81), (110, 82, 399, 81))).Exit.Should().Be(1, "images past what a whole walk holds");
        Check(Walked("manor", (81, 82, 399, 81), (81, 115, 399, 81))).Exit.Should().Be(1, "descriptor sets past what a whole walk holds");
        Check(Walked("manor", (81, 82, 399, 81), (81, 82, 620, 81))).Exit.Should().Be(1, "entity ids past what a whole walk holds");
        Check(Walked("manor", (81, 82, 399, 81), (81, 82, 399, 115))).Exit.Should().Be(1, "assets past what a whole walk holds");
        Check(Walked("swarm", (81, 82, 108, 81), (81, 82, 130, 81))).Exit.Should().Be(0, "a later wave holds more at once");
        Check(Walked("swarm", (81, 82, 108, 81), (81, 82, 190, 81))).Exit.Should().Be(1, "entity ids past what a whole turn holds");
        Check(Walked("slide", (81, 82, 108, 81), (81, 82, 130, 81))).Exit.Should().Be(1, "a game with no whole walk or turn to grow into");
    }

    [NeedsPythonFact]
    public void Manors_Entities_Are_Bounded_By_What_A_Whole_Walk_Holds_And_A_Climb_Says_Where_The_Walk_Stood()
    {
        // The least of each half on the pack run of 80625742, Manor's entities from 374 to 422 with
        // nothing leaking, its walk where its status says after each reading's figures. They pass
        // under the 539 a whole walk holds at once, and past it the error says where the walk stood
        // at each half's least, so the run reads itself.
        string Entities(int before, int after)
        {
            var path = _folder.File("manor.csv");
            File.WriteAllLines(path, Enumerable.Range(0, 8).Select(i =>
                $"{10 * i} managed {50 << 20} heap {100 << 20} gen2 {i} resident {400L << 20} entities {(i < 5 ? before : after)} entityIds 539 " +
                $"buffers 150 | Play lanterns {i / 2}/6 at {i}.0,0.0,{40 - 10 * i}.0 cells {14 + i} doors 0 route {4 * i}/34 time {10 * i}.0 worst 20ms"));
            return path;
        }

        Check(Entities(374, 422)).Exit.Should().Be(0, "the rooms the second half reached are the estate's");
        var (exit, log) = Check(Entities(374, 640));
        exit.Should().Be(1, "past what a whole walk holds at once");
        var error = TestScriptTests.Lines(log).Should().ContainSingle(line => line.StartsWith("::error ", StringComparison.Ordinal)).Subject;
        error.Should().Contain("manor: entities climbed from 374 to 640 at its least")
            .And.Contain("the second half's least where it stood at Play lanterns 2/6 at 5.0,0.0,-10.0 cells 19 doors 0 route 20/34");
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

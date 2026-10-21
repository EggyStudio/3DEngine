using System.Diagnostics;
using System.Text;
using FluentAssertions;

namespace Engine.Tests.Cli;

/// <summary>
/// <c>build/animate.py</c>, which makes captures on disk into one animation, run over three PNG
/// captures by the engine's own writers through <c>./e3d animate</c> and, where Pillow is installed,
/// by Pillow, each file read back by the engine's reader.
/// </summary>
/// <remarks>
/// <para>
/// The three are flat colors, so a GIF holds each exactly, and the third is twice the size of the
/// others, which the script fits to the first's, as captures of a window resized between them
/// would need. The engine's writers are asked for by <c>E3D_ANIMATE_WRITER=engine</c>, so a machine
/// with Pillow tries both and one without it the engine's alone.
/// </para>
/// <para>
/// The engine's path runs the client the suite's own build made, named by <c>E3D_CLIENT</c>, and
/// never builds one, since an MSBuild node a build inside a test starts can hold the script's
/// output open while it idles, as BevyCSharp's suite found (its Verdict 41). The script is held to
/// a minute and ended with all it started past that, the test failing with what it printed.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class AnimateScriptTests : IDisposable
{
    private static readonly (byte R, byte G, byte B)[] Colors = [(200, 40, 30), (20, 60, 220), (240, 200, 30)];

    private readonly TestFolder _folder = new("engine-animate-");

    public void Dispose() => _folder.Dispose();

    [NeedsAnimateTheory]
    [InlineData("three.gif")]
    [InlineData("three.png")]
    public void Three_Captures_Make_An_Animation_Of_Three_Frames_With_The_Delay_Given_By_The_Engines_Writers(string name) =>
        ThreeFrames("engine", name);

    [NeedsAnimateTheory(pillow: true)]
    [InlineData("three.gif")]
    [InlineData("three.png")]
    public void Three_Captures_Make_An_Animation_Of_Three_Frames_With_The_Delay_Given_By_Pillow(string name) =>
        ThreeFrames("pillow", name);

    private void ThreeFrames(string writer, string name)
    {
        var (exit, log) = Animate(writer, name, "--delay", "250");
        exit.Should().Be(0, log);

        var bytes = File.ReadAllBytes(_folder.File(name));
        var read = name.EndsWith(".gif", StringComparison.Ordinal) ? AnimationReader.Gif(bytes) : AnimationReader.Png(bytes);
        read.Frames.Should().HaveCount(3);
        read.Delays.Should().Equal(250, 250, 250);
        read.Loops.Should().Be(0);
        for (var i = 0; i < 3; i++)
        {
            var frame = read.Frames[i];
            (frame.Width, frame.Height).Should().Be((48, 32), "each picture is fitted to the first's size");
            var middle = (16 * 48 + 24) * 4;
            (frame.Pixels[middle], frame.Pixels[middle + 1], frame.Pixels[middle + 2]).Should().Be(Colors[i]);
        }
    }

    [NeedsAnimateFact(pillow: true)]
    public void Pillow_Writes_An_Animated_WebP_Of_The_Three()
    {
        var (exit, log) = Animate("pillow", "three.webp", "--delay", "250");
        exit.Should().Be(0, log);

        // A RIFF of WebP holding an animation's chunk and a frame's chunk for each picture.
        var text = Encoding.ASCII.GetString(File.ReadAllBytes(_folder.File("three.webp")));
        text[..4].Should().Be("RIFF");
        text[8..12].Should().Be("WEBP");
        text.Should().Contain("ANIM");
        (text.Split("ANMF").Length - 1).Should().Be(3);
    }

    [NeedsAnimateFact]
    public void The_Engine_Says_A_WebP_Needs_Pillow_And_The_Script_Refuses_A_Kind_It_Does_Not_Make()
    {
        var (exit, log) = Animate("engine", "three.webp", "--delay", "250");
        exit.Should().NotBe(0);
        log.Should().Contain("needs Pillow");

        (exit, log) = Animate("engine", "three.bmp", "--delay", "250");
        exit.Should().Be(2);
        log.Should().Contain("none of the kinds");

        (exit, _) = Animate("engine", "three.gif", "--delay", "0");
        exit.Should().Be(2);
    }

    // Writes the three captures and runs the script over them into the folder, by the writer named.
    private (int Exit, string Log) Animate(string writer, string name, params string[] options)
    {
        var pictures = new List<string>();
        for (var i = 0; i < Colors.Length; i++)
        {
            var (width, height) = i == 2 ? (96, 64) : (48, 32);
            var pixels = new byte[width * height * 4];
            for (var at = 0; at < pixels.Length; at += 4) (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (Colors[i].R, Colors[i].G, Colors[i].B, (byte)255);
            var path = _folder.File($"capture{i}.png");
            PngWriter.Write(path, pixels, width, height);
            pictures.Add(path);
        }

        var start = new ProcessStartInfo(Probes.Python.Value!)
        {
            WorkingDirectory = Api.CheatsheetTests.RepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(Path.Combine("build", "animate.py"));
        start.ArgumentList.Add(_folder.File(name));
        foreach (var picture in pictures) start.ArgumentList.Add(picture);
        foreach (var option in options) start.ArgumentList.Add(option);
        if (writer == "engine")
        {
            start.Environment["E3D_ANIMATE_WRITER"] = "engine";
            start.Environment["E3D_CLIENT"] = Probes.Client.Value!;
        }

        using var script = new Process { StartInfo = start };
        var said = new StringBuilder();
        var ended = new TaskCompletionSource();
        var open = 2;
        DataReceivedEventHandler take = (_, line) =>
        {
            if (line.Data is null)
            {
                if (Interlocked.Decrement(ref open) == 0) ended.TrySetResult();
                return;
            }
            lock (said) said.AppendLine(line.Data);
        };
        script.OutputDataReceived += take;
        script.ErrorDataReceived += take;
        script.Start();
        script.BeginOutputReadLine();
        script.BeginErrorReadLine();

        // A minute for three small pictures, past which the script and all it started are ended and
        // the test says what it printed.
        if (!script.WaitForExit(60_000))
        {
            script.Kill(entireProcessTree: true);
            lock (said) throw new Xunit.Sdk.XunitException($"build/animate.py had not ended a minute after it started, having printed:\n{said}");
        }
        // Its streams end once nothing it started holds them, which is waited for five seconds at
        // most, since a process it left behind holding them is no fault of what it wrote.
        ended.Task.Wait(TimeSpan.FromSeconds(5));
        lock (said) return (script.ExitCode, said.ToString());
    }
}

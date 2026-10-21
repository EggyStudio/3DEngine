using FluentAssertions;
using StbImageSharp;
using static Engine.E3D;

namespace Engine.Tests.Diagnostics;

/// <summary>
/// Recording a run as an animated GIF or PNG: the two writers read back by the engine's reader and a
/// GIF by StbImageSharp's, the palette a GIF's colors are cut to, the <c>record</c> command's words,
/// frames given to the recorder written as a recording of them is, with no GPU, and a recording of
/// a run that draws, its frames counted and their delays read from the file.
/// </summary>
/// <remarks>
/// The writers are read back by <see cref="AnimationReader"/>, which lays each frame over the ones
/// before as a browser does, so a frame written as the rectangle that changed is checked as the whole
/// picture it leaves. A PNG is lossless and comes back as written. A GIF holds 256 colors, so a
/// picture of fewer comes back exactly, and a gradient within the distance between the colors its
/// part of the palette was cut to.
/// </remarks>
[Collection("E3D")]
public sealed class RecordingTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-record-");

    public void Dispose()
    {
        if (Recorder.IsRecording) Recorder.Stop();
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static RgbaFrame Flat(int width, int height, byte r, byte g, byte b)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (r, g, b, 255);
        return new RgbaFrame(width, height, pixels);
    }

    private static RgbaFrame With(RgbaFrame frame, int left, int top, int width, int height, Func<int, int, (byte R, byte G, byte B)> color)
    {
        var pixels = (byte[])frame.Pixels.Clone();
        for (var y = top; y < top + height; y++)
            for (var x = left; x < left + width; x++)
            {
                var (r, g, b) = color(x, y);
                var at = (y * frame.Width + x) * 4;
                (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (r, g, b, 255);
            }
        return frame with { Pixels = pixels };
    }

    private static (byte R, byte G, byte B) Pixel(RgbaFrame frame, int x, int y)
    {
        var at = (y * frame.Width + x) * 4;
        return (frame.Pixels[at], frame.Pixels[at + 1], frame.Pixels[at + 2]);
    }

    // Three frames, a flat one, one with a square laid over it and one with a gradient.
    private static RgbaFrame[] Three()
    {
        var first = Flat(40, 30, 200, 40, 30);
        var second = With(first, 5, 5, 10, 10, (_, _) => (20, 60, 220));
        var third = With(second, 20, 10, 16, 16, (x, y) => ((byte)((x - 20) * 16), (byte)(y * 9), 128));
        return [first, second, third];
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void An_Animated_Png_Reads_Back_Frame_By_Frame_As_It_Was_Written()
    {
        var frames = Three();
        var file = new MemoryStream();
        PngWriter.Animated(file, frames, [100, 250, 1000]);

        var read = AnimationReader.Png(file.ToArray());

        read.Frames.Should().HaveCount(3);
        read.Delays.Should().Equal(100, 250, 1000);
        read.Loops.Should().Be(0);
        for (var i = 0; i < 3; i++) read.Frames[i].Pixels.Should().Equal(frames[i].Pixels, $"frame {i} is lossless");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Gif_Reads_Back_With_Its_Frames_Its_Delays_In_Hundredths_And_Its_Loop()
    {
        var frames = Three();
        var file = new MemoryStream();
        GifWriter.Write(file, frames, [100, 250, 1004]);

        var read = AnimationReader.Gif(file.ToArray());

        read.Frames.Should().HaveCount(3);
        read.Delays.Should().Equal(100, 250, 1000);
        read.Loops.Should().Be(0);
        // The flat parts, a few colors among the gradient's, come back exactly, and the square laid
        // over the second frame is there in the third, which wrote only what changed.
        Pixel(read.Frames[0], 0, 0).Should().Be(((byte)200, (byte)40, (byte)30));
        Pixel(read.Frames[1], 9, 9).Should().Be(((byte)20, (byte)60, (byte)220));
        Pixel(read.Frames[2], 9, 9).Should().Be(((byte)20, (byte)60, (byte)220));
        Pixel(read.Frames[2], 39, 29).Should().Be(((byte)200, (byte)40, (byte)30));

        // The gradient within a few steps of what it was, by the pattern's mean over its tiles.
        double sum = 0;
        for (var y = 10; y < 26; y++)
            for (var x = 20; x < 36; x++)
            {
                var (r, g, b) = Pixel(read.Frames[2], x, y);
                var (er, eg, eb) = Pixel(frames[2], x, y);
                sum += Math.Sqrt((r - er) * (r - er) + (g - eg) * (g - eg) + (b - eb) * (b - eb));
            }
        (sum / 256).Should().BeLessThan(12);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Median_Cut_Of_A_Known_Picture_Holds_Each_Of_Its_Colors()
    {
        // Sixteen colors in blocks, each in a bin of the cut's 32 steps a channel of its own, which
        // the cut keeps apart and gives back as the mean of the pixels in it.
        (byte R, byte G, byte B)[] colors =
        [
            (0, 0, 0), (255, 255, 255), (255, 0, 0), (0, 255, 0), (0, 0, 255), (255, 255, 0), (0, 255, 255), (255, 0, 255),
            (128, 128, 128), (64, 32, 16), (16, 32, 64), (250, 128, 114), (34, 139, 34), (70, 130, 180), (210, 105, 30), (218, 112, 214),
        ];
        var picture = Flat(64, 64, 0, 0, 0);
        for (var i = 0; i < colors.Length; i++)
        {
            var color = colors[i];
            picture = With(picture, i % 4 * 16, i / 4 * 16, 16, 16, (_, _) => color);
        }

        var cut = GifWriter.MedianCut([picture], 255);
        foreach (var (r, g, b) in colors)
            Enumerable.Range(0, cut.Count).Min(i => Math.Abs(cut.Colors[i * 3] - r) + Math.Abs(cut.Colors[i * 3 + 1] - g) + Math.Abs(cut.Colors[i * 3 + 2] - b))
                .Should().Be(0, $"{(r, g, b)} is a color of the palette");

        var file = new MemoryStream();
        GifWriter.Write(file, [picture], [100]);
        AnimationReader.Gif(file.ToArray()).Frames[0].Pixels.Should().Equal(picture.Pixels);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Noise_Of_Many_Colors_Survives_The_Table_Starting_Again()
    {
        // 250 colors, one to a bin, so the palette is exactly them, scattered at random over a
        // picture whose codes fill the table of 4096 many times over.
        var random = new Random(7);
        var colors = Enumerable.Range(0, 250).Select(i => ((byte)(i % 8 * 32 + 3), (byte)(i / 8 % 8 * 32 + 5), (byte)(i / 64 * 32 + 7))).ToArray();
        var noise = With(Flat(200, 200, 0, 0, 0), 0, 0, 200, 200, (_, _) => colors[random.Next(colors.Length)]);

        var file = new MemoryStream();
        GifWriter.Write(file, [noise, noise], [50, 50]);
        var read = AnimationReader.Gif(file.ToArray());

        read.Frames[0].Pixels.Should().Equal(noise.Pixels);
        read.Frames[1].Pixels.Should().Equal(noise.Pixels);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Record_Commands_Words_Are_Read_As_They_Are_Written()
    {
        var counted = Recorder.Parse("a.gif", ["8"], out _)!;
        (counted.Count, counted.Length, counted.Step).Should().Be((8, (Recorder.Measure?)null, new Recorder.Measure(Recorder.DefaultStepSeconds, false)));

        var timed = Recorder.Parse("a.png", ["5s", "every", "250ms", "scale", "0.5", "play", "100ms"], out _)!;
        (timed.Length, timed.Step, timed.Scale, timed.PlayMilliseconds).Should().Be(
            ((Recorder.Measure?)new Recorder.Measure(5, false), new Recorder.Measure(0.25, false), 0.5f, (int?)100));
        Recorder.Parse("a.gif", ["8", "play", "40"], out _)!.PlayMilliseconds.Should().Be(40, "a bare number of play is milliseconds");

        var framed = Recorder.Parse("a.gif", ["120f", "every", "15f"], out _)!;
        (framed.Length, framed.Step).Should().Be(((Recorder.Measure?)new Recorder.Measure(120, true), new Recorder.Measure(15, true)));

        foreach (string[] wrong in (string[][])[[], ["5x"], ["2.5"], ["8", "every"], ["8", "scale", "2"], ["8", "every", "0ms"], ["8", "play", "15f"], ["8", "speed", "2"]])
        {
            Recorder.Parse("a.gif", wrong, out var problem).Should().BeNull($"{string.Join(' ', wrong)} is not a recording's words");
            problem.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Each_Frame_Is_Shown_For_The_Time_To_The_Next_And_The_Last_For_The_Step()
    {
        var timed = new Recorder.Request("a.gif", new Recorder.Measure(0.25, false), null, 3, 1f, null);
        Recorder.Delays([0, 0.25, 0.5], timed).Should().Equal(250, 250, 250);
        // A step in frames, whose last is shown as long as the one before it.
        Recorder.Delays([0, 0.25, 0.55], timed with { Step = new Recorder.Measure(15, true) }).Should().Equal(250, 300, 300);
        Recorder.Delays([0, 0.25, 0.5], timed with { PlayMilliseconds = 40 }).Should().Equal(40, 40, 40);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Eight_Frames_Given_A_Quarter_Second_Apart_Are_Written_As_A_Recording_Of_Them()
    {
        // With no GPU to take them from, the frames are given to the recorder as its pictures would
        // come back, each redder than the last, and written by the same path.
        var frames = Enumerable.Range(0, 8).Select(i => (Flat(32, 24, (byte)(i * 30), 40, 90), i * 0.25)).ToArray();
        var gif = _folder.File("given.gif");
        Recorder.WriteFrames(new Recorder.Request(gif, new Recorder.Measure(0.25, false), null, 8, 1f, null), frames);

        // Read by StbImageSharp, a reader of its own, as eight frames shown a quarter second each.
        using (var stream = File.OpenRead(gif))
        {
            // Its enumerator fills one buffer again for each frame, so each is read as it comes.
            var read = ImageResult.AnimatedGifFramesFromStream(stream, ColorComponents.RedGreenBlueAlpha)
                .Select(frame => (frame.DelayInMs, Red: (int)frame.Data[0])).ToList();
            read.Should().HaveCount(8);
            read.Select(frame => frame.DelayInMs).Should().AllBeEquivalentTo(250);
            read.Select(frame => frame.Red).Should().Equal(Enumerable.Range(0, 8).Select(i => i * 30), "each frame keeps its own red");
        }

        var png = _folder.File("given.png");
        Recorder.WriteFrames(new Recorder.Request(png, new Recorder.Measure(0.25, false), null, 8, 1f, null), frames);
        var animated = AnimationReader.Png(File.ReadAllBytes(png));
        animated.Frames.Should().HaveCount(8);
        animated.Delays.Should().AllBeEquivalentTo(250);
        for (var i = 0; i < 8; i++) animated.Frames[i].Pixels.Should().Equal(frames[i].Item1.Pixels, $"frame {i} comes back as given");
        File.Exists(png + ".part").Should().BeFalse("the file is written under a name of its own and moved into place whole");
    }

    // An offscreen app of 64 by 48 on a clock of a sixtieth of a second a frame, its clear color
    // redder each frame, drawn for 220 frames with what each frame asks done after it starts.
    private static void Run(Action<int> eachFrame)
    {
        var config = Config.Default.WithWindow("record test", 64, 48) with { Headless = true, Offscreen = true, Samples = 1, FrameSeconds = 1.0 / 60 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        for (var frame = 0; frame < 220; frame++)
        {
            BeginDrawing();
            ClearBackground(new Color((byte)Math.Min(255, frame), 50, 100));
            eachFrame(frame);
            EndDrawing();
        }
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Recording_Of_Eight_Frames_A_Quarter_Second_Apart_Is_A_Gif_Of_Eight_Frames_With_Those_Delays()
    {
        var path = _folder.File("eight.gif");
        string? answer = null;
        Run(frame =>
        {
            if (frame != 30) return;
            using (ConsoleHost.Lend(GetApp().World, GetApp())) answer = ConsoleCommands.Run($"record {path} 8 every 250ms");
        });

        answer.Should().StartWith("recording 8 pictures");
        Recorder.Last!.GetAwaiter().GetResult().Should().Be(path, "the recording writes its file once its last picture came back");
        var read = AnimationReader.Gif(File.ReadAllBytes(path));
        read.Frames.Should().HaveCount(8);
        read.Delays.Should().AllBeEquivalentTo(250);
        (read.Frames[0].Width, read.Frames[0].Height).Should().Be((64, 48));
        // The clear color's red climbs a step a frame, so each picture is redder than the last.
        var reds = read.Frames.Select(frame => Pixel(frame, 32, 24).R).ToArray();
        reds.Should().BeInAscendingOrder();
        reds[^1].Should().BeGreaterThan(reds[0]);
        using (ConsoleHost.Lend(GetApp().World, GetApp())) ConsoleCommands.Run("record.status").Should().Be($"wrote 8 frames to {path}");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Recording_Stopped_By_The_Program_Is_An_Animated_Png_Of_The_Pictures_It_Took()
    {
        var path = _folder.File("eight.png");
        Run(frame =>
        {
            if (frame == 30) StartRecording(path, 250, 0.5f);
            // Eight pictures, from the frame after it started, fifteen frames apart, and stopped
            // before the ninth.
            if (frame == 140) StopRecording();
        });

        Recorder.Last!.GetAwaiter().GetResult().Should().Be(path);
        Recorder.IsRecording.Should().BeFalse();
        var read = AnimationReader.Png(File.ReadAllBytes(path));
        read.Frames.Should().HaveCount(8);
        read.Delays.Should().AllBeEquivalentTo(250);
        (read.Frames[0].Width, read.Frames[0].Height).Should().Be((32, 24), "each picture is kept at half the window's size");
    }
}

using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Core;

[Trait("Category", "Unit")]
public class GesturesTests
{
    private const double Frame = 1 / 60.0;

    // Feeds the frames from time 0, one finger each when given, and returns the gesture of each.
    private static List<Gesture> Run(Gestures gestures, params Vector2?[][] frames)
    {
        var seen = new List<Gesture>();
        for (int i = 0; i < frames.Length; i++)
        {
            gestures.Update(frames[i].Where(p => p is not null).Select(p => p!.Value).ToArray(), i * Frame);
            seen.Add(gestures.Current);
        }
        return seen;
    }

    private static Vector2?[] Down(float x, float y) => [new Vector2(x, y)];
    private static readonly Vector2?[] Up = [];

    [Fact]
    public void A_Touch_Is_A_Tap_Then_A_Hold_That_Counts_Its_Time()
    {
        var gestures = new Gestures();
        var seen = Run(gestures, Down(0.5f, 0.5f), Down(0.5f, 0.5f), Down(0.501f, 0.5f), Up);

        seen.Should().Equal(Gesture.Tap, Gesture.Hold, Gesture.Hold, Gesture.None);
        gestures.HoldSeconds.Should().Be(0, "lifting ends the hold");
    }

    [Fact]
    public void Two_Quick_Taps_Near_One_Place_Are_A_Double_Tap()
    {
        var seen = Run(new Gestures(), Down(0.5f, 0.5f), Up, Down(0.51f, 0.5f), Up, Up, Down(0.9f, 0.9f));

        seen[0].Should().Be(Gesture.Tap);
        seen[2].Should().Be(Gesture.DoubleTap);
        seen[5].Should().Be(Gesture.Tap, "far from the last, it is a tap of its own");
    }

    [Fact]
    public void Clicks_The_Console_Makes_A_Frame_Apart_Are_A_Double_Tap()
    {
        var input = new Input();
        var synthetic = new SyntheticInput();
        var gestures = new Gestures();
        synthetic.Clicks(input, MouseButton.Left, 0, 2);

        var seen = new List<Gesture>();
        for (ulong frame = 0; frame < 6; frame++)
        {
            synthetic.Update(input, frame);
            input.ApplyQueued();
            gestures.Update(input.MouseDown(MouseButton.Left) ? [new Vector2(0.5f, 0.5f)] : [], frame * Frame);
            seen.Add(gestures.Current);
        }

        seen.Should().Equal(Gesture.Tap, Gesture.None, Gesture.DoubleTap, Gesture.None, Gesture.None, Gesture.None);
    }

    [Fact]
    public void A_Moved_Finger_Drags_And_A_Fast_Release_Swipes_Its_Way()
    {
        var gestures = new Gestures();
        var right = Run(gestures, Down(0.2f, 0.5f), Down(0.25f, 0.5f), Down(0.4f, 0.5f), Up);
        right.Should().Equal(Gesture.Tap, Gesture.Drag, Gesture.Drag, Gesture.SwipeRight);
        gestures.DragAngle.Should().BeApproximately(0, 0.5f);

        var up = Run(new Gestures(), Down(0.5f, 0.8f), Down(0.5f, 0.6f), Up);
        up[^1].Should().Be(Gesture.SwipeUp, "the screen's y grows downward, so up is toward smaller y");

        // A drag that goes slower than a swipe is let go with no gesture.
        var slow = new Gestures();
        var frames = new List<Vector2?[]> { Down(0.5f, 0.5f) };
        for (int i = 1; i <= 120; i++) frames.Add(Down(0.5f + i * 0.0003f, 0.5f));
        frames.Add(Up);
        Run(slow, [.. frames])[^1].Should().Be(Gesture.None);
    }

    [Theory]
    [InlineData(0.25, false)]
    [InlineData(1.0 / 60, true)]
    public void A_Drag_The_Console_Makes_Over_Ten_Frames_Swipes_At_A_Sixtieth_A_Frame_And_Not_At_A_Quarter(double frameSeconds, bool swipes)
    {
        // A swipe is a drag let go faster than 0.2 of the window a second, timed on the app's
        // clock. On a device slow enough that the console's drag of ten frames took two seconds, its
        // clock reading about a quarter of a second a frame, a drag up a fifth of the window slid
        // nothing in the puzzle game, where the frame time time.frame sets counts each frame as a
        // sixtieth, as at 60 frames a second. Here the drag is 0.06 of the window.
        var app = new App(Config.Default with { Headless = true, FrameSeconds = frameSeconds });
        app.AddPlugin(new TimePlugin());
        app.AddPlugin(new InputPlugin());
        var input = app.World.Resource<Input>();
        var height = app.World.Resource<Config>().WindowData.Height;
        var synthetic = new SyntheticInput();
        SyntheticInput.Move(input, 100, height / 2);

        var seen = new List<Gesture>();
        for (ulong frame = 0; frame < 16; frame++)
        {
            if (frame == 1) synthetic.Drag(input, MouseButton.Left, 0, -(int)(height * 0.06f), frame, 10);
            synthetic.Update(input, frame);
            input.ApplyQueued();
            app.BeginFrame();
            seen.Add(app.World.Resource<Gestures>().Current);
            app.EndFrame();
        }
        app.Shutdown();

        seen.Contains(Gesture.SwipeUp).Should().Be(swipes, $"the gestures were {string.Join(", ", seen)}");
    }

    [Fact]
    public void Two_Fingers_Moving_Apart_Pinch_Out_And_Together_Pinch_In()
    {
        var gestures = new Gestures();
        Vector2?[] Pair(float half) => [new Vector2(0.5f - half, 0.5f), new Vector2(0.5f + half, 0.5f)];
        var seen = Run(gestures, Pair(0.1f), Pair(0.12f), Pair(0.08f));

        seen.Should().Equal(Gesture.None, Gesture.PinchOut, Gesture.PinchIn);
        gestures.PinchVector.X.Should().BeApproximately(0.16f, 1e-4f);
    }

    [Fact]
    public void A_Gesture_That_Is_Not_Enabled_Is_Not_Reported()
    {
        var gestures = new Gestures { Enabled = Gesture.Drag };
        Run(gestures, Down(0.5f, 0.5f))[0].Should().Be(Gesture.None, "taps are not enabled");
    }
}

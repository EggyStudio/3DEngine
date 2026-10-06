using System.Numerics;

namespace Engine;

/// <summary>The gestures <see cref="Gestures"/> recognizes, as raylib's <c>Gesture</c> flags.</summary>
[Flags]
public enum Gesture
{
    /// <summary>No gesture.</summary>
    None = 0,
    /// <summary>A finger touched the screen.</summary>
    Tap = 1,
    /// <summary>A finger touched the screen twice in quick succession, near one place.</summary>
    DoubleTap = 2,
    /// <summary>A finger stays down without moving far.</summary>
    Hold = 4,
    /// <summary>A finger moves while down.</summary>
    Drag = 8,
    /// <summary>A finger flicked right and lifted.</summary>
    SwipeRight = 16,
    /// <summary>A finger flicked left and lifted.</summary>
    SwipeLeft = 32,
    /// <summary>A finger flicked up and lifted.</summary>
    SwipeUp = 64,
    /// <summary>A finger flicked down and lifted.</summary>
    SwipeDown = 128,
    /// <summary>Two fingers move toward each other.</summary>
    PinchIn = 256,
    /// <summary>Two fingers move apart.</summary>
    PinchOut = 512,
}

/// <summary>
/// Recognizes raylib's gestures from the fingers on the screen, frame by frame: taps, double taps,
/// holds, drags, swipes and pinches.
/// </summary>
/// <remarks>
/// Positions are fractions of the window, from 0 to 1 across and down, as raylib's are, so the
/// thresholds hold at any window size. The thresholds are raylib's. A double tap is a second touch within
/// 0.3 seconds and 0.03 of the screen of the first, a drag starts after 0.015 of the screen, a swipe
/// is a drag released faster than 0.2 of the screen a second, and a pinch counts after the fingers'
/// distance changes by 0.005.
/// </remarks>
public sealed class Gestures
{
    private const double DoubleTapSeconds = 0.3;
    private const float DoubleTapRange = 0.03f;
    private const float DragStart = 0.015f;
    private const float SwipeSpeed = 0.2f;
    private const float PinchStart = 0.005f;

    private int _lastCount;
    private Vector2 _downAt, _lastAt;
    private double _downTime, _lastTapTime = double.NegativeInfinity, _holdSince;
    private Vector2 _lastTapAt;
    private float _pinchDistance;

    /// <summary>Which gestures are recognized, all of them to begin with.</summary>
    internal Gesture Enabled { get; set; } = (Gesture)0x3FF;

    /// <summary>The gesture this frame, <see cref="Gesture.None"/> when there is none or it is not enabled.</summary>
    public Gesture Current { get; internal set; }

    /// <summary>How long the current hold has lasted, in seconds.</summary>
    internal float HoldSeconds { get; private set; }

    /// <summary>How far, in fractions of the window, the current drag has gone since the finger came down.</summary>
    internal Vector2 DragVector { get; private set; }

    /// <summary>The angle of the last drag, in degrees counterclockwise from right, as raylib measures it.</summary>
    internal float DragAngle { get; private set; }

    /// <summary>The vector between the two fingers of a pinch, in fractions of the window.</summary>
    internal Vector2 PinchVector { get; private set; }

    /// <summary>The angle of the vector between the two fingers of a pinch, in degrees.</summary>
    internal float PinchAngle { get; private set; }

    /// <summary>Advances by one frame, with the fingers down in it, in fractions of the window, at <paramref name="time"/> seconds.</summary>
    public void Update(ReadOnlySpan<Vector2> points, double time)
    {
        var gesture = Gesture.None;
        var count = points.Length;

        if (count == 1 && _lastCount == 0)
        {
            // Down: a tap, or a double tap when close in time and place to the one before.
            _downAt = _lastAt = points[0];
            _downTime = _holdSince = time;
            DragVector = Vector2.Zero;
            if (time - _lastTapTime <= DoubleTapSeconds && Vector2.Distance(points[0], _lastTapAt) <= DoubleTapRange)
            {
                gesture = Gesture.DoubleTap;
                _lastTapTime = double.NegativeInfinity;
            }
            else
            {
                gesture = Gesture.Tap;
                _lastTapTime = time;
                _lastTapAt = points[0];
            }
        }
        else if (count == 1 && _lastCount == 1)
        {
            // Held: a drag once it has gone far enough, and a hold until then.
            _lastAt = points[0];
            DragVector = points[0] - _downAt;
            if (DragVector.Length() >= DragStart || Current == Gesture.Drag)
            {
                gesture = Gesture.Drag;
                DragAngle = Angle(DragVector);
            }
            else
            {
                gesture = Gesture.Hold;
                HoldSeconds = (float)(time - _holdSince);
            }
        }
        else if (count == 0 && _lastCount == 1)
        {
            // Up: a swipe when the drag was fast enough, which way by its angle.
            var moved = _lastAt - _downAt;
            var seconds = Math.Max(time - _downTime, 1e-3);
            if (moved.Length() >= DragStart && moved.Length() / seconds > SwipeSpeed)
            {
                DragAngle = Angle(moved);
                gesture = DragAngle is > 30 and < 150 ? Gesture.SwipeUp
                    : DragAngle is > 210 and < 330 ? Gesture.SwipeDown
                    : DragAngle is >= 150 and <= 210 ? Gesture.SwipeLeft
                    : Gesture.SwipeRight;
            }
            HoldSeconds = 0;
        }
        else if (count == 2)
        {
            // Two fingers: a pinch by how their distance changed since the last frame.
            var between = points[1] - points[0];
            var distance = between.Length();
            PinchVector = between;
            PinchAngle = Angle(between);
            if (_lastCount == 2 && MathF.Abs(distance - _pinchDistance) >= PinchStart)
            {
                gesture = distance > _pinchDistance ? Gesture.PinchOut : Gesture.PinchIn;
                _pinchDistance = distance;
            }
            else if (_lastCount != 2) _pinchDistance = distance;
        }

        _lastCount = count;
        Current = (gesture & Enabled) != 0 ? gesture : Gesture.None;
    }

    // The direction of a vector across a screen whose y grows downward, in degrees counterclockwise from right.
    private static float Angle(Vector2 v)
    {
        var angle = float.RadiansToDegrees(MathF.Atan2(-v.Y, v.X));
        return angle < 0 ? angle + 360 : angle;
    }
}

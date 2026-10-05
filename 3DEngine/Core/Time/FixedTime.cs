namespace Engine;

/// <summary>
/// The fixed timestep <see cref="Stage.FixedUpdate"/> runs at: how long a step is, and how much
/// frame time is waiting to be stepped through.
/// </summary>
/// <remarks>
/// <para>
/// <c>TimePlugin</c> adds each frame's delta in <see cref="Stage.First"/>, and
/// <see cref="App.BeginFrame"/> runs <see cref="Stage.FixedUpdate"/> once per whole step that has
/// accumulated, so a simulation advances by the same amount every step whatever the frame rate.
/// </para>
/// <para>
/// At most <see cref="MaxStepsPerFrame"/> steps run in one frame, and time beyond them is
/// dropped. Without the cap, a frame slow enough to need many steps makes the next frame slower
/// still, and the backlog never drains.
/// </para>
/// </remarks>
public sealed class FixedTime
{
    private double _stepSeconds = 1.0 / 60.0;

    /// <summary>Seconds one step simulates. Defaults to 1/60.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public double StepSeconds
    {
        get => _stepSeconds;
        set => _stepSeconds = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "A step must be longer than zero.");
    }

    /// <summary>Steps per second, the inverse of <see cref="StepSeconds"/>.</summary>
    public double Hz
    {
        get => 1.0 / StepSeconds;
        set => StepSeconds = 1.0 / value;
    }

    /// <summary>The most steps run in one frame. Defaults to 5.</summary>
    internal int MaxStepsPerFrame { get; set; } = 5;

    /// <summary>Frame time not yet stepped through, in seconds.</summary>
    internal double Accumulator { get; private set; }

    /// <summary>How many steps have run this frame.</summary>
    internal int StepsThisFrame { get; private set; }

    /// <summary>How far between the last step and the next the frame is, from 0 to 1, for interpolating what is drawn.</summary>
    public double Alpha => Math.Clamp(Accumulator / StepSeconds, 0, 1);

    /// <summary>Adds a frame's time and starts counting the frame's steps from zero.</summary>
    internal void Accumulate(double deltaSeconds)
    {
        Accumulator += Math.Max(0, deltaSeconds);
        StepsThisFrame = 0;
    }

    /// <summary>Takes one step's time out of the accumulator, if a whole step is there and the frame has steps left.</summary>
    /// <returns>Whether a step should run.</returns>
    internal bool TryStep()
    {
        if (StepsThisFrame >= MaxStepsPerFrame)
        {
            Accumulator = Math.Min(Accumulator, StepSeconds);
            return false;
        }

        if (Accumulator < StepSeconds) return false;

        Accumulator -= StepSeconds;
        StepsThisFrame++;
        return true;
    }
}

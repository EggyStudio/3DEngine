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
/// Every second a frame reports is stepped through, so what the program moved by the frame's time
/// and what the simulation moved agree. The one clamp is <see cref="Time.MaxDeltaSeconds"/>, a
/// quarter of a second, which holds a frame to fifteen steps of a sixtieth, so a slow frame cannot
/// owe more, and below four frames a second the whole game slows together. A second cap here
/// dropped what was past five steps, so a frame of 83 to 250 ms told the program a quarter of a
/// second passed and simulated a twelfth.
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

    /// <summary>Frame time not yet stepped through, in seconds.</summary>
    internal double Accumulator { get; private set; }

    /// <summary>How far between the last step and the next the frame is, from 0 to 1, for interpolating what is drawn.</summary>
    public double Alpha => Math.Clamp(Accumulator / StepSeconds, 0, 1);

    /// <summary>Adds a frame's time, already held to <see cref="Time.MaxDeltaSeconds"/>.</summary>
    internal void Accumulate(double deltaSeconds) => Accumulator += Math.Max(0, deltaSeconds);

    /// <summary>Takes one step's time out of the accumulator, if a whole step is there.</summary>
    /// <returns>Whether a step should run.</returns>
    internal bool TryStep()
    {
        if (Accumulator < StepSeconds) return false;
        Accumulator -= StepSeconds;
        return true;
    }
}

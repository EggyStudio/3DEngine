using System.Numerics;

namespace Engine;

/// <summary>Tunable physics simulation parameters. Insert as a resource before adding the physics plugin to override defaults.</summary>
public sealed class PhysicsSettings
{
    /// <summary>Whether the simulation is held still, running no step, so bodies keep their poses and velocities until it resumes.</summary>
    public bool Paused { get; set; }

    /// <summary>Gravity vector (m/s²). Default: (0, -9.81, 0).</summary>
    public Vector3 Gravity { get; set; } = new(0f, -9.81f, 0f);

    /// <summary>Fixed simulation timestep when <see cref="UseFixedTimestep"/> is <c>true</c>. Default 1/60 s.</summary>
    public float FixedTimeStep { get; set; } = 1f / 60f;

    /// <summary>Solver substep count per timestep (higher = more accurate, slower). Default 1.</summary>
    public int SubstepCount { get; set; } = 1;

    /// <summary>Velocity solver iterations per substep. Default 8.</summary>
    public int VelocityIterations { get; set; } = 8;

    /// <summary>Whether to use a deterministic fixed-step accumulator. Default <c>true</c>.</summary>
    public bool UseFixedTimestep { get; set; } = true;

    /// <summary>Maximum number of fixed steps consumed per frame to avoid the spiral of death. Default 8.</summary>
    public int MaxStepsPerFrame { get; set; } = 8;

    /// <summary>Number of worker threads the simulation steps on, 4 to begin with, and <c>0</c> for one fewer than the processors.</summary>
    /// <remarks>
    /// The step is deterministic for a given number of workers, and the same number on every
    /// machine keeps a game's physics the same on every machine, which a count taken from the
    /// processors would not. A machine of fewer cores runs them interleaved, to the same result.
    /// A world of fewer bodies awake than <see cref="ThreadedAbove"/> steps on the calling thread.
    /// </remarks>
    public int WorkerThreads { get; set; } = 4;

    /// <summary>How many bodies awake a step needs before it runs on the workers rather than the calling thread, 500 to begin with.</summary>
    /// <remarks>
    /// Measured on a 16-core machine, four workers step 2000 boxes in 1.4 ms where one takes 2.6, and
    /// 2000 characters in 5.0 where one takes 6.0, while 290 of either take the same on one as on
    /// four, and <c>games/Swarm</c>'s crowd of 180 took longer on four. The choice follows from the
    /// world, so a run is stepped the same way each time.
    /// </remarks>
    public int ThreadedAbove { get; set; } = 500;

    /// <summary>
    /// Whether a body's <see cref="Transform"/> is blended between its pose before and after the
    /// last fixed step by <see cref="FixedTime.Alpha"/>, so it moves smoothly when frames come
    /// more often than steps. It is drawn up to one step behind the simulation. Default <c>true</c>.
    /// </summary>
    public bool Interpolate { get; set; } = true;
}
using System.Numerics;

namespace Engine;

/// <summary>Tunable physics simulation parameters. Insert as a resource before adding the physics plugin to override defaults.</summary>
public sealed class PhysicsSettings
{
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

    /// <summary>Number of worker threads used by the simulation. <c>0</c> = <c>Environment.ProcessorCount - 1</c>. Default 1 (deterministic, test-friendly).</summary>
    public int WorkerThreads { get; set; } = 1;
}
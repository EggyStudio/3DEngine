using System.Numerics;
using BepuPhysics;
using BepuUtilities;
using BepuUtilities.Memory;

namespace Engine;

/// <summary>
/// BepuPhysics v2 implementation of <see cref="PhysicsWorld"/>. Owns the <see cref="Simulation"/>,
/// <see cref="BufferPool"/>, and worker <see cref="ThreadDispatcher"/>; translates the engine-agnostic
/// façade into Bepu calls and writes simulated poses back into the ECS each frame.
/// </summary>
/// <remarks>
/// The implementation is split across several partial files for readability:
/// <list type="bullet">
///   <item><description><c>PhysicsWorld.cs</c> – construction, gravity, disposal.</description></item>
///   <item><description><c>PhysicsWorld.Creation.cs</c> – body / shape creation factories.</description></item>
///   <item><description><c>PhysicsWorld.Bodies.cs</c> – per-body pose / velocity / impulse operations.</description></item>
///   <item><description><c>PhysicsWorld.Queries.cs</c> – raycasts and other spatial queries.</description></item>
///   <item><description><c>PhysicsWorld.Step.cs</c> – per-frame stepping and ECS transform sync.</description></item>
/// </list>
/// </remarks>
public sealed partial class PhysicsWorld : IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.Physics.Bepu");

    /// <summary>Underlying Bepu simulation owning bodies, statics, shapes, and the solver.</summary>
    public Simulation Simulation { get; }

    /// <summary>Buffer pool used by Bepu for all internal allocations.</summary>
    public BufferPool BufferPool { get; }

    /// <summary>Worker thread dispatcher driving Bepu's parallel solve / broadphase.</summary>
    public ThreadDispatcher Dispatcher { get; }

    private readonly PhysicsSettings _settings;

    /// <summary>Maps a Bepu <see cref="BodyHandle"/>.Value to the owning ECS entity (0 = none).</summary>
    private readonly Dictionary<int, int> _bodyToEntity = new();

    /// <summary>Maps a Bepu <see cref="StaticHandle"/>.Value to the owning ECS entity (0 = none).</summary>
    private readonly Dictionary<int, int> _staticToEntity = new();

    /// <summary>Time accumulator for the fixed-timestep integrator.</summary>
    private float _accumulator;

    /// <summary>Creates a physics world using default <see cref="PhysicsSettings"/>.</summary>
    public PhysicsWorld() : this(new PhysicsSettings()) { }

    public PhysicsWorld(PhysicsSettings settings)
    {
        _settings = settings;
        BufferPool = new BufferPool();
        var workers = settings.WorkerThreads <= 0
            ? Math.Max(1, Environment.ProcessorCount - 1)
            : settings.WorkerThreads;
        Dispatcher = new ThreadDispatcher(workers);
        var narrowCallbacks = BepuNarrowPhaseCallbacks.Default();
        var integrator = new BepuPoseIntegratorCallbacks
        {
            Gravity = settings.Gravity,
            LinearDamping = 0f,
            AngularDamping = 0f,
        };
        Simulation = Simulation.Create(
            BufferPool,
            narrowCallbacks,
            integrator,
            new SolveDescription(settings.VelocityIterations, settings.SubstepCount));
        Logger.Info(
            $"PhysicsWorld: created (workers={workers}, gravity={settings.Gravity}, fixedStep={settings.FixedTimeStep}, substeps={settings.SubstepCount}).");
    }

    /// <inheritdoc />
    public Vector3 Gravity
    {
        get => CallbacksRef.Gravity;
        set
        {
            CallbacksRef.Gravity = value;
            _settings.Gravity = value;
        }
    }

    /// <summary>Mutable reference to the live pose-integrator callbacks struct (gravity, damping).</summary>
    private ref BepuPoseIntegratorCallbacks CallbacksRef => 
        ref ((PoseIntegrator<BepuPoseIntegratorCallbacks>)Simulation.PoseIntegrator).Callbacks;

    /// <inheritdoc />
    public void Dispose()
    {
        Logger.Info("PhysicsWorld: disposing simulation.");
        Simulation.Dispose();
        Dispatcher.Dispose();
        BufferPool.Clear();
    }
}
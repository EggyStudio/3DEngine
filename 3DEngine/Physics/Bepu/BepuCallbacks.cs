using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;

namespace Engine;

/// <summary>
/// Per-pair material accept/configure callbacks. Filters out static-static and
/// kinematic-static pairs and applies a single global friction/restitution.
/// </summary>
internal struct BepuNarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    public SpringSettings ContactSpringiness;
    public float Friction;
    public float Restitution;
    public float MaximumRecoveryVelocity;

    public static BepuNarrowPhaseCallbacks Default() => new()
    {
        ContactSpringiness = new SpringSettings(30, 1),
        Friction = 1f,
        Restitution = 0f,
        MaximumRecoveryVelocity = 2f,
    };

    public void Initialize(Simulation simulation) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b,
        ref float speculativeMargin)
        => a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold,
        out PairMaterialProperties pairMaterial)
        where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial.FrictionCoefficient = Friction;
        pairMaterial.MaximumRecoveryVelocity = MaximumRecoveryVelocity;
        pairMaterial.SpringSettings = ContactSpringiness;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB,
        ref ConvexContactManifold manifold) => true;

    public void Dispose() { }
}

/// <summary>Applies global gravity (and optional damping) to all dynamic bodies each substep.</summary>
internal struct BepuPoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    public Vector3 Gravity;
    public float LinearDamping;
    public float AngularDamping;

    private Vector3Wide _gravityDt;
    private Vector<float> _linearDampingDt;
    private Vector<float> _angularDampingDt;

    public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
    public bool AllowSubstepsForUnconstrainedBodies => false;
    public bool IntegrateVelocityForKinematics => false;

    public void Initialize(Simulation simulation) { }

    public void PrepareForIntegration(float dt)
    {
        _gravityDt = Vector3Wide.Broadcast(Gravity * dt);
        _linearDampingDt = new Vector<float>(MathF.Pow(MathF.Max(1e-7f, 1 - LinearDamping), dt));
        _angularDampingDt = new Vector<float>(MathF.Pow(MathF.Max(1e-7f, 1 - AngularDamping), dt));
    }

    public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt,
        ref BodyVelocityWide velocity)
    {
        velocity.Linear = (velocity.Linear + _gravityDt) * _linearDampingDt;
        velocity.Angular = velocity.Angular * _angularDampingDt;
    }
}
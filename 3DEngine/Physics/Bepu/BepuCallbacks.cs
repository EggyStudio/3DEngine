using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;

namespace Engine;

/// <summary>
/// The pairs of collidables touching during one step, gathered from the narrow phase's worker
/// threads into a list per worker, so recording one takes no lock.
/// </summary>
internal sealed class ContactCollector
{
    private readonly List<(CollidableReference A, CollidableReference B)>[] _byWorker;

    public ContactCollector(int workers) =>
        _byWorker = Enumerable.Range(0, Math.Max(1, workers)).Select(_ => new List<(CollidableReference, CollidableReference)>()).ToArray();

    public void Record(int workerIndex, CollidableReference a, CollidableReference b) =>
        _byWorker[workerIndex].Add((a, b));

    /// <summary>Every pair recorded since the last call, which it forgets.</summary>
    public IEnumerable<(CollidableReference A, CollidableReference B)> Take()
    {
        foreach (var list in _byWorker)
        {
            foreach (var pair in list) yield return pair;
            list.Clear();
        }
    }
}

/// <summary>Which body handles are characters, whose contacts the narrow phase gives no friction.</summary>
internal sealed class CharacterFlags
{
    private bool[] _byHandle = [];

    public void Set(int handle, bool character)
    {
        if (_byHandle.Length <= handle) Array.Resize(ref _byHandle, Math.Max(handle + 1, _byHandle.Length * 2));
        _byHandle[handle] = character;
    }

    public bool Is(CollidableReference collidable) =>
        collidable.Mobility != CollidableMobility.Static && collidable.BodyHandle.Value < _byHandle.Length && _byHandle[collidable.BodyHandle.Value];
}

/// <summary>
/// Per-pair material accept/configure callbacks. Filters out static-static and
/// kinematic-static pairs, applies a single global friction/restitution, and records every pair
/// whose manifold has a contact at or past touching into <see cref="Contacts"/>.
/// </summary>
internal struct BepuNarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    public SpringSettings ContactSpringiness;
    public float Friction;
    public float Restitution;
    public float MaximumRecoveryVelocity;
    public ContactCollector? Contacts;
    public CharacterFlags? Characters;

    /// <summary>The gap in world units below which a contact counts as touching.</summary>
    public const float TouchingGap = 0.01f;

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
        // A character slides along what it meets, and its controller decides how it walks.
        pairMaterial.FrictionCoefficient = Characters is not null && (Characters.Is(pair.A) || Characters.Is(pair.B)) ? 0 : Friction;
        pairMaterial.MaximumRecoveryVelocity = MaximumRecoveryVelocity;
        pairMaterial.SpringSettings = ContactSpringiness;

        // A speculative contact has a negative depth, for shapes close enough to meet within the
        // step. Shapes a hundredth of a unit apart or closer count as touching, since the solver
        // leaves a resting pair hovering about a depth of zero, which a strict test would see start
        // and end over and over.
        if (Contacts is not null)
            for (int i = 0; i < manifold.Count; i++)
                if (manifold.GetDepth(i) >= -TouchingGap)
                {
                    Contacts.Record(workerIndex, pair.A, pair.B);
                    break;
                }
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
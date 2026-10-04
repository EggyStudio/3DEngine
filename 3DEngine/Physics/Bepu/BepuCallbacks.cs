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
    private readonly List<(CollidableReference A, CollidableReference B, Vector3 Point, Vector3 Normal, float Speed, bool Touching)>[] _byWorker;

    public ContactCollector(int workers) =>
        _byWorker = Enumerable.Range(0, Math.Max(1, workers)).Select(_ => new List<(CollidableReference, CollidableReference, Vector3, Vector3, float, bool)>()).ToArray();

    /// <summary>
    /// Records a pair at a point in the world, with the normal from B toward A, the speed they close
    /// at along it, and whether they touch or only come near enough to meet within the step.
    /// </summary>
    public void Record(int workerIndex, CollidableReference a, CollidableReference b, Vector3 point, Vector3 normal, float speed, bool touching) =>
        _byWorker[workerIndex].Add((a, b, point, normal, speed, touching));

    /// <summary>Every pair recorded since the last call, which it forgets.</summary>
    public IEnumerable<(CollidableReference A, CollidableReference B, Vector3 Point, Vector3 Normal, float Speed, bool Touching)> Take()
    {
        foreach (var list in _byWorker)
        {
            foreach (var pair in list) yield return pair;
            list.Clear();
        }
    }
}

/// <summary>Which bodies and statics are triggers, whose contacts are reported and never pushed apart.</summary>
internal sealed class TriggerFlags
{
    private bool[] _bodies = [];
    private bool[] _statics = [];

    public void Set(PhysicsBody body, bool trigger)
    {
        ref var flags = ref body.Kind == BodyKind.Static ? ref _statics : ref _bodies;
        if (flags.Length <= body.Handle) Array.Resize(ref flags, Math.Max(body.Handle + 1, flags.Length * 2));
        flags[body.Handle] = trigger;
    }

    public bool Is(CollidableReference collidable)
    {
        var (flags, handle) = collidable.Mobility == CollidableMobility.Static
            ? (_statics, collidable.StaticHandle.Value)
            : (_bodies, collidable.BodyHandle.Value);
        return handle < flags.Length && flags[handle];
    }
}

/// <summary>Each body's and static's friction and bounce, where one was given, which the narrow phase mixes for a pair.</summary>
internal sealed class BodyMaterials
{
    private (float Friction, float Restitution, bool Set)[] _bodies = [];
    private (float Friction, float Restitution, bool Set)[] _statics = [];

    public void Set(PhysicsBody body, PhysicsMaterial material)
    {
        ref var table = ref body.Kind == BodyKind.Static ? ref _statics : ref _bodies;
        if (table.Length <= body.Handle) Array.Resize(ref table, Math.Max(body.Handle + 1, table.Length * 2));
        table[body.Handle] = (MathF.Max(0, material.Friction), Math.Clamp(material.Restitution, 0, 1), true);
    }

    /// <summary>Forgets a body's material, since its handle is given out again.</summary>
    public void Clear(PhysicsBody body)
    {
        var table = body.Kind == BodyKind.Static ? _statics : _bodies;
        if (body.Handle < table.Length) table[body.Handle] = default;
    }

    public (float Friction, float Restitution) Of(CollidableReference collidable, float friction, float restitution)
    {
        var (table, handle) = collidable.Mobility == CollidableMobility.Static
            ? (_statics, collidable.StaticHandle.Value)
            : (_bodies, collidable.BodyHandle.Value);
        return handle < table.Length && table[handle].Set ? (table[handle].Friction, table[handle].Restitution) : (friction, restitution);
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
/// whose manifold has a contact into <see cref="Contacts"/>, with whether it touches.
/// </summary>
internal struct BepuNarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    public SpringSettings ContactSpringiness;
    public float Friction;
    public float Restitution;
    public float MaximumRecoveryVelocity;
    public ContactCollector? Contacts;
    public CharacterFlags? Characters;
    public TriggerFlags? Triggers;
    public BodyMaterials? Materials;
    private Simulation? _simulation;

    /// <summary>The gap in world units below which a contact counts as touching.</summary>
    public const float TouchingGap = 0.01f;

    public static BepuNarrowPhaseCallbacks Default() => new()
    {
        ContactSpringiness = new SpringSettings(30, 1),
        Friction = 1f,
        Restitution = 0f,
        MaximumRecoveryVelocity = 2f,
    };

    public void Initialize(Simulation simulation) => _simulation = simulation;

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
        // A character slides along what it meets, and its controller decides how it walks. Two
        // bodies' frictions mix as the square root of their product, as Box2D mixes them, so ice
        // is slippery under rubber.
        var frictionA = Materials?.Of(pair.A, Friction, Restitution).Friction ?? Friction;
        var frictionB = Materials?.Of(pair.B, Friction, Restitution).Friction ?? Friction;
        pairMaterial.FrictionCoefficient = Characters is not null && (Characters.Is(pair.A) || Characters.Is(pair.B)) ? 0
            : MathF.Sqrt(frictionA * frictionB);
        pairMaterial.MaximumRecoveryVelocity = MaximumRecoveryVelocity;
        pairMaterial.SpringSettings = ContactSpringiness;

        // A speculative contact has a negative depth, for shapes close enough to meet within the
        // step. Shapes a hundredth of a unit apart or closer count as touching, since the solver
        // leaves a resting pair hovering about a depth of zero, which a strict test would see start
        // and end over and over. The deepest contact gives the pair's point and normal. A pair
        // only near is recorded too, since the solver slows a pair in the steps before it touches,
        // so the speed it closed at is read while it approaches.
        if (Contacts is not null && manifold.Count > 0)
        {
            int deepest = 0;
            for (int i = 1; i < manifold.Count; i++)
                if (manifold.GetDepth(i) > manifold.GetDepth(deepest)) deepest = i;
            manifold.GetContact(deepest, out var offset, out var normal, out var depth, out _);
            var point = PositionOf(pair.A) + offset;
            // The narrow phase runs before the solver, so these are the velocities of the step before.
            var closing = Vector3.Dot(VelocityAt(pair.B, point) - VelocityAt(pair.A, point), normal);
            Contacts.Record(workerIndex, pair.A, pair.B, point, normal, MathF.Max(0, closing), depth >= -TouchingGap);
        }

        // A trigger reports what it touches and holds nothing back.
        return Triggers is null || !(Triggers.Is(pair.A) || Triggers.Is(pair.B));
    }

    // Where a collidable is, which a contact's offset is measured from. The narrow phase reads
    // poses and does not move them, so reading one from its workers is safe.
    private readonly Vector3 PositionOf(CollidableReference collidable) =>
        _simulation is null ? Vector3.Zero
        : collidable.Mobility == CollidableMobility.Static ? _simulation.Statics[collidable.StaticHandle].Pose.Position
        : _simulation.Bodies[collidable.BodyHandle].Pose.Position;

    // How fast the collidable's surface moves at a point in the world, turning included. A static
    // never moves.
    private readonly Vector3 VelocityAt(CollidableReference collidable, Vector3 point)
    {
        if (_simulation is null || collidable.Mobility == CollidableMobility.Static) return Vector3.Zero;
        var body = _simulation.Bodies[collidable.BodyHandle];
        var velocity = body.Velocity;
        return velocity.Linear + Vector3.Cross(velocity.Angular, point - body.Pose.Position);
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
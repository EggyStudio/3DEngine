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

    private readonly List<(CollidableReference A, CollidableReference B, Vector3 Point, Vector3 Normal, float Speed, bool Touching)> _all = [];
    private readonly List<(CollidableReference A, CollidableReference B, Vector3 Point, Vector3 Normal, float Speed, bool Touching)> _sorted = [];
    private ulong[] _keys = [];
    private int[] _order = [];

    // Sorts by the pair packed into one number, which sorts as fast as numbers do, and by the
    // rest only within a pair met more than once, as a mesh's triangles are.
    private void SortByPair()
    {
        var count = _all.Count;
        if (_keys.Length < count)
        {
            _keys = new ulong[Math.Max(count, _keys.Length * 2)];
            _order = new int[_keys.Length];
        }
        for (int i = 0; i < count; i++)
        {
            _keys[i] = ((ulong)_all[i].A.Packed << 32) | _all[i].B.Packed;
            _order[i] = i;
        }
        Array.Sort(_keys, _order, 0, count);
        _sorted.Clear();
        for (int i = 0; i < count; i++) _sorted.Add(_all[_order[i]]);
        for (int start = 0; start < count;)
        {
            var end = start + 1;
            while (end < count && _keys[end] == _keys[start]) end++;
            if (end - start > 1) _sorted.Sort(start, end - start, SameKey.Instance);
            start = end;
        }
        _all.Clear();
        _all.AddRange(_sorted);
    }

    private sealed class SameKey : IComparer<(CollidableReference A, CollidableReference B, Vector3 Point, Vector3 Normal, float Speed, bool Touching)>
    {
        public static readonly SameKey Instance = new();

        public int Compare((CollidableReference A, CollidableReference B, Vector3 Point, Vector3 Normal, float Speed, bool Touching) x,
            (CollidableReference A, CollidableReference B, Vector3 Point, Vector3 Normal, float Speed, bool Touching) y)
        {
            var order = x.Touching.CompareTo(y.Touching);
            if (order == 0) order = x.Point.X.CompareTo(y.Point.X);
            if (order == 0) order = x.Point.Y.CompareTo(y.Point.Y);
            if (order == 0) order = x.Point.Z.CompareTo(y.Point.Z);
            if (order == 0) order = x.Normal.X.CompareTo(y.Normal.X);
            if (order == 0) order = x.Speed.CompareTo(y.Speed);
            return order;
        }
    }

    /// <summary>Every pair recorded since the last call, which it forgets, in an order of the pairs' own.</summary>
    /// <remarks>
    /// Which worker meets which pair changes from step to step, and a contact's bounce adds to its
    /// bodies' velocities, whose sum depends on the order it is added in. Sorted by the pair, then by
    /// where they met, the contacts are worked through in the same order every run.
    /// </remarks>
    public List<(CollidableReference A, CollidableReference B, Vector3 Point, Vector3 Normal, float Speed, bool Touching)> Take()
    {
        _all.Clear();
        foreach (var list in _byWorker)
        {
            _all.AddRange(list);
            list.Clear();
        }
        if (_byWorker.Length > 1 && _all.Count > 1) SortByPair();
        return _all;
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

/// <summary>
/// Each body's and static's layer, 0 unless set, and which of the 32 layers collide with which, all
/// of them to begin with, which the narrow phase asks of a pair before it makes their contacts.
/// </summary>
/// <remarks>Written only between steps, and read from the narrow phase's worker threads during one.</remarks>
internal sealed class CollisionLayers
{
    /// <summary>How many layers there are.</summary>
    public const int Count = 32;

    private byte[] _bodies = [];
    private byte[] _statics = [];
    // Bit b of entry a, set when layers a and b collide.
    private readonly uint[] _collides = Enumerable.Repeat(uint.MaxValue, Count).ToArray();

    public void Set(PhysicsBody body, int layer)
    {
        ref var table = ref body.Kind == BodyKind.Static ? ref _statics : ref _bodies;
        if (table.Length <= body.Handle) Array.Resize(ref table, Math.Max(body.Handle + 1, table.Length * 2));
        table[body.Handle] = (byte)layer;
    }

    public int Of(PhysicsBody body) => Of(body.Kind == BodyKind.Static ? _statics : _bodies, body.Handle);

    public int Of(CollidableReference collidable) => collidable.Mobility == CollidableMobility.Static
        ? Of(_statics, collidable.StaticHandle.Value)
        : Of(_bodies, collidable.BodyHandle.Value);

    private static int Of(byte[] table, int handle) => handle < table.Length ? table[handle] : 0;

    public void SetCollide(int a, int b, bool collide)
    {
        if (collide)
        {
            _collides[a] |= 1u << b;
            _collides[b] |= 1u << a;
        }
        else
        {
            _collides[a] &= ~(1u << b);
            _collides[b] &= ~(1u << a);
        }
    }

    public bool Collide(int a, int b) => (_collides[a] & (1u << b)) != 0;

    public bool Collide(CollidableReference a, CollidableReference b) => Collide(Of(a), Of(b));
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

/// <summary>
/// The pairs of bodies a joint holds together, which do not collide, since a joint already decides
/// how they move, and an axle inside its wheel would otherwise rub against it.
/// </summary>
internal sealed class JoinedPairs
{
    private readonly Dictionary<ulong, int> _pairs = [];
    private readonly Dictionary<int, ulong> _byJoint = [];

    private static ulong Key(int a, int b) => a < b ? ((ulong)(uint)a << 32) | (uint)b : ((ulong)(uint)b << 32) | (uint)a;

    public void Add(int joint, int a, int b)
    {
        var key = Key(a, b);
        _byJoint[joint] = key;
        _pairs[key] = _pairs.GetValueOrDefault(key) + 1;
    }

    public void Remove(int joint)
    {
        if (!_byJoint.Remove(joint, out var key)) return;
        if (--_pairs[key] <= 0) _pairs.Remove(key);
    }

    /// <summary>Forgets the joints of a destroyed body, which went with it.</summary>
    public void RemoveBody(int body)
    {
        foreach (var (joint, key) in _byJoint.ToArray())
            if ((int)(key >> 32) == body || (int)(uint)key == body) Remove(joint);
    }

    // Read by the narrow phase's workers, while nothing adds or removes, which happens between steps.
    public bool Has(CollidableReference a, CollidableReference b) =>
        _pairs.Count > 0 && a.Mobility != CollidableMobility.Static && b.Mobility != CollidableMobility.Static
        && _pairs.ContainsKey(Key(a.BodyHandle.Value, b.BodyHandle.Value));
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
    public JoinedPairs? Joined;
    public CollisionLayers? Layers;
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
        => (a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic) && Joined?.Has(a, b) != true
           && Layers?.Collide(a, b) != false;

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
        // Bepu shares a convex manifold's friction among its contacts, so a box resting on four
        // corners slid as if a quarter as rough, and the coefficient is scaled by the count to
        // hold back the weight times the friction, as the coefficient means.
        var contacts = manifold.Convex ? Math.Max(1, manifold.Count) : 1;
        pairMaterial.FrictionCoefficient = Characters is not null && (Characters.Is(pair.A) || Characters.Is(pair.B)) ? 0
            : MathF.Sqrt(frictionA * frictionB) * contacts;
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
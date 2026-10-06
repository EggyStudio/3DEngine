using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints.Contact;

namespace Engine;

/// <summary>Contact tracking, which turns the pairs touching in each step into contacts that start and end.</summary>
public sealed partial class PhysicsWorld
{
    // The pairs touching after the last step, by both collidables packed into one key, with the
    // bodies and entities as they were when the contact started, so an ended contact can name a
    // body that has since been destroyed.
    private readonly Dictionary<ulong, PhysicsContact> _touching = new(PairKeys.Instance);
    private readonly HashSet<ulong> _seen = new(PairKeys.Instance);
    private readonly List<ulong> _gone = [];
    private readonly List<PhysicsContact> _started = [];
    private readonly List<PhysicsContact> _ended = [];
    private readonly List<PhysicsContact> _handedStarted = [];
    private readonly List<PhysicsContact> _handedEnded = [];

    // The fastest each pair near but not yet touching has closed at, which its contact reports
    // when it starts, forgotten once it starts or leaves.
    private readonly Dictionary<ulong, float> _approaching = new(PairKeys.Instance);
    private readonly HashSet<ulong> _near = new(PairKeys.Instance);

    // Hashes a pair's key by all its bits. A ulong hashes as its halves XORed, which for a key of
    // two small handles side by side is the same for thousands of pairs in a crowd, so the sets
    // above slowed to lists, 2 microseconds a pair with 2000 bodies touching.
    private sealed class PairKeys : IEqualityComparer<ulong>
    {
        public static readonly PairKeys Instance = new();
        public bool Equals(ulong x, ulong y) => x == y;
        public int GetHashCode(ulong key) => (int)((key * 0x9E3779B97F4A7C15UL) >> 32);
    }

    /// <summary>
    /// Hands over the contacts that started and ended in the steps since the last call, in the
    /// order they happened, and forgets them.
    /// </summary>
    /// <remarks>
    /// A pair that starts and ends within those steps is in both lists. A pair resting until both
    /// its bodies sleep stays touching, although a sleeping pair is not tested, and a pair whose
    /// body is destroyed ends with the next step.
    /// </remarks>
    internal void TakeContacts(List<PhysicsContact> started, List<PhysicsContact> ended)
    {
        if (_started.Count == 0 && _ended.Count == 0) return;
        started.AddRange(_started);
        ended.AddRange(_ended);
        _started.Clear();
        _ended.Clear();
    }

    /// <summary>
    /// The contacts that started and ended since the last call, in lists this world keeps and
    /// reuses, so they are read before the next call.
    /// </summary>
    internal (List<PhysicsContact> Started, List<PhysicsContact> Ended) TakePendingContacts()
    {
        _handedStarted.Clear();
        _handedEnded.Clear();
        TakeContacts(_handedStarted, _handedEnded);
        return (_handedStarted, _handedEnded);
    }

    // Compares what the step that has run found touching with what touched before it.
    private void UpdateContacts()
    {
        _seen.Clear();
        _near.Clear();
        foreach (var (a, b, point, normal, speed, touching) in _contacts.Take())
        {
            ulong key = Key(a, b);
            if (!touching)
            {
                _near.Add(key);
                _approaching[key] = MathF.Max(speed, _approaching.GetValueOrDefault(key));
                continue;
            }
            if (!_seen.Add(key) || _touching.ContainsKey(key)) continue;

            _approaching.Remove(key, out var approached);
            var contact = new PhysicsContact(BodyOf(a), BodyOf(b), HandleOf(EntityOf(a)), HandleOf(EntityOf(b)), point, normal, MathF.Max(speed, approached));
            _touching[key] = contact;
            _started.Add(contact);
            Bounce(a, b, normal, contact.Speed);
        }
        if (_approaching.Count > _near.Count)
            foreach (var key in _approaching.Keys.Where(key => !_near.Contains(key)).ToArray()) _approaching.Remove(key);

        _gone.Clear();
        foreach (var (key, contact) in _touching)
            if (!_seen.Contains(key) && !Asleep(contact)) _gone.Add(key);
        foreach (var key in _gone)
        {
            _ended.Add(_touching[key]);
            _touching.Remove(key);
        }
    }

    /// <summary>The slowest a pair may close at and bounce, below which it settles, so a body at rest does not jitter.</summary>
    internal const float BounceThreshold = 0.5f;

    // Sends a pair that met apart at their bounce times the speed they closed at, as the solver,
    // which has no bounce of its own, has stopped them at the surface. The push is shared by their
    // masses, a static or kinematic body taking none of it.
    private void Bounce(CollidableReference a, CollidableReference b, Vector3 normal, float closing)
    {
        if (closing < BounceThreshold || _triggerFlags.Is(a) || _triggerFlags.Is(b) || _characterFlags.Is(a) || _characterFlags.Is(b)) return;
        var bounce = MathF.Max(_materials.Of(a, 1, 0).Restitution, _materials.Of(b, 1, 0).Restitution);
        if (bounce <= 0) return;

        var inverseA = InverseMass(a);
        var inverseB = InverseMass(b);
        if (inverseA + inverseB <= 0) return;
        // How fast they part along the normal now, which the solver left about zero.
        var parting = Vector3.Dot(LinearVelocity(a) - LinearVelocity(b), normal);
        var impulse = (bounce * closing - parting) / (inverseA + inverseB);
        if (impulse <= 0) return;
        Push(a, normal * impulse * inverseA);
        Push(b, -normal * impulse * inverseB);
    }

    private float InverseMass(CollidableReference collidable) =>
        collidable.Mobility == CollidableMobility.Dynamic && Simulation.Bodies.BodyExists(collidable.BodyHandle)
            ? Simulation.Bodies[collidable.BodyHandle].LocalInertia.InverseMass
            : 0;

    private Vector3 LinearVelocity(CollidableReference collidable) =>
        collidable.Mobility == CollidableMobility.Static || !Simulation.Bodies.BodyExists(collidable.BodyHandle)
            ? Vector3.Zero
            : Simulation.Bodies[collidable.BodyHandle].Velocity.Linear;

    private void Push(CollidableReference collidable, Vector3 change)
    {
        if (collidable.Mobility != CollidableMobility.Dynamic || change == Vector3.Zero) return;
        var body = Simulation.Bodies[collidable.BodyHandle];
        body.Velocity.Linear += change;
        Wake(body);
    }

    private readonly TriggerFlags _triggerFlags = new();
    private readonly CollisionLayers _layers = new();

    /// <summary>Puts a body on one of the 32 layers, which decides what it collides with, 0 to begin with.</summary>
    /// <remarks>
    /// A pair that sleeps is not tested again until something wakes it, so a body whose layer
    /// changes is woken, and a static wakes the bodies resting within its bounds.
    /// </remarks>
    internal void SetLayer(PhysicsBody body, int layer)
    {
        layer = Math.Clamp(layer, 0, CollisionLayers.Count - 1);
        if (_layers.Of(body) == layer) return;
        _layers.Set(body, layer);
        WakeAround(body);
    }

    /// <summary>The layer a body is on.</summary>
    internal int GetLayer(PhysicsBody body) => _layers.Of(body);

    /// <summary>Whether bodies on two layers collide, which every pair does to begin with.</summary>
    /// <remarks>The bodies asleep on either layer are woken by a change, so their sleeping pairs are tested again.</remarks>
    internal void SetLayersCollide(int a, int b, bool collide)
    {
        a = Math.Clamp(a, 0, CollisionLayers.Count - 1);
        b = Math.Clamp(b, 0, CollisionLayers.Count - 1);
        if (_layers.Collide(a, b) == collide) return;
        _layers.SetCollide(a, b, collide);

        // Every pair the change touches has a body on one of the two layers, a static never
        // sleeping, so waking those bodies is enough.
        _woken.Clear();
        for (int set = 1; set < Simulation.Bodies.Sets.Length; set++)
        {
            ref var sleeping = ref Simulation.Bodies.Sets[set];
            if (!sleeping.Allocated) continue;
            for (int i = 0; i < sleeping.Count; i++)
            {
                var handle = sleeping.IndexToHandle[i];
                var layer = _layers.Of(new CollidableReference(CollidableMobility.Dynamic, handle));
                if (layer == a || layer == b) _woken.Add(handle);
            }
        }
        foreach (var handle in _woken) Wake(Simulation.Bodies.GetBodyReference(handle));
    }

    /// <summary>
    /// Makes a body a trigger, which reports what it touches as contacts that start and end and
    /// pushes nothing, or a solid body again.
    /// </summary>
    /// <remarks>
    /// A trigger reports the dynamic bodies that meet it, as a static or kinematic body sees no
    /// other. A change wakes the body, or what rests within a static's bounds, as a layer's does.
    /// </remarks>
    internal void SetTrigger(PhysicsBody body, bool trigger)
    {
        if (_triggerFlags.Is(CollidableOf(body)) == trigger) return;
        _triggerFlags.Set(body, trigger);
        WakeAround(body);
    }

    // The sleeping bodies a layer set by SetLayersCollide wakes, gathered before any is woken,
    // since waking one moves the sets being read.
    private readonly List<BodyHandle> _woken = [];

    // Wakes a body, or for a static the bodies whose bounds meet its own. A sleeping body's bounds
    // are kept in the broad phase's tree of statics, which is where they are looked for.
    private void WakeAround(PhysicsBody body)
    {
        if (!Exists(body)) return;
        if (body.Kind != BodyKind.Static)
        {
            Wake(Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)));
            return;
        }

        var bounds = Simulation.Statics.GetStaticReference(new StaticHandle(body.Handle)).BoundingBox;
        var sleepers = new SleepingBodies { Found = _woken };
        _woken.Clear();
        Simulation.BroadPhase.GetOverlaps(bounds.Min, bounds.Max, BufferPool, ref sleepers);
        foreach (var handle in _woken)
            if (Simulation.Bodies.BodyExists(handle)) Wake(Simulation.Bodies.GetBodyReference(handle));
    }

    private struct SleepingBodies : BepuUtilities.IBreakableForEach<CollidableReference>
    {
        public List<BodyHandle> Found;

        public bool LoopBody(CollidableReference collidable)
        {
            if (collidable.Mobility != CollidableMobility.Static) Found.Add(collidable.BodyHandle);
            return true;
        }
    }

    // The same key whichever way round the narrow phase handed the pair over.
    private static ulong Key(CollidableReference a, CollidableReference b)
    {
        ulong x = a.Packed, y = b.Packed;
        return x < y ? (x << 32) | y : (y << 32) | x;
    }

    // Whether the pair went untested because it sleeps, which is when every body in it that is not
    // static still exists and is in a sleeping set.
    private bool Asleep(PhysicsContact contact) => Sleeping(contact.BodyA) && Sleeping(contact.BodyB);

    private bool Sleeping(PhysicsBody body)
    {
        if (body.Kind == BodyKind.Static) return Simulation.Statics.StaticExists(new StaticHandle(body.Handle));
        return Simulation.Bodies.BodyExists(new BodyHandle(body.Handle))
            && Simulation.Bodies.HandleToLocation[body.Handle].SetIndex > 0;
    }

    /// <summary>
    /// The push the last step's solver gave two touching bodies along their contacts' normals, in
    /// mass times units a second, or 0 for a pair not touching.
    /// </summary>
    /// <remarks>
    /// It says how hard a pair presses as well as how hard it met, as a stack's weight on what holds
    /// it, where a contact's speed says only how fast they closed. Divided by the step it is the
    /// force between them. A sleeping pair's contact is kept with its island rather than in the
    /// narrow phase's map, and nothing between them changes while it sleeps, so a pair asked about
    /// before it slept is answered with what it was then. One asked about first while asleep is
    /// woken by the question and answered from its next step.
    /// </remarks>
    internal float GetContactImpulse(PhysicsBody a, PhysicsBody b)
    {
        if (!Exists(a) || !Exists(b)) return 0;
        var (ra, rb) = (CollidableOf(a), CollidableOf(b));
        var key = ra.Packed < rb.Packed ? ((ulong)ra.Packed << 32) | rb.Packed : ((ulong)rb.Packed << 32) | ra.Packed;
        ref var mapping = ref Simulation.NarrowPhase.PairCache.Mapping;
        var pair = new BepuPhysics.CollisionDetection.CollidablePair(ra, rb);
        var found = mapping.TryGetValue(ref pair, out var cache);
        if (!found)
        {
            pair = new BepuPhysics.CollisionDetection.CollidablePair(rb, ra);
            found = mapping.TryGetValue(ref pair, out cache);
        }
        if (found && Simulation.Solver.ConstraintExists(cache.ConstraintHandle))
        {
            // The sum of each contact's push along its normal, the friction along the surface and
            // the twist about the normal left out, being other numbers in other units.
            var sum = new PushSum();
            if (Simulation.NarrowPhase.TryExtractSolverContactData(cache.ConstraintHandle, ref sum))
                return _impulses[key] = sum.Total;
        }

        var sleeping = false;
        foreach (var body in new[] { a, b })
            sleeping |= body.Kind != BodyKind.Static && !Simulation.Bodies[new BodyHandle(body.Handle)].Awake;
        if (!sleeping)
        {
            _impulses.Remove(key);
            return 0;
        }
        if (_impulses.TryGetValue(key, out var rested)) return rested;
        foreach (var body in new[] { a, b })
            if (body.Kind != BodyKind.Static) Wake(Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)));
        return 0;
    }

    // The last impulse of each pair asked about, by its two collidables, which a pair asleep since
    // is answered with, and forgotten for a body that goes, whose handle is given out again.
    private readonly Dictionary<ulong, float> _impulses = [];

    private void ForgetImpulses(PhysicsBody body)
    {
        if (_impulses.Count == 0) return;
        var packed = (ulong)CollidableOf(body).Packed;
        foreach (var key in _impulses.Keys.Where(k => k >> 32 == packed || (k & 0xFFFFFFFF) == packed).ToArray()) _impulses.Remove(key);
    }

    private static CollidableReference CollidableOf(PhysicsBody body) => body.Kind switch
    {
        BodyKind.Static => new CollidableReference(new StaticHandle(body.Handle)),
        BodyKind.Kinematic => new CollidableReference(CollidableMobility.Kinematic, new BodyHandle(body.Handle)),
        _ => new CollidableReference(CollidableMobility.Dynamic, new BodyHandle(body.Handle)),
    };

    // Adds a contact constraint's penetration impulses, which Bepu keeps in the first lane of each
    // of its wide numbers for the constraint handed over.
    private struct PushSum : ISolverContactDataExtractor
    {
        public float Total;

        public void ConvexOneBody<TPrestep, TAccumulatedImpulses>(BodyHandle a, ref TPrestep prestep, ref TAccumulatedImpulses impulses)
            where TPrestep : struct, IConvexContactPrestep<TPrestep>
            where TAccumulatedImpulses : struct, IConvexContactAccumulatedImpulses<TAccumulatedImpulses> => AddConvex(ref impulses);

        public void ConvexTwoBody<TPrestep, TAccumulatedImpulses>(BodyHandle a, BodyHandle b, ref TPrestep prestep, ref TAccumulatedImpulses impulses)
            where TPrestep : struct, ITwoBodyConvexContactPrestep<TPrestep>
            where TAccumulatedImpulses : struct, IConvexContactAccumulatedImpulses<TAccumulatedImpulses> => AddConvex(ref impulses);

        public void NonconvexOneBody<TPrestep, TAccumulatedImpulses>(BodyHandle a, ref TPrestep prestep, ref TAccumulatedImpulses impulses)
            where TPrestep : struct, INonconvexContactPrestep<TPrestep>
            where TAccumulatedImpulses : struct, INonconvexContactAccumulatedImpulses<TAccumulatedImpulses> => AddNonconvex(ref impulses);

        public void NonconvexTwoBody<TPrestep, TAccumulatedImpulses>(BodyHandle a, BodyHandle b, ref TPrestep prestep, ref TAccumulatedImpulses impulses)
            where TPrestep : struct, ITwoBodyNonconvexContactPrestep<TPrestep>
            where TAccumulatedImpulses : struct, INonconvexContactAccumulatedImpulses<TAccumulatedImpulses> => AddNonconvex(ref impulses);

        private void AddConvex<T>(ref T impulses) where T : struct, IConvexContactAccumulatedImpulses<T>
        {
            for (int i = 0; i < T.ContactCount; i++)
                Total += T.GetPenetrationImpulseForContact(ref impulses, i)[0];
        }

        private void AddNonconvex<T>(ref T impulses) where T : struct, INonconvexContactAccumulatedImpulses<T>
        {
            for (int i = 0; i < T.ContactCount; i++)
                Total += T.GetImpulsesForContact(ref impulses, i).Penetration[0];
        }
    }

    private PhysicsBody BodyOf(CollidableReference collidable) => collidable.Mobility switch
    {
        CollidableMobility.Static => new PhysicsBody(this, collidable.StaticHandle.Value, BodyKind.Static),
        CollidableMobility.Kinematic => new PhysicsBody(this, collidable.BodyHandle.Value, BodyKind.Kinematic),
        _ => new PhysicsBody(this, collidable.BodyHandle.Value, BodyKind.Dynamic),
    };

    /// <summary>
    /// Turns the entity id a body was made with into a handle, set by <see cref="PhysicsPlugin"/>
    /// from the <see cref="EcsWorld"/>. Without it a contact names no entity.
    /// </summary>
    internal Func<int, Entity>? EntityHandle { get; set; }

    private Entity HandleOf(int id) => id != 0 && EntityHandle is { } handle ? handle(id) : Entity.None;

    /// <summary>The entity id a body was made for, or 0.</summary>
    internal int EntityOf(PhysicsBody body) => body.Kind == BodyKind.Static
        ? _staticToEntity.GetValueOrDefault(body.Handle)
        : _bodyToEntity.GetValueOrDefault(body.Handle);

    private int EntityOf(CollidableReference collidable) =>
        collidable.Mobility == CollidableMobility.Static
            ? _staticToEntity.GetValueOrDefault(collidable.StaticHandle.Value)
            : _bodyToEntity.GetValueOrDefault(collidable.BodyHandle.Value);
}

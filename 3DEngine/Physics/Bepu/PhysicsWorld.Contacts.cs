using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;

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
    public void TakeContacts(List<PhysicsContact> started, List<PhysicsContact> ended)
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
    public const float BounceThreshold = 0.5f;

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
        body.Awake = true;
    }

    private readonly TriggerFlags _triggerFlags = new();

    /// <summary>
    /// Makes a body a trigger, which reports what it touches as contacts that start and end and
    /// pushes nothing, or a solid body again.
    /// </summary>
    /// <remarks>A trigger reports the dynamic bodies that meet it, as a static or kinematic body sees no other.</remarks>
    public void SetTrigger(PhysicsBody body, bool trigger) => _triggerFlags.Set(body, trigger);

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
    public Func<int, Entity>? EntityHandle { get; set; }

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

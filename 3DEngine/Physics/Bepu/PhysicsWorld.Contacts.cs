using BepuPhysics;
using BepuPhysics.Collidables;

namespace Engine;

/// <summary>Contact tracking, which turns the pairs touching in each step into contacts that start and end.</summary>
public sealed partial class PhysicsWorld
{
    // The pairs touching after the last step, by both collidables packed into one key, with the
    // bodies and entities as they were when the contact started, so an ended contact can name a
    // body that has since been destroyed.
    private readonly Dictionary<ulong, PhysicsContact> _touching = [];
    private readonly HashSet<ulong> _seen = [];
    private readonly List<ulong> _gone = [];
    private readonly List<PhysicsContact> _started = [];
    private readonly List<PhysicsContact> _ended = [];

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

    // Compares what the step that has run found touching with what touched before it.
    private void UpdateContacts()
    {
        _seen.Clear();
        foreach (var (a, b) in _contacts.Take())
        {
            ulong key = Key(a, b);
            if (!_seen.Add(key) || _touching.ContainsKey(key)) continue;

            var contact = new PhysicsContact(BodyOf(a), BodyOf(b), HandleOf(EntityOf(a)), HandleOf(EntityOf(b)));
            _touching[key] = contact;
            _started.Add(contact);
        }

        _gone.Clear();
        foreach (var (key, contact) in _touching)
            if (!_seen.Contains(key) && !Asleep(contact)) _gone.Add(key);
        foreach (var key in _gone)
        {
            _ended.Add(_touching[key]);
            _touching.Remove(key);
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

    private int EntityOf(CollidableReference collidable) =>
        collidable.Mobility == CollidableMobility.Static
            ? _staticToEntity.GetValueOrDefault(collidable.StaticHandle.Value)
            : _bodyToEntity.GetValueOrDefault(collidable.BodyHandle.Value);
}

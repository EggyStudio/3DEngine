using System.Numerics;
using Engine;
using static Engine.E3D;

/// <summary>
/// The bridge the player builds over the gap, seen from the side, x across and y up, every plank a
/// box between two nodes of a grid a meter apart, joined at a node it shares with other planks by
/// hinges across the bridge and to the cliffs by hinges to kinematic anchors, with ropes as
/// distance joints from the towers. A joint that holds its planks together harder than its
/// strength breaks.
/// </summary>
public sealed class Bridge
{
    // The gap runs from -4 to 4, and the cliffs' tops stand a tenth above a plank's top, which lies
    // along its nodes.
    public const float Gap = 4, CliffTop = 0.1f;
    public const float PlankThickness = 0.2f, PlankDepth = 2, PlankMass = 20, MaxPlank = 2.3f;
    public const int MostPlanks = 24, MostRopes = 4;

    // The nodes fixed to the world, at the cliffs' edges, on their faces below and on the towers.
    public static readonly Vector2[] Anchors = [new(-Gap, 0), new(Gap, 0), new(-Gap, -2), new(Gap, -2), new(-Gap, 3), new(Gap, 3)];
    public static bool IsTower(Vector2 node) => node.Y > 2;

    // The force a joint breaks past, in newtons. Under the load of 150 kilograms the truss's joints
    // held 4,326 at most and the road's 10,213, past 8,000 before the load was halfway across, and
    // the road's 5,767 under its own weight.
    public const float Strength = 7000;

    public readonly record struct Plank(Vector2 A, Vector2 B, PhysicsBody Body);

    /// <summary>
    /// A joint, the two bodies it holds, where it holds each in that body's own frame, and the most
    /// rope it allows.
    /// </summary>
    public sealed class Link(PhysicsJoint joint, PhysicsBody a, Vector3 localA, PhysicsBody b, Vector3 localB, float rope)
    {
        public PhysicsJoint Joint = joint;
        public readonly PhysicsBody A = a, B = b;
        public readonly Vector3 LocalA = localA, LocalB = localB;
        public readonly float Rope = rope;
        public bool Broken;
        public float Force;

        public Vector3 PointA => World(A, LocalA);
        public Vector3 PointB => World(B, LocalB);

        private static Vector3 World(PhysicsBody body, Vector3 local) =>
            GetPhysicsBodyPosition(body) + Vector3.Transform(local, GetPhysicsBodyRotation(body));
    }

    public readonly List<Plank> Planks = [];
    public readonly List<Link> Links = [];
    public int Ropes => Links.Count(l => l.Rope > 0);
    public int Broken => Links.Count(l => l.Broken);
    private readonly Dictionary<Vector2, PhysicsBody> _anchorBodies = [];

    public Bridge()
    {
        // The anchors stand behind the bridge's planks, so nothing meets them.
        foreach (var node in Anchors)
            _anchorBodies[node] = CreatePhysicsKinematicBox(new Vector3(node.X, node.Y, -PlankDepth), new Vector3(0.1f, 0.1f, 0.1f));
    }

    /// <summary>
    /// Whether a plank may run from a to b, two nodes no further apart than a plank and not already
    /// joined.
    /// </summary>
    public bool CanPlace(Vector2 a, Vector2 b) =>
        a != b && Vector2.Distance(a, b) <= MaxPlank && Planks.Count < MostPlanks &&
        !Planks.Any(p => p.A == a && p.B == b || p.A == b && p.B == a) && !IsTower(a) && !IsTower(b);

    /// <summary>
    /// Lays a plank from a to b and joins it at each end, to an anchor there or to a plank already
    /// ending there.
    /// </summary>
    public void Place(Vector2 a, Vector2 b)
    {
        var middle = (a + b) / 2;
        var along = b - a;
        var center = new Vector3(middle.X, middle.Y - PlankThickness / 2, 0);
        var body = CreatePhysicsBox(center, new Vector3(along.Length(), PlankThickness, PlankDepth), PlankMass);
        SetPhysicsBodyRotation(body, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.Atan2(along.Y, along.X)));
        // Planks meet only at their joints, where a truss's members would otherwise overlap.
        SetPhysicsBodyLayer(body, 1);
        Planks.Add(new Plank(a, b, body));
        Join(a, body);
        Join(b, body);
    }

    /// <summary>
    /// Hangs a rope from a tower to a node a plank ends at, as long as the two stand apart.
    /// </summary>
    public bool Hang(Vector2 tower, Vector2 node)
    {
        if (!IsTower(tower) || Ropes >= MostRopes) return false;
        var index = Planks.FindIndex(p => p.A == node || p.B == node);
        if (index < 0) return false;
        var plank = Planks[index];
        var anchor = _anchorBodies[tower];
        var from = new Vector3(tower.X, tower.Y, 0);
        var to = new Vector3(node.X, node.Y, 0);
        var length = Vector3.Distance(from, to);
        var joint = CreatePhysicsDistanceJoint(anchor, plank.Body, from, to, 0, length);
        Links.Add(new Link(joint, anchor, Local(anchor, from), plank.Body, Local(plank.Body, to), length));
        return true;
    }

    private void Join(Vector2 node, PhysicsBody body)
    {
        var point = new Vector3(node.X, node.Y, 0);
        // To the anchor where the node is one, or to the first plank already ending there.
        PhysicsBody other;
        if (_anchorBodies.TryGetValue(node, out var anchor)) other = anchor;
        else
        {
            var first = Planks.FindIndex(p => p.Body != body && (p.A == node || p.B == node));
            if (first < 0) return;
            other = Planks[first].Body;
        }
        // A hinge across the bridge rather than a ball joint, since a truss of ball joints in a
        // plane holds nothing out of it, and bowed sideways under its own weight and folded under
        // the load.
        var joint = CreatePhysicsHingeJoint(other, body, point, Vector3.UnitZ);
        Links.Add(new Link(joint, other, Local(other, point), body, Local(body, point), 0));
    }

    /// <summary>
    /// Breaks each joint that held harder than its strength over the last step, and says how many
    /// broke.
    /// </summary>
    public int Strain()
    {
        var broke = 0;
        foreach (var link in Links)
        {
            if (link.Broken) continue;
            link.Force = GetPhysicsJointForce(link.Joint);
            if (link.Force <= Strength) continue;
            DestroyPhysicsJoint(link.Joint);
            link.Broken = true;
            broke++;
        }
        return broke;
    }

    /// <summary>Takes every plank and joint away.</summary>
    public void Clear()
    {
        foreach (var link in Links.Where(l => !l.Broken && IsPhysicsJointValid(l.Joint))) DestroyPhysicsJoint(link.Joint);
        foreach (var plank in Planks) DestroyPhysicsBody(plank.Body);
        Links.Clear();
        Planks.Clear();
    }

    // A point in a body's own frame.
    private static Vector3 Local(PhysicsBody body, Vector3 point) =>
        Vector3.Transform(point - GetPhysicsBodyPosition(body), Quaternion.Inverse(GetPhysicsBodyRotation(body)));

    /// <summary>
    /// The planks of a bridge the game knows, a truss under the road or the road alone.
    /// </summary>
    public static (Vector2 A, Vector2 B)[] Design(string name) => name switch
    {
        // The road, a Warren truss of diagonals and a bottom chord under it, tied to the cliffs.
        "truss" =>
        [
            (new(-4, 0), new(-2, 0)), (new(-2, 0), new(0, 0)), (new(0, 0), new(2, 0)), (new(2, 0), new(4, 0)),
            (new(-4, 0), new(-3, -1)), (new(-3, -1), new(-2, 0)), (new(-2, 0), new(-1, -1)), (new(-1, -1), new(0, 0)),
            (new(0, 0), new(1, -1)), (new(1, -1), new(2, 0)), (new(2, 0), new(3, -1)), (new(3, -1), new(4, 0)),
            (new(-3, -1), new(-1, -1)), (new(-1, -1), new(1, -1)), (new(1, -1), new(3, -1)),
            (new(-4, -2), new(-3, -1)), (new(4, -2), new(3, -1)),
        ],
        // The road alone, which sags and breaks under the load.
        "road" => [(new(-4, 0), new(-2, 0)), (new(-2, 0), new(0, 0)), (new(0, 0), new(2, 0)), (new(2, 0), new(4, 0))],
        _ => [],
    };
}

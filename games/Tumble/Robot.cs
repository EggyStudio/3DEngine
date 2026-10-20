using System.Numerics;
using Engine;
using static Engine.E3D;

/// <summary>
/// The robot, a character controller drawn as a skinned model, its walk, run and stand blended by
/// its speed and its upper body aimed on a layer of its own, which falls as a ragdoll of capsules
/// when struck and stands up again by a blend from the pose it lies in.
/// </summary>
public sealed class Robot
{
    // The model is 3.6 units tall, drawn at half its size, so the robot stands 1.8 meters.
    public const float Scale = 0.5f, Height = 1.8f, CrouchHeight = 1.1f, Radius = 0.35f;
    public const float WalkSpeed = 1.6f, RunSpeed = 4.2f;

    public enum Phase { Up, Down, Rising }

    public Phase State { get; private set; } = Phase.Up;
    public PhysicsBody Body { get; private set; }
    public Vector3 Position { get; private set; }
    public float Yaw;
    public bool Crouched { get; private set; }
    public int Falls, Rises;

    private readonly Model _model;
    private readonly ModelAnimation _idle, _walk, _run, _punch, _sit, _stand;
    private readonly Dictionary<string, int> _bone = [];
    private float _clock, _punchLeft, _phaseTime, _restTime;
    private Vector3 _velocity;

    // The pose the robot was last given, in the model's space, which a fall builds the ragdoll on.
    private Transform[] _pose;

    // The upper body's clip, a keyframe of a clip with the torso and what hangs from it turned
    // toward the aim, and the ragdoll's, a keyframe the bodies fill each frame.
    private readonly ModelAnimation _aimed, _lying;

    public Robot(Model model, ModelAnimation[] clips, Vector3 feet)
    {
        _model = model;
        ModelAnimation Clip(string name) => clips.First(c => c.Name.EndsWith(name, StringComparison.Ordinal));
        (_idle, _walk, _run, _punch, _sit, _stand) = (Clip("Idle"), Clip("Walking"), Clip("Running"), Clip("Punch"), Clip("Sitting"), Clip("Standing"));
        for (int b = 0; b < _idle.Bones.Length; b++) _bone[_idle.Bones[b].Name] = b;
        _pose = (Transform[])_idle.KeyframePoses[0].Clone();
        _aimed = new ModelAnimation { Name = "Aimed", Bones = _idle.Bones, KeyframePoses = [(Transform[])_pose.Clone()] };
        _lying = new ModelAnimation { Name = "Lying", Bones = _idle.Bones, KeyframePoses = [(Transform[])_pose.Clone()] };
        Body = CreatePhysicsCharacter(feet, Radius, Height, 80);
        Position = feet;
    }

    /// <summary>Where the robot's chest is, which a cannon aims at.</summary>
    public Vector3 Chest => State == Phase.Down ? GetPhysicsBodyPosition(_parts[1].Body) : Position + new Vector3(0, Crouched ? 0.8f : 1.3f, 0);

    // -- Upright

    /// <summary>
    /// Walks the robot at a velocity along the ground, turning it to face the way it goes, crouched
    /// or standing, and aims its upper body at a point, as of this frame.
    /// </summary>
    public void Move(Vector3 velocity, bool crouch, Vector3 aimAt, float dt)
    {
        _clock += dt;
        if (State == Phase.Down)
        {
            UpdateRagdoll(dt);
            return;
        }
        if (State == Phase.Rising)
        {
            UpdateRise(dt);
            return;
        }

        // A crouch is the character's height, and it stands again only where nothing is over it.
        if (crouch != Crouched && SetPhysicsCharacterHeight(Body, crouch ? CrouchHeight : Height)) Crouched = crouch;
        if (Crouched) velocity *= 0.4f;
        MovePhysicsCharacter(Body, velocity);
        Position = GetPhysicsBodyPosition(Body) - new Vector3(0, (Crouched ? CrouchHeight : Height) / 2, 0);
        _velocity = GetPhysicsBodyVelocity(Body);
        var flat = new Vector2(velocity.X, velocity.Z);
        if (flat.Length() > 0.1f)
        {
            var want = MathF.Atan2(flat.X, flat.Y);
            Yaw += Wrap(want - Yaw) * MathF.Min(1, dt * 10);
        }

        // The legs, by how fast it goes, and the upper body over them, turned toward the aim
        // within a quarter turn either way, punching while a punch lasts.
        _punchLeft = MathF.Max(0, _punchLeft - dt);
        var speed = flat.Length();
        var (under, underAt) = Crouched ? (_sit, 18 / 60f)
            : speed < 0.1f ? (_idle, _clock % Length(_idle))
            : speed < (WalkSpeed + RunSpeed) / 2 ? (_walk, _clock * speed / WalkSpeed % Length(_walk))
            : (_run, _clock * speed / RunSpeed % Length(_run));
        var toAim = aimAt - Position;
        var aim = Math.Clamp(Wrap(MathF.Atan2(toAim.X, toAim.Z) - Yaw), -MathF.PI / 2, MathF.PI / 2);
        var (over, overAt) = _punchLeft > 0 ? (_punch, Length(_punch) - _punchLeft) : (_idle, _clock % Length(_idle));
        Aim(over, overAt, aim);
        UpdateModelAnimationLayer(_model, under, underAt, _aimed, 0, "Torso");
        Remember(under, underAt);
    }

    /// <summary>Starts a punch, the length of the clip, unless one is under way.</summary>
    public bool Punch()
    {
        if (State != Phase.Up || _punchLeft > 0) return false;
        _punchLeft = Length(_punch);
        return true;
    }

    /// <summary>Whether a punch is at the part of its swing that strikes.</summary>
    public bool Striking => _punchLeft > 0 && _punchLeft < Length(_punch) * 0.6f && _punchLeft > Length(_punch) * 0.3f;

    /// <summary>Where the punching fist is, in the world.</summary>
    public Vector3 Fist => Position + Vector3.Transform(new Vector3(0, 1.3f, 0.7f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, Yaw));

    // A keyframe of a clip with the torso and every bone below it turned about the torso's joint by
    // an angle about the vertical, which the layer puts over the legs.
    private void Aim(ModelAnimation clip, float seconds, float angle)
    {
        var source = clip.KeyframePoses[Math.Min(clip.KeyframeCount - 1, (int)(seconds * AnimationFps))];
        var target = _aimed.KeyframePoses[0];
        var torso = _bone["Torso"];
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle);
        var pivot = source[torso].Position;
        for (int b = 0; b < target.Length; b++)
        {
            target[b] = source[b];
            if (!Below(b, torso)) continue;
            target[b].Position = pivot + Vector3.Transform(source[b].Position - pivot, turn);
            target[b].Rotation = Quaternion.Normalize(turn * source[b].Rotation);
        }
    }

    private bool Below(int bone, int ancestor)
    {
        for (var b = bone; b >= 0; b = _idle.Bones[b].Parent)
            if (b == ancestor) return true;
        return false;
    }

    private void Remember(ModelAnimation clip, float seconds)
    {
        var frame = clip.KeyframePoses[Math.Min(clip.KeyframeCount - 1, (int)(seconds * AnimationFps))];
        Array.Copy(frame, _pose, _pose.Length);
    }

    /// <summary>Draws the robot where it stands, or where its ragdoll lies.</summary>
    public void Draw()
    {
        if (State == Phase.Down) DrawModelEx(_model, Vector3.Zero, Vector3.UnitY, 0, new Vector3(Scale), Color.White);
        else DrawModelEx(_model, Position, Vector3.UnitY, Yaw * 180 / MathF.PI, new Vector3(Scale), Color.White);
    }

    // -- The ragdoll

    // A part of the ragdoll, a capsule between two bones, and the joint holding it to the part it
    // hangs from.
    private sealed record Part(string Name, PhysicsBody Body, PhysicsJoint Joint);

    private readonly List<Part> _parts = [];

    // Each bone's place in the frame of the part that carries it, from the pose it fell in.
    private readonly Dictionary<int, (int Part, Matrix4x4 Offset)> _carried = [];

    // The parts, each the bones it runs between, its radius and its mass, and the part it hangs
    // from, the joint at its first bone. The feet hang from the root, so a lower leg runs to its
    // foot.
    private static readonly (string Name, string From, string To, float Radius, float Mass, string? Parent)[] Parts =
    [
        ("Hips", "Hips", "Torso", 0.22f, 15, null),
        ("Chest", "Torso", "Neck", 0.26f, 15, "Hips"),
        ("Head", "Neck", "Head", 0.2f, 5, "Chest"),
        ("UpperArm.L", "UpperArm.L", "LowerArm.L", 0.09f, 3, "Chest"),
        ("LowerArm.L", "LowerArm.L", "Palm2.L", 0.09f, 2, "UpperArm.L"),
        ("UpperArm.R", "UpperArm.R", "LowerArm.R", 0.09f, 3, "Chest"),
        ("LowerArm.R", "LowerArm.R", "Palm2.R", 0.09f, 2, "UpperArm.R"),
        ("UpperLeg.L", "UpperLeg.L", "LowerLeg.L", 0.11f, 8, "Hips"),
        ("LowerLeg.L", "LowerLeg.L", "Foot.L", 0.1f, 5, "UpperLeg.L"),
        ("UpperLeg.R", "UpperLeg.R", "LowerLeg.R", 0.11f, 8, "Hips"),
        ("LowerLeg.R", "LowerLeg.R", "Foot.R", 0.1f, 5, "UpperLeg.R"),
    ];

    /// <summary>
    /// Knocks the robot down, its character taken away and a ragdoll put where its bones stood,
    /// moving as it moved, struck at a point by an impulse.
    /// </summary>
    public void Fall(Vector3 at, Vector3 impulse)
    {
        if (State != Phase.Up) return;
        var place = World();
        DestroyPhysicsBody(Body);
        foreach (var (name, from, to, radius, mass, parent) in Parts)
        {
            var a = Vector3.Transform(_pose[_bone[from]].Position, place);
            var b = Vector3.Transform(_pose[_bone[to]].Position, place);
            // The head runs from the neck past the head's joint to the top of the head.
            if (name == "Head") b = a + Vector3.Normalize(b - a) * 0.45f;
            var along = b - a;
            var length = along.Length();
            var body = CreatePhysicsCapsule((a + b) / 2, radius, length + radius, mass);
            SetPhysicsBodyRotation(body, FromTo(Vector3.UnitY, along / length));
            SetPhysicsBodyLayer(body, Layer);
            SetPhysicsBodyVelocity(body, _velocity);
            var joint = PhysicsJoint.None;
            if (parent is not null)
            {
                var up = _parts.First(p => p.Name == parent).Body;
                var across = Vector3.TransformNormal(Vector3.UnitX, place);
                switch (name)
                {
                    // Knees bend the foot back, and elbows the hand forward, about the body's
                    // across, from how they stood.
                    case "LowerLeg.L" or "LowerLeg.R":
                        joint = CreatePhysicsHingeJoint(up, body, a, Vector3.Normalize(across));
                        SetPhysicsHingeLimits(joint, -15, 140);
                        break;
                    case "LowerArm.L" or "LowerArm.R":
                        joint = CreatePhysicsHingeJoint(up, body, a, Vector3.Normalize(across));
                        SetPhysicsHingeLimits(joint, -140, 15);
                        break;
                    default:
                        joint = CreatePhysicsBallJoint(up, body, a);
                        var (swing, twist) = name switch
                        {
                            "Chest" => (35f, 25f),
                            "Head" => (45f, 45f),
                            "UpperArm.L" or "UpperArm.R" => (85f, 60f),
                            _ => (75f, 25f),
                        };
                        SetPhysicsBallJointLimits(joint, along / length, swing, twist);
                        break;
                }
            }
            _parts.Add(new Part(name, body, joint));
        }

        // Each bone carried by the part it belongs to, the nearest of its own and its parents'.
        _carried.Clear();
        for (int bone = 0; bone < _pose.Length; bone++)
        {
            var part = PartOf(bone);
            var world = Matrix(_pose[bone]) * place;
            Matrix4x4.Invert(GetPhysicsBodyTransform(_parts[part].Body), out var inverse);
            _carried[bone] = (part, world * inverse);
        }

        // The part nearest the blow takes it.
        var struck = _parts.MinBy(p => Vector3.Distance(GetPhysicsBodyPosition(p.Body), at))!;
        ApplyPhysicsImpulseAt(struck.Body, impulse, at);
        (State, _phaseTime, _restTime) = (Phase.Down, 0, 0);
        Falls++;
    }

    // The ragdoll's layer, whose parts pass through one another, since a limb rests against the
    // chest and their capsules overlap where it does.
    public const int Layer = 3;

    // The bones a part carries that are not its own, the rest found by their parents.
    private static readonly Dictionary<string, string> Carriers = new()
    {
        ["Bone"] = "Hips", ["Body"] = "Hips", ["Hips"] = "Hips", ["Abdomen"] = "Hips",
        ["Torso"] = "Chest", ["Shoulder.L"] = "Chest", ["Shoulder.R"] = "Chest",
        ["Neck"] = "Head", ["Head"] = "Head",
        ["Foot.L"] = "LowerLeg.L", ["Foot.R"] = "LowerLeg.R",
    };

    private int PartOf(int bone)
    {
        for (var b = bone; b >= 0; b = _idle.Bones[b].Parent)
        {
            var name = _idle.Bones[b].Name;
            if (Carriers.TryGetValue(name, out var carrier)) return Index(carrier);
            if (Index(name) is var own and >= 0) return own;
        }
        return Index("Hips");
    }

    private int Index(string name) => _parts.FindIndex(p => p.Name == name);

    // Poses the model from the ragdoll's bodies, and stands it up once it has lain still a while.
    private void UpdateRagdoll(float dt)
    {
        _phaseTime += dt;
        LieAs(_lying.KeyframePoses[0], Matrix4x4.CreateScale(Scale));
        UpdateModelAnimation(_model, _lying, 0);
        var still = _parts.All(p => GetPhysicsBodyVelocity(p.Body).Length() < 0.4f);
        _restTime = still ? _restTime + dt : 0;
        if (_phaseTime > 1.5f && _restTime > 0.6f || _phaseTime > 6) Rise();
    }

    // Each bone where the bodies carry it, in the frame a model is drawn in.
    private void LieAs(Transform[] pose, Matrix4x4 drawn)
    {
        Matrix4x4.Invert(drawn, out var into);
        foreach (var (bone, (part, offset)) in _carried)
        {
            var world = offset * GetPhysicsBodyTransform(_parts[part].Body);
            Matrix4x4.Decompose(world * into, out _, out var rotation, out var position);
            pose[bone] = new Transform(position, Quaternion.Normalize(rotation), Vector3.One);
        }
    }

    // -- Standing up

    private const float SitUp = 0.7f, StandUp = 0.9f;

    // Takes the ragdoll away and puts the character where the hips lie, facing the feet, as one
    // sits up toward them, its pose blended from how it lies to the sitting start of the clip that
    // stands.
    private void Rise()
    {
        var hips = GetPhysicsBodyPosition(_parts[Index("Hips")].Body);
        var feet = (GetPhysicsBodyPosition(_parts[Index("LowerLeg.L")].Body) + GetPhysicsBodyPosition(_parts[Index("LowerLeg.R")].Body)) / 2;
        var toward = new Vector2(feet.X - hips.X, feet.Z - hips.Z);
        Yaw = toward.Length() > 0.05f ? MathF.Atan2(toward.X, toward.Y) : Yaw;
        // Sitting, the hips are a third of a meter behind where it stands.
        var facing = new Vector3(MathF.Sin(Yaw), 0, MathF.Cos(Yaw));
        Position = new Vector3(hips.X, 0, hips.Z) + facing * 0.33f;
        LieAs(_lying.KeyframePoses[0], World());
        foreach (var part in _parts) DestroyPhysicsBody(part.Body);
        _parts.Clear();
        _carried.Clear();
        Body = CreatePhysicsCharacter(Position, Radius, Height, 80);
        Crouched = false;
        (State, _phaseTime) = (Phase.Rising, 0);
    }

    private void UpdateRise(float dt)
    {
        _phaseTime += dt;
        MovePhysicsCharacter(Body, Vector3.Zero);
        if (_phaseTime < SitUp)
        {
            UpdateModelAnimationBlend(_model, _lying, 0, _stand, 0, Smooth(_phaseTime / SitUp));
            return;
        }
        var standing = (_phaseTime - SitUp) / StandUp * Length(_stand);
        UpdateModelAnimationAt(_model, _stand, MathF.Min(standing, Length(_stand) - 1f / AnimationFps));
        if (_phaseTime < SitUp + StandUp) return;
        Remember(_idle, 0);
        State = Phase.Up;
        Rises++;
    }

    // -- Helpers

    private Matrix4x4 World() =>
        Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateRotationY(Yaw) * Matrix4x4.CreateTranslation(Position);

    private static Matrix4x4 Matrix(Transform t) =>
        Matrix4x4.CreateScale(t.Scale) * Matrix4x4.CreateFromQuaternion(t.Rotation) * Matrix4x4.CreateTranslation(t.Position);

    private static float Length(ModelAnimation clip) => clip.KeyframeCount / (float)AnimationFps;

    private static float Smooth(float t) => t * t * (3 - 2 * t);

    private static float Wrap(float angle) => MathF.IEEERemainder(angle, MathF.Tau);

    private static Quaternion FromTo(Vector3 from, Vector3 to)
    {
        var axis = Vector3.Cross(from, to);
        var dot = Vector3.Dot(from, to);
        if (axis.LengthSquared() < 1e-8f) return dot > 0 ? Quaternion.Identity : Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
        return Quaternion.Normalize(new Quaternion(axis, 1 + dot));
    }
}

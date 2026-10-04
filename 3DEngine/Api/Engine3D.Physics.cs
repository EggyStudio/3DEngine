using System.Numerics;

namespace Engine;

/// <summary>A ray in world space, from a position along a direction, as raylib's.</summary>
public readonly record struct Ray(Vector3 Position, Vector3 Direction);

public static partial class Engine3D
{
    private static PhysicsWorld Physics => Res<PhysicsWorld>();

    // -- Bodies

    /// <summary>A box of <paramref name="size"/> that falls, collides and is pushed, centered at <paramref name="position"/>.</summary>
    public static PhysicsBody CreatePhysicsBox(Vector3 position, Vector3 size, float mass = 1) =>
        Physics.CreateBox(position, size / 2, mass);

    /// <summary>A ball that falls, collides, rolls and is pushed.</summary>
    public static PhysicsBody CreatePhysicsSphere(Vector3 position, float radius, float mass = 1) =>
        Physics.CreateSphere(position, radius, mass);

    /// <summary>A box that never moves, for floors, walls and level geometry.</summary>
    public static PhysicsBody CreatePhysicsStaticBox(Vector3 position, Vector3 size) =>
        Physics.CreateStaticBox(position, size / 2);

    /// <summary>An upright capsule <paramref name="height"/> units from end to end that falls, collides and is pushed.</summary>
    public static PhysicsBody CreatePhysicsCapsule(Vector3 position, float radius, float height, float mass = 1) =>
        Physics.CreateCapsule(position, radius, MathF.Max(0, height - 2 * radius), mass);

    /// <summary>
    /// A box that never moves and stops nothing, which reports what enters it as contacts that
    /// start and end, as a goal, a pickup or a door's sensor does.
    /// </summary>
    /// <remarks>It sees bodies that fall and are pushed, and characters, as a static body does.</remarks>
    public static PhysicsBody CreatePhysicsTrigger(Vector3 position, Vector3 size)
    {
        var body = Physics.CreateStaticBox(position, size / 2);
        Physics.SetTrigger(body, true);
        return body;
    }

    /// <summary>
    /// A body that never moves, shaped as every triangle of <paramref name="model"/> placed at
    /// <paramref name="position"/> and scaled as <c>DrawModel</c> places it, for level geometry
    /// a box does not fit.
    /// </summary>
    /// <remarks>A mesh collides only with bodies that move, and a triangle only from its front, the side its winding faces.</remarks>
    /// <returns>The body, or an invalid one when the model has no triangles, with the reason in the log.</returns>
    public static PhysicsBody CreatePhysicsStaticModel(Model model, Vector3 position, float scale = 1)
    {
        var world = model.Transform * Matrix4x4.CreateScale(scale);
        var points = new List<Vector3>();
        var triangles = new List<int>();
        foreach (var mesh in model.Meshes)
        {
            if (!Meshes.TryGetData(mesh.Id, out var vertices, out var indices)) continue;
            var first = points.Count;
            foreach (var vertex in vertices) points.Add(Vector3.Transform(vertex.Position, world));
            // A model's front faces wind counterclockwise and Bepu's clockwise, so each triangle is
            // turned over.
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                triangles.Add(first + (int)indices[i]);
                triangles.Add(first + (int)indices[i + 2]);
                triangles.Add(first + (int)indices[i + 1]);
            }
        }
        if (triangles.Count == 0)
        {
            ApiLogger.Warn("CreatePhysicsStaticModel: the model has no triangles loaded.");
            return default;
        }
        return Physics.CreateStaticMesh(position, points.ToArray(), triangles.ToArray());
    }

    // -- Joints, which hold bodies that move. A body held to the world is joined to a kinematic one.

    /// <summary>Joins two bodies at a point in the world, each free to turn about it, as a ball in a socket.</summary>
    /// <exception cref="ArgumentException">A body is static.</exception>
    public static PhysicsJoint CreatePhysicsBallJoint(PhysicsBody a, PhysicsBody b, Vector3 point) => Physics.CreateBallJoint(a, b, point);

    /// <summary>Joins two bodies at a point in the world, turning only around <paramref name="axis"/>, as a door on its hinge.</summary>
    /// <exception cref="ArgumentException">A body is static.</exception>
    public static PhysicsJoint CreatePhysicsHingeJoint(PhysicsBody a, PhysicsBody b, Vector3 point, Vector3 axis) =>
        Physics.CreateHingeJoint(a, b, point, axis);

    /// <summary>Joins two bodies rigidly, as they are placed when it is made.</summary>
    /// <exception cref="ArgumentException">A body is static.</exception>
    public static PhysicsJoint CreatePhysicsWeldJoint(PhysicsBody a, PhysicsBody b) => Physics.CreateWeldJoint(a, b);

    /// <summary>Keeps a point on each body between two distances apart, as a rope does with a minimum of 0.</summary>
    /// <exception cref="ArgumentException">A body is static, or the distances are out of order.</exception>
    public static PhysicsJoint CreatePhysicsDistanceJoint(PhysicsBody a, PhysicsBody b, Vector3 pointA, Vector3 pointB, float minimum, float maximum) =>
        Physics.CreateDistanceJoint(a, b, pointA, pointB, minimum, maximum);

    /// <summary>Keeps a hinge turned between two angles in degrees from where it was made, as a door that opens one way.</summary>
    /// <exception cref="ArgumentException">The joint is not a hinge, or the angles are out of order.</exception>
    public static void SetPhysicsHingeLimits(PhysicsJoint hinge, float minimumDegrees, float maximumDegrees) =>
        Physics.SetHingeLimit(hinge, float.DegreesToRadians(minimumDegrees), float.DegreesToRadians(maximumDegrees));

    /// <summary>Turns a hinge at a speed in degrees a second, with no more than a torque, as a wheel's drive does.</summary>
    /// <exception cref="ArgumentException">The joint is not a hinge.</exception>
    public static void SetPhysicsHingeMotor(PhysicsJoint hinge, float degreesPerSecond, float maximumTorque) =>
        Physics.SetHingeMotor(hinge, float.DegreesToRadians(degreesPerSecond), maximumTorque);

    /// <summary>
    /// Keeps a ball joint within a cone around <paramref name="axis"/>, in the world, swung no more
    /// than <paramref name="swingDegrees"/> and twisted no more than <paramref name="twistDegrees"/>
    /// either way from how the bodies are turned now, as a shoulder or a link of a chain. 180
    /// degrees or more leaves that part free.
    /// </summary>
    /// <exception cref="ArgumentException">The joint is not a ball joint, or an angle is below 0.</exception>
    public static void SetPhysicsBallJointLimits(PhysicsJoint ball, Vector3 axis, float swingDegrees, float twistDegrees) =>
        Physics.SetBallJointLimit(ball, axis, float.DegreesToRadians(swingDegrees), float.DegreesToRadians(twistDegrees));

    /// <summary>Changes how far apart a distance joint keeps its points, as a winch reeling a rope in does a little each frame.</summary>
    /// <exception cref="ArgumentException">The joint is not a distance joint, or the distances are out of order.</exception>
    public static void SetPhysicsDistanceJointRange(PhysicsJoint joint, float minimum, float maximum) =>
        Physics.SetDistanceJointRange(joint, minimum, maximum);

    /// <summary>The highest step a character climbs onto as it walks into it, its radius to begin with.</summary>
    public static void SetPhysicsCharacterStepHeight(PhysicsBody body, float height) => Physics.SetCharacterStepHeight(body, height);

    /// <summary>Makes a character a height with its feet where they are, as crouching and standing do.</summary>
    /// <returns>Whether it has the height, which it has not when it would stand up into a ceiling.</returns>
    public static bool SetPhysicsCharacterHeight(PhysicsBody body, float height) => Physics.SetCharacterHeight(body, height);

    /// <summary>Removes a joint.</summary>
    public static void DestroyPhysicsJoint(PhysicsJoint joint) => Physics.DestroyJoint(joint);

    /// <summary>Whether a joint exists, which it stops doing when it or one of its bodies is destroyed.</summary>
    public static bool IsPhysicsJointValid(PhysicsJoint joint) => Physics.JointExists(joint);

    /// <summary>
    /// Gives a body a friction, 0 for ice and 1 for rubber, and a bounce, 0 for none and 1 for a
    /// ball that comes back as fast as it fell. Two bodies mix their frictions and take the larger bounce.
    /// </summary>
    public static void SetPhysicsBodyMaterial(PhysicsBody body, float friction, float bounce) =>
        Physics.SetMaterial(body, PhysicsMaterial.Default with { Friction = friction, Restitution = bounce });

    /// <summary>Makes a body a trigger, which reports what it touches and stops nothing, or a solid body again.</summary>
    public static void SetPhysicsBodyTrigger(PhysicsBody body, bool trigger) => Physics.SetTrigger(body, trigger);

    /// <summary>A box moved only by the program, through <see cref="SetPhysicsBodyPosition"/> or <see cref="SetPhysicsBodyVelocity"/>, which pushes what it meets.</summary>
    public static PhysicsBody CreatePhysicsKinematicBox(Vector3 position, Vector3 size) =>
        Physics.CreateKinematicBox(position, size / 2);

    /// <summary>
    /// A character controller's body with its feet at <paramref name="feet"/>, an upright capsule
    /// walked by <see cref="MovePhysicsCharacter"/> that walls stop, that slides along them, rides
    /// over a low edge and holds still on a slope (PhysicsWorld.CreateCharacter says how).
    /// </summary>
    public static PhysicsBody CreatePhysicsCharacter(Vector3 feet, float radius, float height, float mass = 80) =>
        Physics.CreateCharacter(feet, radius, height, mass);

    /// <summary>The velocity a character walks at along the ground, until it is given another.</summary>
    /// <remarks>A character whose entity has a <see cref="CharacterController"/>, as one a scene file made, is walked through it.</remarks>
    public static void MovePhysicsCharacter(PhysicsBody body, Vector3 velocity)
    {
        if (Controller(body) is { } entity) Res<EcsWorld>().GetRef<CharacterController>(entity).Velocity = velocity;
        else Physics.MoveCharacter(body, velocity);
    }

    /// <summary>Makes a character on the ground jump, leaving it upward at <paramref name="speed"/>.</summary>
    public static void JumpPhysicsCharacter(PhysicsBody body, float speed)
    {
        if (Controller(body) is { } entity) Res<EcsWorld>().GetRef<CharacterController>(entity).Jump = speed;
        else Physics.JumpCharacter(body, speed);
    }

    // The entity a character body's CharacterController is on, which the physics step reads the
    // walk from, so a flat call goes through it rather than being overwritten by it.
    private static int? Controller(PhysicsBody body)
    {
        var entity = Physics.EntityOf(body);
        return entity != 0 && TryRes<EcsWorld>(out var ecs) && ecs.Has<CharacterController>(entity) ? entity : null;
    }

    /// <summary>Whether a character stands on ground it can walk on.</summary>
    public static bool IsPhysicsCharacterGrounded(PhysicsBody body) => Physics.IsCharacterGrounded(body);

    /// <summary>The steepest ground, in degrees, a character walks on, 45 to begin with.</summary>
    public static void SetPhysicsCharacterMaxSlope(PhysicsBody body, float degrees) => Physics.SetCharacterMaxSlope(body, degrees);

    /// <summary>Removes a body from the simulation.</summary>
    public static void DestroyPhysicsBody(PhysicsBody body)
    {
        if (body.IsValid) Physics.Destroy(body);
    }

    /// <summary>Whether a body exists in the simulation.</summary>
    public static bool IsPhysicsBodyValid(PhysicsBody body) => body.IsValid;

    // -- Pose and motion

    /// <summary>
    /// Where a body is to be drawn this frame, as a model's <see cref="Model.Transform"/>: its pose
    /// blended between its last two steps, so it moves smoothly whatever the frame rate.
    /// </summary>
    /// <example><code>box.Transform = GetPhysicsBodyTransform(body); DrawModel(box, Vector3.Zero, 1, Color.Orange);</code></example>
    public static Matrix4x4 GetPhysicsBodyTransform(PhysicsBody body)
    {
        var alpha = Res<PhysicsSettings>().Interpolate && TryRes<FixedTime>(out var fixedTime)
            ? (float)fixedTime.Alpha
            : 1f;
        var (position, rotation) = Physics.GetPose(body, alpha);
        return Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
    }

    /// <summary>Where a body is in the simulation, as of its last step.</summary>
    public static Vector3 GetPhysicsBodyPosition(PhysicsBody body) => Physics.GetPosition(body);

    /// <summary>Moves a body to a position at once, keeping its velocity.</summary>
    public static void SetPhysicsBodyPosition(PhysicsBody body, Vector3 position) => Physics.SetPosition(body, position);

    /// <summary>How fast and which way a body moves, in units a second.</summary>
    public static Vector3 GetPhysicsBodyVelocity(PhysicsBody body) => Physics.GetLinearVelocity(body);

    /// <summary>Sets how fast and which way a body moves, waking it.</summary>
    public static void SetPhysicsBodyVelocity(PhysicsBody body, Vector3 velocity) => Physics.SetLinearVelocity(body, velocity);

    /// <summary>Pushes a body at its center, changing its velocity by the impulse over its mass.</summary>
    public static void ApplyPhysicsImpulse(PhysicsBody body, Vector3 impulse) => Physics.ApplyImpulse(body, impulse, Vector3.Zero);

    /// <summary>Holds the simulation still, as a pause menu does, or lets it run again. Bodies keep their velocities across a pause.</summary>
    public static void SetPhysicsPaused(bool paused) => Res<PhysicsSettings>().Paused = paused;

    /// <summary>Whether the simulation is held still.</summary>
    public static bool IsPhysicsPaused() => Res<PhysicsSettings>().Paused;

    /// <summary>Sets the acceleration every dynamic body falls by, (0, -9.81, 0) to begin with.</summary>
    public static void SetPhysicsGravity(Vector3 gravity) => Physics.Gravity = gravity;

    // -- Queries

    /// <summary>The first body a ray meets within <paramref name="maxDistance"/>, with where and at what face.</summary>
    /// <returns>Whether the ray met a body.</returns>
    public static bool GetRayCollisionPhysics(Ray ray, float maxDistance, out RaycastHit hit)
    {
        hit = default;
        return ray.Direction != Vector3.Zero && Physics.Raycast(ray.Position, Vector3.Normalize(ray.Direction), maxDistance, out hit);
    }

    /// <summary>The pairs of bodies that started touching in this frame's steps.</summary>
    public static IReadOnlyList<ContactStarted> GetPhysicsContacts() => World.ReadEvents<ContactStarted>();

    /// <summary>Whether a body started touching anything in this frame's steps.</summary>
    public static bool IsPhysicsBodyHit(PhysicsBody body)
    {
        foreach (var contact in World.ReadEvents<ContactStarted>())
            if (contact.BodyA == body || contact.BodyB == body) return true;
        return false;
    }

    // -- Rays from the screen

    /// <summary>The ray from a camera through a point of the window, as a mouse click points into the scene.</summary>
    public static Ray GetScreenToWorldRay(Vector2 position, Camera3D camera) =>
        GetScreenToWorldRayEx(position, camera, GetScreenWidth(), GetScreenHeight());

    /// <summary>The ray from a camera through a point of a view <paramref name="width"/> by <paramref name="height"/> pixels.</summary>
    public static Ray GetScreenToWorldRayEx(Vector2 position, Camera3D camera, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (!Matrix4x4.Invert(camera.View * camera.ProjectionMatrix((float)width / height), out var inverse))
            return new Ray(camera.Position, Vector3.Normalize(camera.Target - camera.Position));

        // Normalized device coordinates, which run down the screen as pixels do, since the
        // projection is flipped for Vulkan.
        var x = position.X / width * 2 - 1;
        var y = position.Y / height * 2 - 1;
        var near = Vector4.Transform(new Vector4(x, y, 0, 1), inverse);
        var far = Vector4.Transform(new Vector4(x, y, 1, 1), inverse);
        var from = new Vector3(near.X, near.Y, near.Z) / near.W;
        var to = new Vector3(far.X, far.Y, far.Z) / far.W;
        return new Ray(from, Vector3.Normalize(to - from));
    }

    /// <summary>The ray from a camera through the mouse pointer, raylib's older name for <see cref="GetScreenToWorldRay"/>.</summary>
    public static Ray GetMouseRay(Vector2 mousePosition, Camera3D camera) => GetScreenToWorldRay(mousePosition, camera);
}

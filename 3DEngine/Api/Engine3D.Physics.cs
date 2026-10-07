using System.Numerics;

namespace Engine;

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
    /// <remarks>
    /// A mesh collides only with bodies that move, and a triangle only from its front, the side its
    /// winding faces. A skinned model is shaped as it was last posed and drawn, and stays so when it
    /// is posed again.
    /// </remarks>
    /// <returns>The body, or an invalid one when the model has no triangles, with the reason in the log.</returns>
    public static PhysicsBody CreatePhysicsStaticModel(Model model, Vector3 position, float scale = 1)
    {
        var world = model.Transform * Matrix4x4.CreateScale(scale);
        var points = new List<Vector3>();
        var triangles = new List<int>();
        for (int index = 0; index < model.Meshes.Length; index++)
        {
            if (!Meshes.TryGetData(model.Meshes[index].Id, out var vertices, out var indices)) continue;
            var first = points.Count;
            foreach (var vertex in Posed(model, index, vertices)) points.Add(Vector3.Transform(vertex.Position, world));
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

    /// <summary>
    /// A body shaped as the convex hull of <paramref name="model"/>, the smallest shape without
    /// hollows that holds its vertices, scaled as <c>DrawModel</c> scales it, with the model's origin
    /// at <paramref name="position"/>, which falls and is pushed with <paramref name="mass"/>, as a
    /// rock, a barrel or a crate of the model's own shape.
    /// </summary>
    /// <remarks>
    /// The body's position and rotation are where the model is drawn, as
    /// <c>DrawModelEx(model, GetPhysicsBodyPosition(body), ...)</c> draws it, and it turns about its
    /// center of mass. A hollow in the model is filled, so a cup holds nothing. A skinned model is
    /// shaped as it was last posed and drawn.
    /// </remarks>
    /// <returns>The body, or an invalid one when the model's vertices hold nothing, with the reason in the log.</returns>
    public static PhysicsBody CreatePhysicsConvexHull(Model model, Vector3 position, float mass = 1, float scale = 1)
    {
        var world = model.Transform * Matrix4x4.CreateScale(scale);
        var points = new HashSet<Vector3>();
        for (int index = 0; index < model.Meshes.Length; index++)
            if (Meshes.TryGetData(model.Meshes[index].Id, out var vertices, out _))
                foreach (var vertex in Posed(model, index, vertices)) points.Add(Vector3.Transform(vertex.Position, world));
        try
        {
            return Physics.CreateConvexHull(position, [.. points], mass);
        }
        catch (ArgumentException ex)
        {
            ApiLogger.Warn($"CreatePhysicsConvexHull: the model makes no convex hull, {ex.Message}");
            return default;
        }
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

    /// <summary>
    /// Joins two bodies so the second slides along <paramref name="axis"/> against the first and
    /// neither turns, as a drawer, a sliding door or a lift on its frame, starting as they are placed.
    /// </summary>
    /// <remarks>
    /// The first is often a kinematic body the frame is, which the program moves or leaves still,
    /// since a joint holds bodies that move. <see cref="SetPhysicsSliderLimits"/> stops it at its
    /// ends, and <see cref="SetPhysicsSliderMotor"/> drives it.
    /// </remarks>
    /// <exception cref="ArgumentException">A body is static.</exception>
    public static PhysicsJoint CreatePhysicsSliderJoint(PhysicsBody a, PhysicsBody b, Vector3 axis) => Physics.CreateSliderJoint(a, b, axis);

    /// <summary>Keeps a slider between two distances along its axis from where it was made, as a drawer that stops out and in.</summary>
    /// <exception cref="ArgumentException">The joint is not a slider, or the distances are out of order.</exception>
    public static void SetPhysicsSliderLimits(PhysicsJoint slider, float minimum, float maximum) => Physics.SetSliderLimit(slider, minimum, maximum);

    /// <summary>
    /// Drives a slider at a speed in units a second, toward its axis's tip for a positive one, with
    /// no more than a force, as a lift's winch. A speed of 0 holds it where it is, up to that force.
    /// </summary>
    /// <exception cref="ArgumentException">The joint is not a slider.</exception>
    public static void SetPhysicsSliderMotor(PhysicsJoint slider, float speed, float maximumForce) => Physics.SetSliderMotor(slider, speed, maximumForce);

    /// <summary>How far a slider's second body is along its axis from where it was made, as how high a lift has risen.</summary>
    /// <exception cref="ArgumentException">The joint is not a slider.</exception>
    public static float GetPhysicsSliderPosition(PhysicsJoint slider) => Physics.GetSliderPosition(slider);

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
    /// <remarks>A change wakes the body, or what rests on it when it is static, as a change of layer does.</remarks>
    public static void SetPhysicsBodyTrigger(PhysicsBody body, bool trigger) => Physics.SetTrigger(body, trigger);

    /// <summary>A box moved only by the program, through <see cref="SetPhysicsBodyPosition"/> or <see cref="SetPhysicsBodyVelocity"/>, which pushes what it meets.</summary>
    public static PhysicsBody CreatePhysicsKinematicBox(Vector3 position, Vector3 size) =>
        Physics.CreateKinematicBox(position, size / 2);

    /// <summary>
    /// A vehicle: a box of <paramref name="size"/> facing -Z, held up on raycast wheels as springs
    /// and driven by <see cref="SetPhysicsVehicleInput"/>, every step pushing it as
    /// <paramref name="settings"/> says, a car of about 1000 kg unless set.
    /// </summary>
    /// <remarks>Its wheels are worked out on the physics' fixed steps, so a vehicle drives the same however fast frames come.</remarks>
    public static PhysicsBody CreatePhysicsVehicle(Vector3 position, Vector3 size, float mass = 1000, Vehicle? settings = null) =>
        Physics.CreateVehicle(position, size, mass, settings);

    /// <summary>Drives a vehicle until told otherwise: throttle and steering from -1 to 1, reverse and right being negative.</summary>
    public static void SetPhysicsVehicleInput(PhysicsBody vehicle, float throttle, float steer, bool brake = false) =>
        Physics.SetVehicleInput(vehicle, throttle, steer, brake);

    /// <summary>Changes how a vehicle rides and drives, as a tuning screen or a gear does.</summary>
    public static void SetPhysicsVehicle(PhysicsBody vehicle, Vehicle settings) => Physics.SetVehicle(vehicle, settings);

    /// <summary>How a vehicle rides and drives, to change one value with <c>with</c>.</summary>
    public static Vehicle GetPhysicsVehicle(PhysicsBody vehicle) => Physics.GetVehicle(vehicle);

    /// <summary>A vehicle's wheels as its last step left them, front first, for drawing them and for a skid's sound.</summary>
    public static VehicleWheel[] GetPhysicsVehicleWheels(PhysicsBody vehicle) => Physics.GetVehicleWheels(vehicle).ToArray();

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

    /// <summary>
    /// Pushes a body at a point in the world, which turns it as well as moving it unless the point
    /// is its center of mass, as a wheel's grip on a car or a hit on a crate's corner does.
    /// </summary>
    public static void ApplyPhysicsImpulseAt(PhysicsBody body, Vector3 impulse, Vector3 point) => Physics.ApplyImpulseAt(body, impulse, point);

    /// <summary>A body's rotation as of its last step.</summary>
    public static Quaternion GetPhysicsBodyRotation(PhysicsBody body) => Physics.GetRotation(body);

    /// <summary>Turns a body at once, about its position, as righting a car that rolled over does.</summary>
    public static void SetPhysicsBodyRotation(PhysicsBody body, Quaternion rotation) => Physics.SetRotation(body, Quaternion.Normalize(rotation));

    /// <summary>How fast a body turns, about which axis, in radians a second.</summary>
    public static Vector3 GetPhysicsBodyAngularVelocity(PhysicsBody body) => Physics.GetAngularVelocity(body);

    /// <summary>Sets how fast a body turns, waking it.</summary>
    public static void SetPhysicsBodyAngularVelocity(PhysicsBody body, Vector3 velocity) => Physics.SetAngularVelocity(body, velocity);

    /// <summary>How fast a point of a body moves in the world, its velocity and its turn at that point, as a wheel's contact patch.</summary>
    public static Vector3 GetPhysicsBodyPointVelocity(PhysicsBody body, Vector3 point) => Physics.GetPointVelocity(body, point);

    /// <summary>Holds the simulation still, as a pause menu does, or lets it run again. Bodies keep their velocities across a pause.</summary>
    public static void SetPhysicsPaused(bool paused) => Res<PhysicsSettings>().Paused = paused;

    /// <summary>Whether the simulation is held still.</summary>
    public static bool IsPhysicsPaused() => Res<PhysicsSettings>().Paused;

    /// <summary>Sets the acceleration every dynamic body falls by, (0, -9.81, 0) to begin with.</summary>
    public static void SetPhysicsGravity(Vector3 gravity) => Physics.Gravity = gravity;

    // -- Queries

    /// <summary>
    /// The first body a ray meets other than <paramref name="ignore"/>, as a ray cast from inside a
    /// car's body down to the ground under a wheel needs, of those <paramref name="ignore"/>'s layer
    /// collides with.
    /// </summary>
    public static PhysicsRayCollision GetRayCollisionPhysicsEx(Ray ray, float maxDistance, PhysicsBody ignore) =>
        ray.Direction != Vector3.Zero && Physics.Raycast(ray.Position, Vector3.Normalize(ray.Direction), maxDistance, ignore, out var hit) ? hit : default;

    /// <summary>The first body a ray meets within <paramref name="maxDistance"/>, with where and at what face, its <see cref="PhysicsRayCollision.Hit"/> false for none.</summary>
    /// <remarks>A ray goes through a trigger, which stops nothing, as a body does. It answers as raylib's <c>GetRayCollision</c> functions do, with the collision rather than a flag beside it.</remarks>
    public static PhysicsRayCollision GetRayCollisionPhysics(Ray ray, float maxDistance) =>
        ray.Direction != Vector3.Zero && Physics.Raycast(ray.Position, Vector3.Normalize(ray.Direction), maxDistance, out var hit) ? hit : default;

    /// <summary>
    /// The first body a ball of <paramref name="radius"/> meets moving along a ray within
    /// <paramref name="maxDistance"/>, as a thick shot or a camera pulled in from behind a wall,
    /// its <see cref="PhysicsRayCollision.Hit"/> false for none.
    /// </summary>
    /// <remarks>
    /// The distance is how far the ball's middle moved, the point where the ball touched and the
    /// normal the way the surface faces there. A ball that starts inside a body meets it at 0,
    /// facing back along the ray. It goes through triggers as a ray does.
    /// </remarks>
    public static PhysicsRayCollision GetSphereCastPhysics(Ray ray, float radius, float maxDistance) =>
        Physics.SphereCast(ray.Position, radius, ray.Direction, maxDistance, default, out var hit) ? hit : default;

    /// <summary>The same past one body, as a ball cast from a character's own capsule.</summary>
    public static PhysicsRayCollision GetSphereCastPhysicsEx(Ray ray, float radius, float maxDistance, PhysicsBody ignore) =>
        Physics.SphereCast(ray.Position, radius, ray.Direction, maxDistance, ignore, out var hit) ? hit : default;

    /// <summary>
    /// Every body a sphere overlaps or touches, each once, as what an explosion reaches. Triggers
    /// are left out, as rays pass through them.
    /// </summary>
    public static PhysicsBody[] GetPhysicsBodiesInSphere(Vector3 center, float radius) => [.. Physics.Overlap(center, radius)];

    /// <summary>
    /// Puts a body on one of 32 layers, 0 to 31, which decides what it collides with, as
    /// <see cref="SetPhysicsLayersCollide"/> says. Every body is on layer 0 to begin with.
    /// </summary>
    /// <remarks>
    /// Bodies on layers that do not collide pass through each other and report no contact, a
    /// trigger included, so a trigger on a layer only the player's collides with reports the player
    /// alone. A character stands only on what its layer collides with, and a ray cast past a body
    /// with <see cref="GetRayCollisionPhysicsEx"/> sees what that body's layer collides with. A
    /// change wakes the body, or what rests on it when it is static, so a crate asleep on a floor
    /// falls once the floor's layer stops colliding with its own.
    /// </remarks>
    public static void SetPhysicsBodyLayer(PhysicsBody body, int layer) => Physics.SetLayer(body, layer);

    /// <summary>
    /// Sweeps a body over each step to find what it would meet within it, as a ball struck hard or a
    /// thrown crate needs, so it does not cross a thin wall within one step, or stops sweeping it.
    /// </summary>
    /// <remarks>
    /// Without it a ball of 20 units a second crossed a wall a fifth of a unit thick. With it a body
    /// stops at a wall of any thickness at 50 units a second, at a fifth of a unit's at 100 and at
    /// half a unit's at 300, since a contact stops a body over a step rather than at once. A shot
    /// faster than that is a ray or a ball cast each frame, as <see cref="GetSphereCastPhysics"/>
    /// makes, rather than a body. A swept body costs a sweep test for each body it nears.
    /// </remarks>
    public static void SetPhysicsBodyContinuous(PhysicsBody body, bool continuous) => Physics.SetContinuous(body, continuous);

    /// <summary>The layer a body is on.</summary>
    public static int GetPhysicsBodyLayer(PhysicsBody body) => Physics.GetLayer(body);

    /// <summary>
    /// Whether bodies on layer <paramref name="a"/> collide with bodies on layer <paramref name="b"/>,
    /// both ways, as the player's shots pass through the player and the enemies through each other.
    /// Every layer collides with every other to begin with.
    /// </summary>
    /// <remarks>A change wakes the bodies asleep on either layer, whose resting contacts are then tested again.</remarks>
    public static void SetPhysicsLayersCollide(int a, int b, bool collide) => Physics.SetLayersCollide(a, b, collide);

    /// <summary>
    /// The push the last step gave two touching bodies along the normals of their contacts, in mass
    /// times units a second, or 0 for a pair not touching. It says how hard they press, as a
    /// crate's weight on a pressure plate, where a contact's <c>Speed</c> says how fast they met.
    /// Divided by the step, a sixtieth of a second, it is the force between them.
    /// </summary>
    /// <remarks>
    /// The friction along the surface and the twist about the normal are left out, so a crate
    /// dragged across a plate presses it by its weight as one at rest does. A pair asked about goes
    /// on being answered with what it was when it fell asleep, as a crate long at rest on a
    /// pressure plate does, since nothing between them changes while it sleeps. A pair asked about
    /// first while asleep is woken and answered from its next step.
    /// </remarks>
    public static float GetPhysicsContactImpulse(PhysicsBody a, PhysicsBody b) => Physics.GetContactImpulse(a, b);

    /// <summary>The pairs of bodies that started touching in this frame's steps.</summary>
    public static IReadOnlyList<ContactStarted> GetPhysicsContacts() => World.ReadEvents<ContactStarted>();

    /// <summary>The pairs of bodies that stopped touching in this frame's steps, as a body leaving a trigger.</summary>
    /// <remarks>
    /// A pair that touched and parted within the frame is in both this and
    /// <see cref="GetPhysicsContacts"/>, and a body destroyed while touching ends its contacts with
    /// the next step, so a door's sensor that counts who is in it by the two lists stays right.
    /// </remarks>
    public static IReadOnlyList<ContactEnded> GetPhysicsContactsEnded() => World.ReadEvents<ContactEnded>();

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
}

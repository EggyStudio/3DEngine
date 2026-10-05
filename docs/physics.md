# Physics

Physics makes bodies fall, collide, bounce and push each other, through BepuPhysics, so a program
says what shape and how heavy a thing is and the engine moves it. The program draws each body
where the simulation says it is, and asks the simulation what a ray hits and what touched.

## Bodies that fall

`CreatePhysicsBox` makes a box of a size and a mass that falls, collides and is pushed, and
`CreatePhysicsStaticBox` one that never moves, for floors and walls. Each returns a `PhysicsBody`
the program keeps. From the `physics_boxes` example:

```csharp
var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
var floor = LoadModelFromMesh(GenMeshCube(16, 1, 16));

// A floor that never moves, and boxes that fall onto it.
CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(16, 1, 16));
var boxes = new List<(PhysicsBody Body, Color Color)>();
void Drop(int count)
{
    for (int i = 0; i < count; i++)
    {
        var at = new Vector3(Random.Shared.NextSingle() * 4 - 2, 3 + boxes.Count * 0.6f % 9, Random.Shared.NextSingle() * 4 - 2);
        boxes.Add((CreatePhysicsBox(at, Vector3.One), new Color(230, (byte)(100 + Random.Shared.Next(110)), 60)));
    }
}
Drop(20);
```

The simulation steps at a fixed rate inside `BeginDrawing`, as many steps as the time since the
last frame holds, so it runs alike at any frame rate. A body is drawn where it is by setting a
model's `Transform` to `GetPhysicsBodyTransform(body)`, which holds its position and its turn,
blended between the last two steps so motion is smooth:

```csharp
foreach (var (body, color) in boxes)
{
    cube.Transform = GetPhysicsBodyTransform(body);
    DrawModel(cube, Vector3.Zero, 1, IsPhysicsBodyHit(body) ? Color.White : color);
}
// ...
foreach (var (body, _) in boxes) DestroyPhysicsBody(body);
```

The other shapes are `CreatePhysicsSphere` and `CreatePhysicsCapsule`, and
`CreatePhysicsStaticModel` makes level geometry from a model's own triangles, so a loaded level is
solid where it is drawn. A box the program moves itself, as a lift or a moving platform, is
`CreatePhysicsKinematicBox`, which pushes what it meets and is not pushed back. It is moved by its
velocity, so what stands on it rides along, where setting its position each frame jumps it to
the place and leaves a rider behind or pushed through it. `games/Summit`'s lift heads for where it
is due:

```csharp
var liftY = 4.5f - 1.5f * MathF.Cos((float)GetTime() * 0.6f);
var liftAt = GetPhysicsBodyPosition(lift);
SetPhysicsBodyVelocity(lift, new Vector3(0, (liftY - liftAt.Y) * 8, 0));
```

A body of a model's own shape that falls and tumbles, as a rock or a barrel, is
`CreatePhysicsConvexHull`, shaped as the smallest shape without hollows that holds the model's
vertices. Its position is the model's origin, where the model is drawn, though it turns about its
center of mass. The `physics_boxes` example drops cones among its boxes:

```csharp
cones.Add(CreatePhysicsConvexHull(cone, new Vector3(Random.Shared.NextSingle() * 6 - 3, 4 + i, Random.Shared.NextSingle() * 6 - 3), mass: 0.5f));
// ...
cone.Transform = GetPhysicsBodyTransform(body);
DrawModel(cone, Vector3.Zero, 1, new Color(80, 130, 220));
```

In the ECS, `Collider.ConvexHull` beside a `RigidBody` makes the same from the entity's meshes and
those below it.

## Pushing and moving

`ApplyPhysicsImpulse` pushes a body at its center, as a hit or an explosion does, and
`SetPhysicsBodyVelocity` sets its speed and direction outright. `SetPhysicsBodyPosition` moves it
at once, as a respawn does. `SetPhysicsGravity` sets what every body falls by, 9.81 down unless
set, and `SetPhysicsPaused` holds the whole simulation still, as a pause menu needs.

`SetPhysicsBodyMaterial` gives a body its friction and bounce, so ice is a friction near 0, rubber
a bounce near 1, and a dead ball a bounce of 0:

```csharp
var ball = CreatePhysicsSphere(new Vector3(0, 5, 0), 0.5f);
SetPhysicsBodyMaterial(ball, friction: 0.4f, bounce: 0.8f);
```

`ApplyPhysicsImpulseAt` pushes a body at a point of it, which turns it as well as moving it, so a
push under one corner of a car lifts that corner. `GetPhysicsBodyPointVelocity` says how fast a
point of a body moves, its turning included, which is how fast a spring at that corner is pressed
or a tyre slides. `GetPhysicsBodyRotation` and `SetPhysicsBodyRotation` read and set how it is
turned, and `GetPhysicsBodyAngularVelocity` and `SetPhysicsBodyAngularVelocity` how fast it turns.

## Rays and contacts

`GetRayCollisionPhysics` finds the first body along a ray and gives back the collision, as raylib's
`GetRayCollision` functions do, with `Hit` saying whether it met one, the body, the point, the
surface's normal and the distance. With the ray under the mouse it picks a body with a click, as `physics_boxes`
does to push the box clicked:

```csharp
// A click pushes the box under the pointer away from the camera and up.
if (IsMouseButtonPressed(MouseButton.Left) &&
    GetRayCollisionPhysics(GetScreenToWorldRay(GetMousePosition(), camera), 100) is { Hit: true } hit &&
    hit.Body.Kind == BodyKind.Dynamic)
    ApplyPhysicsImpulse(hit.Body, Vector3.Normalize(hit.Point - camera.Position) * 6 + Vector3.UnitY * 3);
```

`GetRayCollisionPhysicsEx` looks past one body, as a ray cast from inside a car's body to the
ground under a wheel does. A ray goes through triggers, which stop nothing.

`IsPhysicsBodyHit` says whether a body started touching anything this frame, which the example
flashes a box white by. `GetPhysicsContacts` lists every pair that started touching this frame,
with where they met, the normal between them, and `Speed`, how fast they closed, which says how
hard they hit:

```csharp
foreach (var contact in GetPhysicsContacts())
    if (contact.Speed > 2) PlaySound(thud);
```

## Triggers

`CreatePhysicsTrigger` makes a box that stops nothing and reports what enters it, as a goal, a
pickup or a door's sensor. A body entering it is a contact that starts, in `GetPhysicsContacts`
like any other:

```csharp
var goal = CreatePhysicsTrigger(new Vector3(0, 1, -20), new Vector3(4, 2, 1));
// ...
foreach (var contact in GetPhysicsContacts())
    if (contact.BodyA == goal || contact.BodyB == goal) won = true;
```

`GetPhysicsContactsEnded` lists the pairs that stopped touching this frame, so a door's sensor
knows who is still in it by counting who entered and who left:

```csharp
foreach (var contact in GetPhysicsContacts()) if (contact.BodyA == sensor || contact.BodyB == sensor) inside++;
foreach (var contact in GetPhysicsContactsEnded()) if (contact.BodyA == sensor || contact.BodyB == sensor) inside--;
SetPhysicsHingeMotor(hinge, inside > 0 ? 120 : -120, 400);
```

A contact starts when a body enters, so a body already inside a trigger when it begins to be
counted, as a car waiting at a start line inside the first gate, is never reported until it
leaves and comes back.

`SetPhysicsBodyTrigger` turns a body of any shape into a trigger, or back into a solid one.

## Joints

A joint holds two bodies together, and two bodies joined do not collide with each other. A ball
joint lets them turn freely about a point, a hinge about an axis, as a door, a weld holds them
rigidly, and a distance joint keeps them within a range, as a rope or a rod. A joint holds bodies that move, so a body held to the world is joined to a kinematic one, and a
static body given to a joint throws:

```csharp
var post = CreatePhysicsKinematicBox(new Vector3(0, 1, 0), new Vector3(0.2f, 2, 0.2f));
var door = CreatePhysicsBox(new Vector3(0.6f, 1, 0), new Vector3(1, 2, 0.1f), mass: 10);
var hinge = CreatePhysicsHingeJoint(post, door, new Vector3(0.1f, 1, 0), Vector3.UnitY);
SetPhysicsHingeLimits(hinge, -90, 90);
```

`SetPhysicsHingeMotor` drives a hinge at a speed, as a wheel or a fan, and `games/Manor` swings
its doors by it, open while the player is in a door's sensor and back to shut after.
`SetPhysicsBallJointLimits` keeps a ball joint within a cone it swings and twists in, as a
shoulder, `SetPhysicsDistanceJointRange` lengthens or shortens a rope after it is made, as a winch,
`DestroyPhysicsJoint` breaks a joint, as a rope that is cut, and `IsPhysicsJointValid` says whether
it is still there. `games/Summit` hangs a bridge from a beam on two ropes,
distance joints a little slack, so it sways as it is crossed:

```csharp
var beam = CreatePhysicsKinematicBox(new Vector3(5, 9, -27), new Vector3(6, 0.4f, 0.4f));
var bridge = CreatePhysicsBox(new Vector3(5, 3, -27), new Vector3(6, 0.3f, 1.4f), mass: 120);
CreatePhysicsDistanceJoint(beam, bridge, new Vector3(2.5f, 9, -27), new Vector3(2.5f, 3, -27), 5.9f, 6);
CreatePhysicsDistanceJoint(beam, bridge, new Vector3(7.5f, 9, -27), new Vector3(7.5f, 3, -27), 5.9f, 6);
```

## A vehicle

`CreatePhysicsVehicle` makes a car: a box held up by four wheels, each a ray cast down from the
body that pushes it up as a spring and a damper, grips the ground sideways up to what presses it,
and drives or brakes along the way it points. `SetPhysicsVehicleInput` drives it from the keys or
a pad, and the wheels are worked out on the physics' fixed steps, so it drives the same however
fast frames come. `games/Rally` races one:

```csharp
var car = CreatePhysicsVehicle(start, new Vector3(1.8f, 0.6f, 3.8f));
// ...each frame
SetPhysicsVehicleInput(car, throttle, steer, brake);
foreach (var wheel in GetPhysicsVehicleWheels(car))
    DrawModelEx(tyre, wheel.Center, Vector3.UnitY, wheel.Steer * 180 / MathF.PI, Vector3.One, Color.DarkGray);
```

A `Vehicle` holds its wheels' mounts, their radius and springs, the engine's and the brakes'
force, the grip, the steering's lock, which wheels drive, and the air's drag and downforce, the
defaults a car of about 1000 kg. Steering turns less the faster it goes, rolling and pitching
settle on the ground, and in the air it turns itself level a little, so a car thrown over a crest
lands on its wheels. A wheel's `Slip` is how fast its tyre slides sideways, which a skid's sound
and dust follow, and `Spin` how far it has turned about its axle, for drawing it.

A vehicle of a program's own, as a boat or a hovercraft, is built from `ApplyPhysicsImpulseAt`,
`GetPhysicsBodyPointVelocity` and `GetRayCollisionPhysicsEx`, above.

## A character

A character is moved by the player, not pushed about, and needs to stop at walls, slide along
them, climb steps and stand on slopes. `CreatePhysicsCharacter` makes an upright capsule with its
feet at a point, which `MovePhysicsCharacter` walks at a velocity along the ground until it is
given another, leaving its fall to gravity:

```csharp
var player = CreatePhysicsCharacter(new Vector3(0, 0, 0), radius: 0.4f, height: 1.8f);
// ...
var walk = Vector3.Zero;
if (IsKeyDown(Key.W)) walk.Z -= 1;
if (IsKeyDown(Key.S)) walk.Z += 1;
if (IsKeyDown(Key.A)) walk.X -= 1;
if (IsKeyDown(Key.D)) walk.X += 1;
MovePhysicsCharacter(player, walk == Vector3.Zero ? walk : Vector3.Normalize(walk) * 4);
if (IsKeyPressed(Key.Space) && IsPhysicsCharacterGrounded(player)) JumpPhysicsCharacter(player, 5);
```

`SetPhysicsCharacterHeight` crouches and stands, and answers false when a ceiling is in the way.
`SetPhysicsCharacterMaxSlope` and `SetPhysicsCharacterStepHeight` set the steepest ground it walks
up and the highest step it climbs.

## Physics in the ECS

A body belongs to an entity when it is made through `ctx.Physics`, the physics world of a
[behavior](behaviors-and-the-ecs.md)'s context, and the entity's `Transform` follows it, so a mesh
entity beside it is drawn where it is with no code. The `ecs_physics` example drops boxes this way. In the ECS a box is given by half its size, the
distance from its center to each face:

```csharp
var box = ctx.Ecs.Spawn();
ctx.Ecs.Add(box, ctx.Physics.CreateBox(at, new Vector3(0.5f), entityId: box));
ctx.Ecs.Add(box, new Mesh(Cube));
ctx.Ecs.Add(box, new Material(new Color(230, (byte)(102 + Random.Shared.Next(102)), 51)));
ctx.Ecs.Add(box, new Transform(at));
```

An entity can also say what it is shaped as with a `Collider` component, and a `RigidBody` beside
it, from which the engine makes its body. A scene file holds those two, so a level saved to a file
says what in it is solid. Contacts reach behaviors as `ContactStarted` and `ContactEnded` events,
each naming the two entities, and a character is walked through a `CharacterController` component
beside its body.

## The same every run

The step gives the same result every run for the same input, to the bit, which a replay or a test
that checks where things land relies on. It runs on four worker threads once 500 bodies are awake,
the same four on every machine, in Bepu's deterministic mode, and contacts are worked through and
reported in an order of the pairs' own rather than the order the workers met them.
`PhysicsSettings.WorkerThreads` and `ThreadedAbove` change both, a program that changes them
keeping its runs the same as each other but not as a program that does not.

## See also

- Examples: [`physics_boxes`](../3DEngine.Examples/Physics/PhysicsBoxes.cs),
  [`ecs_physics`](../3DEngine.Examples/Ecs/EcsPhysics.cs), and the game
  [`games/Summit`](../games/Summit/Program.cs), a character on the controller over joints, a lift
  and triggers
- The cheatsheet's [Physics](../CHEATSHEET.md#physics)
- Previous: [Input](input.md)
- Next: [Behaviors and the ECS](behaviors-and-the-ecs.md)

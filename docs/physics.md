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

## Rays and contacts

`GetRayCollisionPhysics` finds the first body along a ray, with the point, the surface's normal
and the distance. With the ray under the mouse it picks a body with a click, as `physics_boxes`
does to push the box clicked:

```csharp
// A click pushes the box under the pointer away from the camera and up.
if (IsMouseButtonPressed(MouseButton.Left) &&
    GetRayCollisionPhysics(GetScreenToWorldRay(GetMousePosition(), camera), 100, out var hit) &&
    hit.Body.Kind == BodyKind.Dynamic)
    ApplyPhysicsImpulse(hit.Body, Vector3.Normalize(hit.Point - camera.Position) * 6 + Vector3.UnitY * 3);
```

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

`SetPhysicsBodyTrigger` turns a body of any shape into a trigger, or back into a solid one.

## Joints

A joint holds two bodies together, and two bodies joined do not collide with each other. A ball
joint lets them turn freely about a point, a hinge about an axis, as a door, a weld holds them
rigidly, and a distance joint keeps them within a range, as a rope or a rod:

A joint holds bodies that move, so a body held to the world is joined to a kinematic one, and a
static body given to a joint throws:

```csharp
var post = CreatePhysicsKinematicBox(new Vector3(0, 1, 0), new Vector3(0.2f, 2, 0.2f));
var door = CreatePhysicsBox(new Vector3(0.6f, 1, 0), new Vector3(1, 2, 0.1f), mass: 10);
var hinge = CreatePhysicsHingeJoint(post, door, new Vector3(0.1f, 1, 0), Vector3.UnitY);
SetPhysicsHingeLimits(hinge, -90, 90);
```

`SetPhysicsHingeMotor` drives a hinge at a speed, as a wheel or a fan, and `DestroyPhysicsJoint`
breaks a joint, as a rope that is cut.

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

## See also

- Examples: [`physics_boxes`](../3DEngine.Examples/Physics/PhysicsBoxes.cs),
  [`ecs_physics`](../3DEngine.Examples/Ecs/EcsPhysics.cs)
- The cheatsheet's [Physics](../CHEATSHEET.md#physics)
- Previous: [Input](input.md)
- Next: [Behaviors and the ECS](behaviors-and-the-ecs.md)

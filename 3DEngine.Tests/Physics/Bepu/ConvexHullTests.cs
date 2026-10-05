using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Physics;

/// <summary>Bodies shaped as the convex hull of a mesh's points, whose pose is where the mesh is drawn.</summary>
[Trait("Category", "Integration")]
public class ConvexHullTests
{
    private const float Step = 1f / 60f;

    // A unit cube from its origin up and out, so its center of mass is half a unit from the origin
    // on every axis, as a model made with its origin at a corner is.
    private static readonly Vector3[] Corner =
    [
        new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0),
        new(0, 0, 1), new(1, 0, 1), new(0, 1, 1), new(1, 1, 1),
    ];

    private static void Run(PhysicsWorld world, float seconds)
    {
        for (int i = 0; i < seconds / Step; i++) world.StepOnce(Step);
    }

    [Fact]
    public void A_Hull_Rests_On_The_Ground_With_Its_Origin_Where_Its_Mesh_Is_Drawn()
    {
        using var world = new PhysicsWorld();
        world.CreateGroundPlane();
        var hull = world.CreateConvexHull(new Vector3(2, 3, 0), Corner, mass: 4);

        world.GetPosition(hull).Should().Be(new Vector3(2, 3, 0), "the body is read where its points' origin is");
        Run(world, 3);

        var at = world.GetPosition(hull);
        at.Y.Should().BeApproximately(0, 0.02f, "the cube's lowest face, at its origin, rests on the ground");
        at.X.Should().BeApproximately(2, 0.02f, "and it fell straight down");
    }

    [Fact]
    public void Turning_A_Hull_Keeps_Its_Origin_Where_It_Was()
    {
        using var world = new PhysicsWorld();
        var hull = world.CreateConvexHull(new Vector3(0, 5, 0), Corner, kind: BodyKind.Kinematic);
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);

        world.SetRotation(hull, turn);

        world.GetPosition(hull).Should().Be(new Vector3(0, 5, 0));
        world.GetRotation(hull).Should().Be(turn);
        world.SetPosition(hull, new Vector3(1, 1, 1));
        Vector3.Distance(world.GetPosition(hull), new Vector3(1, 1, 1)).Should().BeLessThan(1e-5f);
    }

    [Fact]
    public void Points_In_A_Plane_Make_No_Hull()
    {
        using var world = new PhysicsWorld();
        var act = () => world.CreateConvexHull(Vector3.Zero, [new(0, 0, 0), new(1, 0, 0), new(0, 0, 1), new(1, 0, 1)]);

        act.Should().Throw<ArgumentException>().WithMessage("*plane*");
    }

    [Fact]
    public void A_Collider_Of_The_Entitys_Mesh_Makes_A_Hull_That_Falls_And_Writes_Its_Transform()
    {
        var world = new World();
        world.InsertResource(new EcsWorld());
        var physics = new PhysicsWorld();
        world.InsertResource(physics);
        var ecs = world.Resource<EcsWorld>();
        physics.CreateGroundPlane();

        // The cube's triangles from its corners, as a Mesh component holds them.
        int[] faces = [0, 1, 3, 0, 3, 2, 4, 6, 7, 4, 7, 5, 0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3, 0, 4, 5, 0, 5, 1, 2, 3, 7, 2, 7, 6];
        var rock = ecs.Spawn();
        ecs.Add(rock, new Transform(new Vector3(-1, 2, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3f), Vector3.One));
        ecs.Add(rock, new Mesh([.. faces.Select(i => Corner[i])]));
        ecs.Add(rock, Collider.ConvexHull);
        ecs.Add(rock, RigidBody.Dynamic(3));

        PhysicsBodies.Run(world);
        ecs.Has<PhysicsBody>(rock).Should().BeTrue("the hull is made from the entity's mesh");
        Run(physics, 3);
        physics.SyncTransforms(ecs);

        var transform = ecs.GetReadOnly<Transform>(rock);
        transform.Position.Y.Should().BeApproximately(0, 0.02f, "the mesh drawn at the transform sits on the ground");
        Quaternion.Dot(transform.Rotation, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3f)).Should().BeGreaterThan(0.999f, "it landed flat, turned as it was placed");
        physics.Dispose();
    }
}

using FluentAssertions;

namespace Engine.Tests.Ecs;

[Trait("Category", "Unit")]
public class HierarchyTests
{
    [Fact]
    public void An_Entity_Is_Found_By_Its_Name()
    {
        var ecs = new EcsWorld();
        var a = ecs.Spawn();
        ecs.SetName(a, "Player");
        ecs.SetName(a, "Hero");

        ecs.NameOf(a).Should().Be("Hero");
        ecs.FindByName("Hero").Should().Be(a);
        ecs.FindByName("Player").Should().Be(0);
    }

    [Fact]
    public void Children_Are_Listed_And_Despawned_With_Their_Parent()
    {
        var ecs = new EcsWorld();
        var root = ecs.Spawn();
        var child = ecs.Spawn();
        var grandchild = ecs.Spawn();
        ecs.SetParent(child, root);
        ecs.SetParent(grandchild, child);

        ecs.ChildrenOf(root).Should().Equal(child);
        ecs.ParentOf(grandchild).Should().Be(child);

        ecs.DespawnRecursive(root);

        ecs.EntityCount.Should().Be(0);
    }

    [Fact]
    public void A_Parent_Despawned_And_Replaced_Is_Not_The_New_Entity()
    {
        var ecs = new EcsWorld();
        var parent = ecs.Spawn();
        var child = ecs.Spawn();
        ecs.SetParent(child, parent);

        ecs.Despawn(parent);
        var reuse = ecs.Spawn();

        reuse.Should().Be(parent);
        ecs.ParentOf(child).Should().Be(0);
    }

    [Fact]
    public void A_Cycle_Is_Refused()
    {
        var ecs = new EcsWorld();
        var a = ecs.Spawn();
        var b = ecs.Spawn();
        ecs.SetParent(b, a);

        var act = () => ecs.SetParent(a, b);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Moving_A_Parent_Moves_Its_Children()
    {
        var world = new World();
        var ecs = new EcsWorld();
        world.InsertResource(ecs);
        var parent = ecs.Spawn();
        ecs.Add(parent, new Transform(new System.Numerics.Vector3(10, 0, 0)));
        var child = ecs.Spawn();
        ecs.Add(child, new Transform(new System.Numerics.Vector3(0, 2, 0)));
        ecs.SetParent(child, parent);

        TransformPropagation.Run(world);
        TransformPropagation.WorldMatrix(ecs, child).Translation.Should().Be(new System.Numerics.Vector3(10, 2, 0));

        ecs.GetRef<Transform>(parent).Position = new System.Numerics.Vector3(-5, 0, 0);
        TransformPropagation.Run(world);
        TransformPropagation.WorldMatrix(ecs, child).Translation.Should().Be(new System.Numerics.Vector3(-5, 2, 0));
    }

    [Fact]
    public void A_Transform_Becomes_The_Matrix_Of_Its_Scale_Rotation_And_Position_In_That_Order()
    {
        var random = new Random(3);
        for (int i = 0; i < 50; i++)
        {
            var t = new Transform(new System.Numerics.Vector3(random.NextSingle() * 10 - 5, random.NextSingle() * 10, -random.NextSingle()))
            {
                Rotation = System.Numerics.Quaternion.Normalize(new System.Numerics.Quaternion(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f, random.NextSingle() - 0.5f, random.NextSingle())),
                Scale = new System.Numerics.Vector3(0.5f + random.NextSingle() * 2, 0.5f + random.NextSingle(), 2 - random.NextSingle()),
            };
            var product = System.Numerics.Matrix4x4.CreateScale(t.Scale)
                * System.Numerics.Matrix4x4.CreateFromQuaternion(t.Rotation)
                * System.Numerics.Matrix4x4.CreateTranslation(t.Position);
            var made = TransformPropagation.ToMatrix(t);
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    made[r, c].Should().BeApproximately(product[r, c], 1e-5f);
        }
    }
}

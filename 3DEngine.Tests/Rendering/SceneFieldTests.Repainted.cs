using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

public sealed partial class SceneFieldTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void A_Still_Mesh_Painted_Again_Where_It_Stands_Goes_Into_The_Field_At_Once_And_Is_Stamped_Nowhere()
    {
        // A cube still in both cascades, then drawn of the same vertices through the same matrix
        // in another color, as a mesh is once its texture has loaded or a panel's light is put
        // out. It has not moved, so it is still at once, both cascades are built again with its
        // new paint within the two frames the budget of one mesh a frame takes, and nothing is
        // stamped as a box. Taken as moved, it would wait the frames a moving figure waits to
        // settle, stamped as its box all that while, a building's box filling every room in it.
        var plan = new SceneFieldPlan(2, 0.25f, 1, triangleBudget: 12, triangles: _ => 12);
        var eye = new Vector3(0.1f, 0.1f, 0.1f);
        for (int frame = 0; frame <= SceneFieldPlan.SettleFrames + 2; frame++) plan.Update(eye, [At(Vector3.Zero)]);
        plan.Builds.Should().BeEmpty("both cascades are built with the cube");

        var painted = At(Vector3.Zero).Item1 with { Color = new Vector3(0.8f, 0.4f, 0.2f) };
        var built = new HashSet<int>();
        for (int frame = 1; frame <= 2; frame++)
        {
            plan.Update(eye, [(painted, false)]);
            plan.Shapes.Should().BeEmpty($"the cube painted again stands where it stood, {frame} frames on");
            foreach (var build in plan.Builds.Where(b => b.Instances.Contains(painted))) built.Add(build.Cascade);
        }
        built.Should().BeEquivalentTo([0, 1], "both cascades are built again with the cube's new paint within two frames");
        plan.StillCount.Should().Be(1, "the cube painted again is the one still mesh");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Mesh_Painted_Again_As_It_Settles_Settles_When_It_Would_Have_And_Is_Stamped_Nowhere()
    {
        // A cube drawn three frames and then drawn again in another color, as a mesh whose texture
        // loads while it settles. It goes on settling from the frames it had, still when the cube
        // would have been, and is stamped nowhere meanwhile.
        var plan = new SceneFieldPlan(2, 0.25f, 1, triangleBudget: 12, triangles: _ => 12);
        var eye = new Vector3(0.1f, 0.1f, 0.1f);
        for (int frame = 0; frame < 3; frame++) plan.Update(eye, [At(Vector3.Zero)]);
        var painted = (At(Vector3.Zero).Item1 with { Color = new Vector3(0.8f, 0.4f, 0.2f) }, false);
        for (int frame = 3; frame < SceneFieldPlan.SettleFrames - 1; frame++)
        {
            plan.Update(eye, [painted]);
            plan.Shapes.Should().BeEmpty($"the cube painted again stands where it stood at frame {frame}");
            plan.StillCount.Should().Be(0, "it has not yet been drawn the same for the frames a mesh settles in");
        }
        plan.Update(eye, [painted]);
        plan.StillCount.Should().Be(1, "it is still when the cube would have been");
        plan.Shapes.Should().BeEmpty();
    }
}

using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

public sealed partial class SceneFieldTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void A_Mesh_That_Replaced_One_In_Place_Is_Not_Stamped_While_A_Cascade_Not_Yet_Built_Holds_The_One_It_Replaced()
    {
        // Two cascades and a budget of one, as a voxel game's section is replaced when a block
        // breaks. Once the replacement settles, the first cascade is built with it and the second
        // waits a frame, holding the cube still. Stamped as its boxes that frame, a section's box
        // filled the air above its ground in the cascades not yet built, and the room's indirect
        // light went for the frame.
        var plan = new SceneFieldPlan(2, 0.25f, 1);
        var eye = new Vector3(0.1f, 0.1f, 0.1f);
        var replacement = GenMeshCubeVertices(2.5f);
        for (int frame = 0; frame <= SceneFieldPlan.SettleFrames + 2; frame++) plan.Update(eye, [At(Vector3.Zero)]);
        plan.StillCount.Should().Be(1);
        plan.Builds.Should().BeEmpty("both cascades are built with the cube");

        for (int frame = 1; frame < SceneFieldPlan.SettleFrames; frame++) plan.Update(eye, [At(Vector3.Zero, replacement)]);
        plan.Update(eye, [At(Vector3.Zero, replacement)]);
        plan.Builds.Should().ContainSingle().Which.Cascade.Should().Be(0, "the budget builds the first cascade with the replacement");
        plan.Shapes.Should().BeEmpty("the second cascade, not yet built, holds the cube the replacement replaced");
        plan.Update(eye, [At(Vector3.Zero, replacement)]);
        plan.Builds.Should().ContainSingle().Which.Cascade.Should().Be(1);
        plan.Shapes.Should().BeEmpty("every cascade holds the replacement");
    }
}

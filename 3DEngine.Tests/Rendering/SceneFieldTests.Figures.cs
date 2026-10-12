using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

public sealed partial class SceneFieldTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void A_Figure_Walking_Among_Still_Meshes_Is_Read_By_Place_And_Builds_No_Cascade_Again_Where_It_Stops()
    {
        // Forty still meshes and a figure of vertices of its own that walks for 30 frames, stands
        // for 100 and walks on, drawn in the same order every frame. The plan reads the frame by
        // place as the figure walks, where it read every mesh into a dictionary each frame, and the
        // figure, a copy of whose vertices moved, is stamped as it stands rather than settled into
        // the cascades, which would be built again when it settled and again when it walked on.
        var plan = new SceneFieldPlan(2, 0.25f, 1);
        var figure = GenMeshCubeVertices(0.5f);
        var builds = new List<int>();
        for (int frame = 0; frame < 200; frame++)
        {
            var walked = frame < 30 ? frame : frame < 130 ? 30 : frame - 100;
            List<(SceneFieldPlan.Instance, bool)> drawn = [.. Enumerable.Range(0, 40).Select(i => At(new Vector3(i % 8 - 4, -2, i / 8 - 2)))];
            drawn.Add(At(new Vector3(walked * 0.05f, 0, 0), figure));
            plan.Update(Vector3.Zero, drawn);
            if (frame > 0) plan.ReadByPlace.Should().BeTrue($"frame {frame} draws the meshes of the one before in its order, the figure moved in its place");
            if (frame >= 40) builds.Add(plan.Builds.Count);
            if (frame is >= 40 and < 130) plan.Shapes.Should().HaveCount(1, $"the figure is stamped as it stands, frame {frame}");
        }
        builds.Should().OnlyContain(count => count == 0, "no cascade is built again as the figure stops and walks on");
        plan.StillCount.Should().Be(40, "the still meshes settled and the figure did not");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Copy_That_Never_Moved_Settles_After_The_Short_Count_Beside_One_That_Walks()
    {
        // Two copies of a crate, one walking and one standing, drawn in the same order and turned
        // a place each frame, which reads them whole. The one standing settles after the frames a
        // mesh that never moved takes, as a red wall that moves beside a floor and a block of its
        // own mesh leaves them still, and the one walking is stamped on.
        var crate = GenMeshCubeVertices(0.5f);
        foreach (var turned in new[] { false, true })
        {
            var plan = new SceneFieldPlan(1, 0.25f, 1);
            for (int frame = 0; frame <= SceneFieldPlan.SettleFrames + 4; frame++)
            {
                (SceneFieldPlan.Instance, bool)[] drawn = [At(new Vector3(frame * 0.05f, 0, 0), crate), At(new Vector3(0, 0, 3), crate)];
                plan.Update(Vector3.Zero, turned && frame % 2 == 1 ? [drawn[1], drawn[0]] : drawn);
                plan.StillCount.Should().Be(frame >= SceneFieldPlan.SettleFrames - 1 ? 1 : 0, $"frame {frame}, turned {turned}");
            }
            plan.Shapes.Should().HaveCount(1, "the crate walking is stamped");
        }
    }
}

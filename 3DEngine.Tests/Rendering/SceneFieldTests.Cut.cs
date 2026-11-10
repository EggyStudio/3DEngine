using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

// The builds' budgets lifted after a cut held while what the cut brought in still arrives, as a game
// meshes its sections over the frames after a teleport, and let go once frames bring none.
public sealed partial class SceneFieldTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void After_A_Cut_The_Lift_Holds_While_The_Meshes_The_New_View_Brings_Arrive_And_Ends_Once_Frames_Bring_None()
    {
        // A budget of four sections a frame, the eye cut far off into a place that holds nothing
        // until the program meshes it, 50 sections a frame from the fifth frame after the cut to the
        // fourteenth. Under the base budget the 500 take over a hundred frames to build in, and the
        // cascades the eye went past are built in the frame of the cut, empty.
        var plan = new SceneFieldPlan(4, 0.25f, 2, triangleBudget: 4 * SectionTriangles, triangles: _ => SectionTriangles);
        var eye = new Vector3(0, 2, 0);
        for (int frame = 0; frame < 60; frame++) plan.Update(eye, []);
        plan.Built.Should().BeTrue("the empty field is built");

        var there = new Vector3(4000, 0, 0);
        eye = there + new Vector3(0, 2, 0);
        var drawn = new List<(SceneFieldPlan.Instance, bool)>();
        var (last, held) = (14, 0);
        int after;
        var trace = new List<string>();
        for (after = 1; after < 400; after++)
        {
            if (after is >= 5 and <= 14)
                for (int i = (after - 5) * 50; i < (after - 4) * 50; i++)
                    drawn.Add((new SceneFieldPlan.Instance(1, Section, Matrix4x4.CreateTranslation(there + SectionAt(i)), false), false));
            plan.Update(eye, drawn);
            if (plan.Spent > plan.BaseTriangleBudget) held++;
            if (after < 60 || after % 50 == 0) trace.Add($"{after}:{plan.Spent / SectionTriangles}/{plan.Waiting}/{plan.Arriving}/{(plan.Whole ? 'W' : '-')}{(plan.Built ? 'B' : '-')}[{string.Join(",", plan.Unsettled.Cascades)}|{plan.Unsettled.Waiting}|{plan.Unsettled.Kept}]");
            if (after > last && plan.Whole && plan.Built) break;
        }
        held.Should().BeGreaterThan(1, "the frames after the cut built what arrived over the base budget");
        (after - last).Should().BeLessThanOrEqualTo(SceneFieldPlan.SettleFrames + 10,
            $"the 500 sections that came after the cut were built in by frame {after}, {after - last} frames after the last came, the lift held while they arrived, " + string.Join(" ", trace));

        // Once frames bring nothing for CutQuietFrames, the lift ends and a section that comes is
        // built under the base budget.
        for (int frame = 0; frame < SceneFieldPlan.CutQuietFrames + 2; frame++) plan.Update(eye, drawn);
        for (int i = 500; i < 540; i++)
            drawn.Add((new SceneFieldPlan.Instance(1, Section, Matrix4x4.CreateTranslation(there + SectionAt(i % Sections) + new Vector3(0, 8, 0)), false), false));
        for (int frame = 0; frame < 200 && !(plan.Whole && plan.Built); frame++)
        {
            plan.Update(eye, drawn);
            plan.Spent.Should().BeLessThanOrEqualTo(plan.BaseTriangleBudget, "the lift ended once frames brought nothing, a mesh at most past the budget");
        }
        plan.Whole.Should().BeTrue("the sections that came later were built in");
    }
}

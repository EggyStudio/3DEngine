using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

// Whether the field holds every mesh in reach, which the probes' carried rays bring the sky by, not
// while meshes arrive or wait to be built in, and still while a move of the eye builds the cascades
// again over frames, since the passes read each as it was last built.
public sealed partial class SceneFieldTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void The_Field_Is_Whole_Once_Every_Mesh_That_Came_Is_Built_In_And_Stays_So_While_A_Move_Of_The_Eye_Builds_It_Again()
    {
        var plan = new SceneFieldPlan(4, 0.25f, 4, triangleBudget: 65_536, triangles: _ => SectionTriangles);
        var drawn = AllSections();
        var eye = new Vector3(0, 2, 0);

        // Arriving, then settled and built in over the frames the budget takes.
        int frame;
        for (frame = 1; frame < SceneFieldPlan.SettleFrames; frame++)
        {
            plan.Update(eye, drawn);
            plan.Whole.Should().BeFalse($"the sections have not settled at frame {frame}, so the field holds none of them");
        }
        var building = 0;
        for (; frame < 400 && !plan.Whole; frame++)
        {
            plan.Update(eye, drawn);
            if (plan.Waiting > 0) building++;
        }
        plan.Whole.Should().BeTrue("the sections are built into every cascade");
        building.Should().BeGreaterThan(1, "the budget spread the first build over frames, the field not whole through them");

        // The eye carried past the cascades, which are built again around it over frames.
        eye = new Vector3(30, 2, 0);
        var again = 0;
        for (int moved = 0; moved < 400 && (moved == 0 || plan.Waiting > 0 || plan.Builds.Count > 0); moved++)
        {
            plan.Update(eye, drawn);
            if (plan.Waiting > 0) again++;
            plan.Whole.Should().BeTrue($"the cascades built again around the eye hold their sections as last built, {moved} frames after it moved");
        }
        again.Should().BeGreaterThan(1, "the move's builds went on over frames");

        // A section that comes is not held until it is built in.
        drawn.Add((new SceneFieldPlan.Instance(1, Section, Matrix4x4.CreateTranslation(34, 0, 6), false), false));
        plan.Update(eye, drawn);
        plan.Whole.Should().BeFalse("the section that came is in no cascade");
        for (frame = 0; frame < 400 && !plan.Whole; frame++) plan.Update(eye, drawn);
        plan.Whole.Should().BeTrue("the section that came settled and was built in");
    }
}

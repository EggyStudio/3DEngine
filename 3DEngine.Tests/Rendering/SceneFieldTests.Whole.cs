using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

// Whether the field holds every mesh in reach, which the probes' carried rays bring the sky by, not
// while meshes arrive or wait to be built in, and still while a move of the eye builds the cascades
// again over frames, since the passes read each as it was last built, and whether it holds the
// scene as it stands, which a picture of the scene waits for.
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

        // The eye carried along by less than the finest cascade's box, so the cascades shift and are
        // built again around it over frames, where a cut past a box builds them at once.
        eye = new Vector3(12, 2, 0);
        var again = 0;
        for (int moved = 0; moved < 400 && (moved == 0 || plan.Waiting > 0 || plan.Builds.Count > 0); moved++)
        {
            plan.Update(eye, drawn);
            if (plan.Waiting > 0)
            {
                again++;
                plan.Built.Should().BeFalse("a build runs, so the field does not hold the scene where the eye has it");
                plan.FramesBuilt.Should().Be(0);
            }
            plan.Whole.Should().BeTrue($"the cascades built again around the eye hold their sections as last built, {moved} frames after it moved");
        }
        again.Should().BeGreaterThan(1, "the move's builds went on over frames");
        plan.Update(eye, drawn);
        plan.Built.Should().BeTrue("the cascades are built around the eye with none waiting");
        plan.FramesBuilt.Should().BeGreaterThanOrEqualTo(1);

        // A section that comes is not held until it is built in.
        drawn.Add((new SceneFieldPlan.Instance(1, Section, Matrix4x4.CreateTranslation(34, 0, 6), false), false));
        plan.Update(eye, drawn);
        plan.Whole.Should().BeFalse("the section that came is in no cascade");
        for (frame = 0; frame < 400 && !plan.Whole; frame++) plan.Update(eye, drawn);
        plan.Whole.Should().BeTrue("the section that came settled and was built in");
    }
    [Fact]
    [Trait("Category", "Unit")]
    public void Meshes_That_Settle_While_A_Regions_Build_Runs_Are_Built_In_And_The_Field_Is_Whole_With_The_Eye_Still()
    {
        // An empty field built, then 50 sections a frame for ten frames into it, the eye still,
        // under a budget of four sections a frame, so each region's build runs over frames while
        // later sections settle and are taken into it. A section taken in was built into the
        // cascade by the build's end and waited for that cascade without end all the same, its
        // built frame the one the build gathered in, so the field was never whole.
        var plan = new SceneFieldPlan(4, 0.25f, 2, triangleBudget: 4 * SectionTriangles, triangles: _ => SectionTriangles);
        var eye = new Vector3(0, 2, 0);
        for (int frame = 0; frame < 60; frame++) plan.Update(eye, []);
        var drawn = new List<(SceneFieldPlan.Instance, bool)>();
        int after;
        for (after = 1; after < 2000; after++)
        {
            if (after <= 10)
                for (int i = (after - 1) * 50; i < after * 50; i++)
                    drawn.Add((new SceneFieldPlan.Instance(1, Section, Matrix4x4.CreateTranslation(SectionAt(i)), false), false));
            plan.Update(eye, drawn);
            if (after > 10 && plan.Whole && plan.Built) break;
        }
        plan.Whole.Should().BeTrue($"every section that came was built in, {plan.Unsettled.Waiting} waiting for cascades {string.Join(" ", plan.Unsettled.Cascades)}");
        plan.Built.Should().BeTrue();
    }
}

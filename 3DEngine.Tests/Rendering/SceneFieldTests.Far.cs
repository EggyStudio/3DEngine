using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

// The far cascade after the counted ones, its cells as wide as its reach takes, built under the
// budgets as the others are, and the probes' last cascade standing in it.
public sealed partial class SceneFieldTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void A_Far_Cascade_Reaches_Its_Distance_Ahead_In_Cells_Of_Its_Own_And_Is_Left_Out_Of_The_Count()
    {
        // Ore's field, four cascades of half a block reaching 256 across, and a far cascade to the
        // 384 blocks the game draws.
        var cell = SceneFieldPlan.FarCellFor(384, 4, 0.5f);
        var plan = new SceneFieldPlan(4, 0.5f, 1, farCell: cell);
        plan.Counted.Should().Be(4);
        plan.Cascades.Should().Be(5, "the far cascade comes after the counted ones");
        plan.HasFar.Should().BeTrue();
        plan.CellOf(3).Should().Be(4, "the counted cascades double as before");
        plan.CellOf(4).Should().Be(cell);
        cell.Should().BeGreaterThan(plan.CellOf(3) * 2 - 1e-4f, "a far cascade is at least twice as coarse as the last");

        // Its side ahead of an eye looking along -z lies at the reach or past it, by a snap at most.
        var eye = new Vector3(3, 70, -5);
        var origin = plan.OriginAround(4, eye, -Vector3.UnitZ);
        var ahead = eye.Z - origin.Z;
        ahead.Should().BeInRange(384 - SceneFieldPlan.SnapCells * cell, 384 + SceneFieldPlan.SnapCells * cell);

        // A reach no further than twice the last's cells reach takes twice its cells, and a reach
        // read from a far plane a little off gives the same cells.
        SceneFieldPlan.FarCellFor(100, 4, 0.5f).Should().Be(8);
        SceneFieldPlan.FarCellFor(384.003f, 4, 0.5f).Should().Be(cell);
        SceneFieldPlan.FarCellFor(0, 4, 0.5f).Should().Be(0, "none was asked for");
        SceneFieldPlan.FarCellFor(float.PositiveInfinity, 4, 0.5f).Should().Be(0, "a camera with no far plane gives no reach to fit");
        new SceneFieldPlan(4, 0.5f, 1).Cascades.Should().Be(4, "with no far cascade the count is the field's");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Far_Cascade_Is_Built_After_The_Counted_Ones_Under_The_Budget_With_The_Meshes_Past_Them()
    {
        // Two cascades of a quarter reaching 32 across, and a far one to 200 units, with the 500
        // sections and a row of them out to 180 units ahead, under a budget of one cascade a frame
        // and 65,536 triangles.
        var plan = new SceneFieldPlan(2, 0.25f, 1, triangleBudget: 65_536, triangles: _ => SectionTriangles, farCell: SceneFieldPlan.FarCellFor(200, 2, 0.25f));
        var eye = new Vector3(0, 2, 0);
        var drawn = AllSections();
        drawn.AddRange(Enumerable.Range(1, 9).Select(i =>
            (new SceneFieldPlan.Instance(2, Section, Matrix4x4.CreateTranslation(0, 0, -20 * i), false), false)));
        var builds = new List<SceneFieldPlan.Build>();
        for (int frame = 1; frame < 200; frame++)
        {
            plan.Update(eye, drawn, -Vector3.UnitZ);
            // From the frame the meshes settle in, the builds before it being of no mesh.
            if (frame >= SceneFieldPlan.SettleFrames) builds.AddRange(plan.Builds);
            plan.Splatted.Should().BeLessThanOrEqualTo(65_536, "the far cascade's build splats under the budget as the others' do");
            if (frame > SceneFieldPlan.SettleFrames && plan.Builds.Count == 0 && plan.Waiting == 0) break;
        }

        var far = builds.Where(build => build.Cascade == 2).ToList();
        far.Should().NotBeEmpty("the far cascade is built");
        far[0].Cell.Should().Be(plan.CellOf(2));
        builds.FindIndex(build => build.Cascade == 2).Should().BeGreaterThan(builds.FindLastIndex(build => build.Cascade == 1 && build.First),
            "the cascades are built finest first");
        far.Count.Should().BeGreaterThan(1, "the far cascade's 509 meshes are splatted over several frames");
        far.SelectMany(build => build.Instances).Should().Contain(instance => instance.World.Translation.Z <= -160,
            "the far cascade holds the meshes past the counted ones' reach");
        plan.BuiltOrigin(2).Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Last_Cascade_Of_Probes_Stands_In_The_Far_Cascade_With_As_Many_Directions_As_The_One_Before()
    {
        // Low traces the first two of four counted cascades, and its third, the far cascade's,
        // stands in the field's fifth.
        var plan = new SceneFieldPlan(4, 0.5f, 1, farCell: 16);
        plan.CascadeOfProbes(0, 3).Should().Be(0);
        plan.CascadeOfProbes(1, 3).Should().Be(1);
        plan.CascadeOfProbes(2, 3).Should().Be(4, "the probes' last cascade stands in the far one");
        new SceneFieldPlan(4, 0.5f, 1).CascadeOfProbes(1, 2).Should().Be(1, "with no far cascade each stands in its own");

        GlobalIlluminationRenderer.TexelsFor(GlobalIllumination.Low, 3, far: true).Should().Equal(8, 8, 8);
        GlobalIlluminationRenderer.TexelsFor(GlobalIllumination.High, 5, far: true).Should().Equal(8, 16, 16, 16, 16);
        GlobalIlluminationRenderer.TexelsFor(GlobalIllumination.Medium, 2, far: false).Should().Equal(8, 8);
    }
}

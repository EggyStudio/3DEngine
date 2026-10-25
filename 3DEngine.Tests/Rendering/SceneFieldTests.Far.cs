using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

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

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Sheet_Under_The_Far_Cascades_Wide_Cells_Rises_No_Slab_In_Them()
    {
        // A floor drawn double-sided under a far cascade of cells many units wide, which a cascade
        // holds as a sheet half a cell thick either side of it, as it holds a wall thinner than a
        // cell, so a ray between two cells still meets it. Half the far cascade's own cell stood such
        // a sheet as a slab rising past the eye, as the voxel game's flat ground of four blocks rose
        // 37 blocks under the far cascade's cells of 73.75. The floor is put so a far cell's middle
        // lies a quarter of a cell over it, which half a far cell held inside and which lies in the
        // open with the sheet held as thin as the last counted cascade holds one. The plane is of
        // many triangles, since one of fewer than sixteen is held as its box, which no sheet is.
        var config = Config.Default.WithWindow("scene field test", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 2, farCascade: 400);
        var floor = LoadModelFromMesh(GenMeshPlane(600, 600, 12, 12));
        floor.Materials[0].DoubleSided = true;
        var camera = new Camera3D(new Vector3(0, 3, 0), new Vector3(0, 3, -10), Vector3.UnitY, 60, CameraProjection.Perspective);
        var top = 0f;
        void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.RayWhite);
                BeginMode3D(camera);
                DrawModel(floor, new Vector3(0, top, 0), 1, Color.Gray);
                EndMode3D();
                EndDrawing();
            }
        }
        Frames(SceneFieldPlan.SettleFrames + 30);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        var plan = fields.Plan!;
        plan.HasFar.Should().BeTrue();
        var far = plan.Counted;
        var cell = plan.CellOf(far);
        cell.Should().BeGreaterThan(4, "the far cascade's cells are many units wide");
        var origin = plan.BuiltOrigin(far)!.Value;

        // The middle of the far cells' row nearest the floor's top, and the floor moved under it.
        var row = (int)MathF.Round((0 - origin.Y) / cell - 0.5f);
        top = origin.Y + (row + 0.5f) * cell - 0.25f * cell;
        // Moved, it is stamped as its box until it has stood as long as a mesh that moved stands.
        Frames(SceneFieldPlan.MobileSettleFrames + 30);
        plan.BuiltOrigin(far).Should().Be(origin, "the eye stood still, so the far cascade lies where it lay");

        var distances = ((GraphicsDevice)renderer.Context.Graphics!).ReadSceneField(fields.Field!);
        var size = SceneFieldPlan.Resolution;
        float At(int x, int y, int z) => distances[((far * size + z) * size + y) * size + x];
        // The row's cells ahead of the eye and beside it, past the counted cascades and over the floor.
        var eyeCell = (camera.Position - origin) / cell;
        var held = Enumerable.Range(-8, 17).SelectMany(dx => Enumerable.Range(4, 8).Select(dz => At((int)eyeCell.X + dx, row, (int)eyeCell.Z - dz))).ToList();
        held.Should().OnlyContain(d => d > 0.1f * cell, "a cell a quarter of a far cell over the floor lies in the open");
        UnloadModel(floor);
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

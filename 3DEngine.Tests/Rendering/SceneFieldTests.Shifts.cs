using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A cascade the eye moved by whole cells shifted as a clipmap is, the cells it keeps moved on the
// GPU and the slab it brings in built alone, which holds the field a build of the whole cascade at
// its new corner holds.
public sealed partial class SceneFieldTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void A_Move_Of_The_Eye_By_Whole_Cells_Shifts_Its_Cascade_And_Builds_The_Slab_It_Brings_In_Alone()
    {
        // The sections built around the eye, then the eye moved two units along x, eight cells of
        // the first cascade's quarter and a snap of its grid.
        var plan = new SceneFieldPlan(2, 0.25f, 2, triangles: _ => SectionTriangles);
        var drawn = AllSections();
        var eye = new Vector3(0, 2, 0);
        for (int frame = 0; frame < 400 && !plan.Built; frame++) plan.Update(eye, drawn);
        plan.Built.Should().BeTrue();
        var before = plan.BuiltOrigin(0)!.Value;

        eye = new Vector3(2, 2, 0);
        plan.Update(eye, drawn);
        var shift = plan.Builds.Should().ContainSingle(build => build.Cascade == 0).Subject;
        shift.ShiftAxis.Should().Be(0, "the eye moved along x");
        shift.ShiftCells.Should().Be(8, "two units are eight cells of a quarter");
        shift.Cells.Should().Be(new SceneFieldPlan.CellBox(56, 0, 0, 63, 63, 63), "the slab the move brings in at the side ahead");
        plan.BuiltOrigin(0).Should().Be(before + new Vector3(2, 0, 0), "the passes read the cascade at its new corner from the shift's first part");
        shift.Instances.Should().OnlyContain(instance => instance.World.Translation.X + 4 >= before.X + 2 + 55 * 0.25f - SceneFieldPlan.Band * 0.25f,
            "the slab's build splats the sections within the band of its cells alone");
        shift.Instances.Length.Should().BeLessThan(drawn.Count / 2, "a slab of eight cells of 64 holds few of the sections");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Field_Is_Not_Whole_While_The_Last_Cascades_Shift_Holds_Its_Slab_Empty()
    {
        // A section a frame, so the last cascade's slab is built over frames, at the field's edge.
        var plan = new SceneFieldPlan(2, 0.25f, 2, triangleBudget: 1, triangles: _ => SectionTriangles);
        var drawn = AllSections();
        var eye = new Vector3(0, 2, 0);
        for (int frame = 0; frame < 4000 && !plan.Built; frame++) plan.Update(eye, drawn);
        plan.Built.Should().BeTrue();

        // Four units along x, a snap of the last cascade's grid of eight cells of a half.
        eye = new Vector3(4, 2, 0);
        var (partial, whole, unbuilt) = (false, true, true);
        for (int frame = 0; frame < 4000 && (frame == 0 || !plan.Built); frame++)
        {
            plan.Update(eye, drawn);
            var building = plan.Builds.FirstOrDefault(build => build.Cascade == 1);
            if (building is { Cells: not null, Last: false })
            {
                partial = true;
                whole &= plan.Whole;
                unbuilt &= plan.Unbuilt == (1, building.Cells.Value);
            }
        }
        partial.Should().BeTrue("the last cascade's slab was built over frames");
        whole.Should().BeFalse("the field is not whole while the last cascade's slab is empty");
        unbuilt.Should().BeTrue("the plan says which cells wait while the slab's build goes on, which the passes read in the next cascade out");
        plan.Whole.Should().BeTrue("the slab built, the field holds every mesh in reach");
        plan.Unbuilt.Should().BeNull("no cells wait once the slab is built");
    }

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(SceneFieldConfig.DefaultTriangleBudget)]
    [InlineData(64)]
    public void A_Shifted_Cascade_Holds_The_Field_A_Build_Of_The_Whole_Cascade_At_Its_New_Corner_Holds(int triangleBudget)
    {
        // At the default budget each slab is built in the frame its shift is, and at 64 triangles,
        // five blocks a frame, over frames, the slab held empty until its build ends.
        var config = Config.Default.WithWindow("scene field shift", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 2, triangleBudget);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>;
        void Draw(Vector3 eye)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(eye, eye + new Vector3(0, -0.2f, -1), Vector3.UnitY, 60));
            DrawModelEx(block, new Vector3(0, -0.25f, 0), Vector3.UnitY, 0, new Vector3(40, 0.5f, 40), Color.Gray);
            for (int i = 0; i < 12; i++)
                DrawModelEx(block, new Vector3(i * 2.3f - 12, 1, (i % 3) * 3 - 3), Vector3.UnitY, i * 17, new Vector3(1, 2 + i % 3, 0.6f), Color.Gray);
            EndMode3D();
            EndDrawing();
        }
        float[] Settled(Vector3 eye)
        {
            for (int frame = 0; frame < 120; frame++)
            {
                Draw(eye);
                if (fields()?.Plan is { Built: true, FramesBuilt: > 4 }) break;
            }
            fields()!.Plan!.Built.Should().BeTrue("the field is built around the eye");
            return SceneFieldStages.Of((GraphicsDevice)renderer.Context.Graphics!).ReadSceneField(fields()!.Field!);
        }

        Settled(new Vector3(0, 1.6f, 4));
        // Two and a half units along x, a snap of eight cells for both cascades, shifted.
        Draw(new Vector3(2.5f, 1.6f, 4));
        fields()!.Plan!.Builds.Should().Contain(build => build.ShiftAxis == 0, "the move shifts the cascades");
        var shifted = Settled(new Vector3(2.5f, 1.6f, 4));
        fields()!.Plan!.Rebuild(2);
        var whole = Settled(new Vector3(2.5f, 1.6f, 4));

        var worst = 0f;
        for (int i = 0; i < whole.Length; i++)
            worst = MathF.Max(worst, MathF.Abs(MathF.Min(shifted[i], 1) - MathF.Min(whole[i], 1)));
        worst.Should().BeLessThan(0.01f, "the shifted cascades hold what a build of the whole of them at the new corner holds, within the band");
        UnloadModel(block);
    }
}

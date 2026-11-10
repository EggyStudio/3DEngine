using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A budget changed through SetSceneField taken by the field as it stands, where its cascades and its
// cell stand as they were, so a game can raise it for a while after a cut of its own.
public sealed partial class SceneFieldTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Budget_Changed_Through_SetSceneField_Keeps_The_Field_And_A_Changed_Cell_Makes_It_Again()
    {
        var config = Config.Default.WithWindow("scene field budgets", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 2);
        var cube = LoadModelFromMesh(GenMeshCube(2, 2, 2));
        var camera = new Camera3D(new Vector3(4, 3, 4), Vector3.Zero, Vector3.UnitY, 60, CameraProjection.Perspective);
        void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.RayWhite);
                BeginMode3D(camera);
                DrawModel(cube, Vector3.Zero, 1, Color.Gray);
                EndMode3D();
                EndDrawing();
            }
        }
        Frames(SceneFieldPlan.SettleFrames + 30);
        var fields = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<SceneFieldRenderer>()!;
        var (plan, field) = (fields.Plan!, fields.Field!);
        plan.Built.Should().BeTrue("the cube is built in");
        var built = plan.FramesBuilt;

        // Four times the triangles and four cascades a frame, the field kept with what it holds.
        SetSceneField(2, 0.25f, 4, 4 * SceneFieldConfig.DefaultTriangleBudget);
        Frames(1);
        fields.Plan.Should().BeSameAs(plan, "a budget alone changed keeps the field's plan");
        fields.Field.Should().BeSameAs(field, "a budget alone changed keeps the field's cascades on the device");
        (plan.Budget, plan.BaseTriangleBudget).Should().Be((4, 4 * SceneFieldConfig.DefaultTriangleBudget));
        plan.FramesBuilt.Should().BeGreaterThan(built, "the field stays built, lit as it was");

        // Put back, kept again.
        SetSceneField(2, 0.25f, 2);
        Frames(1);
        fields.Plan.Should().BeSameAs(plan);
        plan.BaseTriangleBudget.Should().Be(SceneFieldConfig.DefaultTriangleBudget);

        // A cell changed makes it again.
        SetSceneField(2, 0.5f, 2);
        Frames(1);
        fields.Plan.Should().NotBeSameAs(plan, "a changed cell makes the field again");
        UnloadModel(cube);
    }
}

using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The march field.march answers with, read back from the field the GPU built.
public sealed partial class SceneFieldTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_March_Read_Back_Stops_At_A_Wall_In_The_Cascade_And_Cell_That_Hold_It_And_Leaves_The_Field_Above_It()
    {
        // A floor and a wall 0.3 thick whose face looks along -x at x 1.85, in a field of two
        // cascades of 0.125 around the eye, under a sun behind the wall.
        var config = Config.Default.WithWindow("scene field test", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.125f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, -1, 0)), Color.White, 1);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(-2, 1, 0), new Vector3(2, 1, 0), Vector3.UnitY, 60, CameraProjection.Perspective);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.RayWhite);
            BeginMode3D(camera);
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(8, 0.3f, 8), Color.Gray);
            DrawModelEx(slab, new Vector3(2, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4), Color.Gray);
            EndMode3D();
            EndDrawing();
        }

        var march = FieldMarch.Read(GetApp().World, out var why);
        march.Should().NotBeNull(why);
        var start = new Vector3(0, 1, 0);
        var toWall = march!.March(start, Vector3.UnitX, 0, 1e5f);
        toWall.Ended.Should().Be("met a surface");
        toWall.Met.Should().BeApproximately(1.85f, 0.125f, "the march stops within a cell of the wall's face");
        var hit = start + Vector3.UnitX * toWall.Met;
        march.CascadeAt(hit, 1).Should().Be(0, "the finest cascade holds the wall two units from the eye");
        Vector3.Dot(march.Normal(hit), -Vector3.UnitX).Should().BeGreaterThan(0.9f, "the wall's face looks along -x");
        march.March(start, Vector3.UnitY, 0, 1e5f).Ended.Should().Be("left every cascade", "nothing stands over the floor");

        var said = march.Describe(start, Vector3.UnitX, Vector3.Normalize(new Vector3(1, 1, 0)));
        said.Should().Contain("met a surface").And.Contain("in cascade 0's cell").And.Contain("then from the surface",
            "the march goes on from the wall toward the sun behind it");
        UnloadModel(slab);
    }
}

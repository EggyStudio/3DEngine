using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

public sealed partial class SceneFieldTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Mesh_Whose_Material_Bounces_No_Light_Casts_Its_Shadow_And_Is_Not_Held_In_The_Field()
    {
        // A cube a unit over a floor under a sun that casts shadows, its material's BouncesLight
        // off, as a game's walking animal has it, so the field does not stamp it again in each
        // frame it moves.
        var config = Config.Default.WithWindow("scene field test", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.25f);
        var way = Vector3.Normalize(new Vector3(-1, -1, 0));
        CreateDirectionalLight(way, Color.White, 2, castsShadows: true);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0].BouncesLight = false;
        var camera = new Camera3D(new Vector3(0, 7, 5), Vector3.Zero, Vector3.UnitY, 45, CameraProjection.Perspective);
        var path = Path.Combine(_folder.Path, "unbounced.png");
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(slab, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(8, 0.2f, 8), Color.White);
            DrawModel(cube, new Vector3(0, 1, 0), 1, Color.White);
            EndMode3D();
            if (frame == SceneFieldPlan.SettleFrames) TakeScreenshot(path);
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        fields.Drawn.Should().ContainSingle("the floor alone is held, the cube left out").Which.Instance.Mesh.Should().Be(slab.Meshes[0].Id);
        var distances = ((GraphicsDevice)renderer.Context.Graphics!).ReadSceneField(fields.Field!);
        var origin = fields.Plan!.BuiltOrigin(0)!.Value;
        var cell = Vector3.Clamp((new Vector3(0, 1, 0) - origin) / 0.25f, Vector3.Zero, new Vector3(SceneFieldPlan.Resolution - 1));
        distances[((int)cell.Z * SceneFieldPlan.Resolution + (int)cell.Y) * SceneFieldPlan.Resolution + (int)cell.X]
            .Should().BeGreaterThan(0.5f, "the field holds nothing where the cube stands, a unit over the floor");

        // The floor where the cube's middle throws its shadow, and as far the other way, lit.
        var shadow = new Vector3(0, 1, 0) + way * (-1 / way.Y);
        var lit = new Vector3(-shadow.X, 0, -shadow.Z) * 2;
        var image = LoadImage(path);
        var (dark, bright) = (GetWorldToScreenEx(shadow, camera, 96, 64), GetWorldToScreenEx(lit, camera, 96, 64));
        var shadowed = GetImageColor(image, (int)dark.X, (int)dark.Y).R;
        var unshadowed = GetImageColor(image, (int)bright.X, (int)bright.Y).R;
        unshadowed.Should().BeGreaterThan(80, "the sun lights the floor clear of the cube");
        shadowed.Should().BeLessThan((byte)(unshadowed / 2), "the cube casts its shadow all the same");
        UnloadModel(slab);
        UnloadModel(cube);
    }
}

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

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Mesh_Entity_Whose_Material_Casts_No_Shadow_Leaves_The_Floor_Lit_And_One_That_Bounces_No_Light_Is_Not_Held_In_The_Field()
    {
        // Three cubes as entities a unit over a floor under a sun that casts shadows, one as any
        // other, one whose Material casts no shadow, as a voxel game's glass and water, and one
        // that bounces no light, as its grass, which casts its shadow all the same.
        var config = Config.Default.WithWindow("scene field test", 128, 96) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.25f);
        var way = Vector3.Normalize(new Vector3(-1, -1, 0));
        CreateDirectionalLight(way, Color.White, 2, castsShadows: true);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var cube = GenMeshCube(1, 1, 1);
        var ecs = GetApp().World.Resource<EcsWorld>();
        void Spawn(float z, bool casts, bool bounces)
        {
            var entity = ecs.Spawn();
            ecs.Add(entity, new Mesh(cube));
            ecs.Add(entity, new Material(Color.White) { AlphaMode = MaterialAlphaMode.Opaque, CastsShadows = casts, BouncesLight = bounces });
            ecs.Add(entity, new Transform(new Vector3(0, 1, z)));
        }
        Spawn(-2.5f, true, true);
        Spawn(0, false, true);
        Spawn(2.5f, true, false);
        var camera = new Camera3D(new Vector3(0, 9, 6), Vector3.Zero, Vector3.UnitY, 45, CameraProjection.Perspective);
        var path = Path.Combine(_folder.Path, "entities.png");
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(slab, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(10, 0.2f, 10), Color.White);
            EndMode3D();
            if (frame == SceneFieldPlan.SettleFrames) TakeScreenshot(path);
            EndDrawing();
        }
        var fields = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<SceneFieldRenderer>()!;
        fields.Drawn.Where(drawn => drawn.Instance.Mesh == cube.Id).Select(drawn => drawn.Instance.World.Translation.Z)
            .Should().Equal([-2.5f], "the field holds the cube that casts a shadow and bounces light, and neither of the others");

        // The floor where each cube's middle throws its shadow, and as far the other way, lit.
        var image = LoadImage(path);
        byte Floor(Vector3 at)
        {
            var seen = GetWorldToScreenEx(at, camera, 128, 96);
            return GetImageColor(image, (int)seen.X, (int)seen.Y).R;
        }
        var drop = way * (-1 / way.Y);
        foreach (var z in new[] { -2.5f, 0, 2.5f })
            Floor(new Vector3(-drop.X, 0, z)).Should().BeGreaterThan(80, $"the sun lights the floor clear of the cube at {z}");
        var (twin, glass, grass) = (Floor(new Vector3(drop.X, 0, -2.5f)), Floor(new Vector3(drop.X, 0, 0)), Floor(new Vector3(drop.X, 0, 2.5f)));
        var clear = Floor(new Vector3(-drop.X, 0, 0));
        twin.Should().BeLessThan((byte)(clear / 2), "the cube as any other casts its shadow");
        grass.Should().BeLessThan((byte)(clear / 2), "the cube that bounces no light casts its shadow all the same");
        glass.Should().BeGreaterThan((byte)(clear * 3 / 4), "the cube that casts no shadow leaves the floor under the sun lit");
        UnloadModel(slab);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Roof_Drawn_Into_The_Shadow_Maps_Alone_Is_Held_In_The_Field_And_Keeps_The_Sky_Off_The_Room_Under_It()
    {
        // A room of walls on a ground under a bright sky, its roof drawn through a material with
        // ShadowOnly set, as the voxel game draws the rock its eye cannot see. The field holds the
        // roof as the shadows do, so the sky's light reaches the floor no more than under a roof
        // drawn as any other, where left out of the field the floor took the sky through it. The
        // frame's mean light from inside read 0.0001 under either roof on the development machine's
        // GPU and 0.058 under none, and 0.058 under the roof left out of the field.
        var config = Config.Default.WithWindow("shadow only roof", 160, 90) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetGlobalIllumination(GlobalIllumination.Low);
        var sky = new byte[64 * 32 * 4];
        for (int i = 0; i < 64 * 32; i++)
            (sky[i * 4], sky[i * 4 + 1], sky[i * 4 + 2], sky[i * 4 + 3]) = i < 64 * 16 ? ((byte)120, (byte)165, (byte)230, (byte)255) : ((byte)90, (byte)100, (byte)110, (byte)255);
        SetEnvironmentMap(new Image(sky, 64, 32));
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var hidden = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        hidden.Materials[0] = new ModelMaterial(Color.Gray) { ShadowOnly = true };
        var stone = new Color(150, 150, 150);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var eye = new Vector3(0.4f, 1.5f, 1.2f);
        var camera = new Camera3D(eye, eye + new Vector3(-0.3f, -0.8f, -1), Vector3.UnitY, 70);
        double Seen(Model? roof)
        {
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 60; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModelEx(block, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(64, 1, 64), stone);
                DrawModelEx(block, new Vector3(-3.25f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.5f, 3, 7), stone);
                DrawModelEx(block, new Vector3(3.25f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.5f, 3, 7), stone);
                DrawModelEx(block, new Vector3(0, 1.5f, -3.25f), Vector3.UnitY, 0, new Vector3(6, 3, 0.5f), stone);
                DrawModelEx(block, new Vector3(0, 1.5f, 3.25f), Vector3.UnitY, 0, new Vector3(6, 3, 0.5f), stone);
                if (roof is { } shown) DrawModelEx(shown, new Vector3(0, 3.25f, 0), Vector3.UnitY, 0, new Vector3(7, 0.5f, 7), stone);
                EndMode3D();
                EndDrawing();
            }
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            double sum = 0;
            for (int p = 0; p < 160 * 90 * 4; p += 4)
                sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
            return sum / (160 * 90);
        }

        var open = Seen(null);
        var drawn = Seen(block);
        var alone = Seen(hidden);
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        fields.Drawn.Should().Contain(held => held.Instance.Mesh == hidden.Meshes[0].Id, "the field holds the roof the shadows alone draw");
        alone.Should().BeLessThan(open * 0.05,
            $"the roof the shadows alone draw keeps the sky's light off the floor, {alone / open:0.0000} of the open ground's where {drawn / open:0.0000} under one drawn");
        UnloadModel(block);
        UnloadModel(hidden);
    }
}

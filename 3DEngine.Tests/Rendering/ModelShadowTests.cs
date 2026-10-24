using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A model's material deciding whether it casts a shadow, read from the floor under it, and a
/// caster the camera does not see still casting one where the camera's pass leaves it out.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class ModelShadowTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-shadow-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // Draws frames until the first one's capture has been written, as ReferenceFrameTests does.
    private Image Capture(Action draw, string name)
    {
        var path = Path.Combine(_folder.Path, name + ".png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            draw();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    // Draws frames until the scene's distance field has settled, then captures one.
    private Image Settled(Action draw, string name)
    {
        var path = Path.Combine(_folder.Path, name + ".png");
        var at = SceneFieldPlan.SettleFrames + 6;
        for (int frame = 0; frame < at + 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            draw();
            if (frame == at) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    [NeedsVulkanFact]
    public void With_Contact_Shadows_Off_A_Shadow_The_Map_Holds_Keeps_Its_Hard_Edge_With_The_Field_On()
    {
        var config = Config.Default.WithWindow("contact shadow test", 320, 180) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        // A pillar a unit across under a low sun, its shadow running three units and more toward -x,
        // with the scene's distance field on, which the contact shadows are traced through.
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, -0.45f, 0)), Color.White, 1, castsShadows: true);
        SetSceneField(2, 0.25f, 2);
        var floor = LoadModelFromMesh(GenMeshPlane(30, 30, 1, 1));
        var pillar = LoadModelFromMesh(GenMeshCube(1, 3, 1));
        var camera = new Camera3D(new Vector3(-3, 7, 4), new Vector3(-3, 0, 0), Vector3.UnitY, 45);
        void Scene()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(floor, Vector3.Zero, 1, Color.White);
            DrawModel(pillar, new Vector3(0, 1.5f, 0), 1, Color.White);
            EndMode3D();
        }
        // The floor a fifth of a unit past the shadow's side, which the map leaves lit, inside the
        // shadow, and far from it. On the RTX 4070 the floor past the side read 0.71 of the far
        // floor's light with contact shadows on and 1.00 off or kept past the map, and inside the
        // shadow, the map reaching none of it, 0 kept past the map and 169 of 255 off.
        int Red(Image image, Vector3 at)
        {
            var pixel = GetWorldToScreen(at, camera);
            return GetImageColor(image, (int)pixel.X, (int)pixel.Y).R;
        }
        float Beside(Image image) => Red(image, new Vector3(-3, 0, 0.7f)) / (float)Red(image, new Vector3(-3, 0, 2.5f));

        var on = Settled(Scene, "on");
        SetContactShadows(ContactShadows.Off);
        var off = Settled(Scene, "off");
        SetContactShadows(ContactShadows.PastShadowMap);
        var past = Settled(Scene, "past");
        // With the map reaching a unit past the camera and no fill past it, it holds none of the floor.
        SetShadowFill(false);
        SetShadowDistance(1);
        var pastBeyond = Settled(Scene, "past-beyond");
        SetContactShadows(ContactShadows.Off);
        var offBeyond = Settled(Scene, "off-beyond");

        Beside(off).Should().BeGreaterThan(0.95f, "with contact shadows off the floor past the map's hard edge is lit as the floor far from it");
        Beside(past).Should().BeGreaterThan(0.95f, "kept past the map's reach alone, they leave a shadow the map holds as it draws it");
        Beside(on).Should().BeLessThan(Beside(off) - 0.05f, "the field's contact shadow softens the edge past the map's, which the setting takes away");
        Red(pastBeyond, new Vector3(-3, 0, 0)).Should().BeLessThan(Red(offBeyond, new Vector3(-3, 0, 0)) - 40,
            "past the map's reach they still shadow the ground the map no longer holds");
        UnloadModel(floor);
        UnloadModel(pillar);
    }

    [NeedsVulkanFact]
    public void A_Cave_Under_Ten_Units_Of_Stone_Seventy_Up_Takes_No_Sun_On_Its_Floor_Or_Its_Blocks_Tops()
    {
        // A cave's floor with a block on it 4, 10, 25, 50 and 90 units out, a cascade or more apart,
        // under a roof ten units thick 70 units up, as the voxel game's ground over its caves, seen
        // from inside looking down. A roof that far above the view's slice lay before the near
        // cascades' near plane, where the map left it out, so the sun lit the floor and the near
        // blocks' tops through it, 255 and 210 on the development machine's GPU before the casters
        // there were drawn onto the near plane; the game's caves read 34 to 20 on their tops.
        var config = Config.Default.WithWindow("cave test", 640, 360) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetAmbientLight(Color.Black, 0);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.3f, -1, 0.2f)), Color.White, 3, castsShadows: true);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var eye = new Vector3(0, 3, 0);
        var camera = new Camera3D(eye, eye + new Vector3(0, -0.45f, -1), Vector3.UnitY, 70);
        float[] distances = [4, 10, 25, 50, 90];
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(block, new Vector3(0, -0.5f, -150), Vector3.UnitY, 0, new Vector3(400, 1, 400), Color.Gray);
            foreach (var d in distances) DrawModelEx(block, new Vector3(0, 0.5f, -d), Vector3.UnitY, 0, Vector3.One, Color.White);
            DrawModelEx(block, new Vector3(0, 75, -150), Vector3.UnitY, 0, new Vector3(400, 10, 400), Color.Gray);
            EndMode3D();
        }, "cave");
        int Lit(Vector3 at)
        {
            var pixel = GetWorldToScreenEx(at, camera, 640, 360);
            var c = GetImageColor(image, (int)pixel.X, (int)pixel.Y);
            return Math.Max(c.R, Math.Max(c.G, c.B));
        }
        foreach (var d in distances)
        {
            Lit(new Vector3(0, 1, -d)).Should().BeLessThan(8, $"the block {d} units out lies under the roof, its top unlit");
            Lit(new Vector3(1.5f, 0, -d)).Should().BeLessThan(8, $"the floor {d} units out lies under the roof");
        }
        UnloadModel(block);
    }

    [NeedsVulkanFact]
    public void A_Low_Suns_Long_Shadow_Is_Cast_Whole_Across_The_Cascades_Seen_From_Its_Far_End()
    {
        // A trunk a unit across and six high under a crown three units wide, under a sun 10 degrees
        // up, whose shadows run along the ground to x = -34 and from x = -30 to -50, seen from the
        // far end of the shadow looking back at the tree. At so low a sun the near cascade's tile
        // reaches far along the ground toward the sun, past its own near plane, before which the
        // tree was left out and clipped, so ground in the shadow read lit: from 55 units out the
        // trunk's shadow stopped partway, and from 65 the crown's was gone near the camera.
        var config = Config.Default.WithWindow("low sun test", 480, 270) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var elevation = 10 * MathF.PI / 180;
        CreateDirectionalLight(new Vector3(-MathF.Cos(elevation), -MathF.Sin(elevation), 0), Color.White, 1, castsShadows: true);
        var ground = LoadModelFromMesh(GenMeshPlane(400, 400, 1, 1));
        var trunk = LoadModelFromMesh(GenMeshCube(1, 6, 1));
        var crown = LoadModelFromMesh(GenMeshCube(3, 3, 3));

        foreach (var from in new[] { -55f, -65f })
        {
            var camera = new Camera3D(new Vector3(from, 4, 3), new Vector3(from + 55, 2, 0), Vector3.UnitY, 60);
            var image = Capture(() =>
            {
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModel(ground, Vector3.Zero, 1, Color.White);
                DrawModel(trunk, new Vector3(0, 3, 0), 1, Color.White);
                DrawModel(crown, new Vector3(0, 7, 0), 1, Color.White);
                EndMode3D();
            }, $"low-sun-{-from}");
            int Red(Vector3 at)
            {
                var pixel = GetWorldToScreen(at, camera);
                return GetImageColor(image, (int)pixel.X, (int)pixel.Y).R;
            }
            // Along the middle of the shadow, the trunk's to -30 and the crown's past it, each
            // against the ground six units beside it.
            foreach (var x in new[] { -6f, -12f, -18f, -24f, -30f, -36f, -42f, -46f })
                Red(new Vector3(x, 0, 0)).Should().BeLessThan((byte)(Red(new Vector3(x, 0, 6)) / 3),
                    $"the ground at x = {x} lies in the tree's shadow, seen from x = {from}");
        }
        UnloadModel(ground);
        UnloadModel(trunk);
        UnloadModel(crown);
    }

    [NeedsVulkanFact]
    public void A_Material_That_Casts_No_Shadow_Leaves_The_Floor_Under_It_Lit()
    {
        var config = Config.Default.WithWindow("shadow test", 160, 120) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        CreateDirectionalLight(new Vector3(0.01f, -1, 0), Color.White, 1, castsShadows: true);
        var floor = LoadModelFromMesh(GenMeshPlane(10, 10, 1, 1));
        var sphere = LoadModelFromMesh(GenMeshSphere(0.8f, 16, 16));
        var camera = new Camera3D(new Vector3(0, 6, 4), Vector3.Zero, Vector3.UnitY, 45);
        Action Scene(bool casts) => () =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(floor, Vector3.Zero, 1, Color.White);
            sphere.Materials[0] = new ModelMaterial(Color.White) { CastsShadows = casts };
            DrawModel(sphere, new Vector3(0, 1.5f, 0), 1, Color.White);
            EndMode3D();
        };

        var shadowed = Capture(Scene(casts: true), "casts");
        var lit = Capture(Scene(casts: false), "none");

        var under = GetWorldToScreen(Vector3.Zero, camera);
        var (x, y) = ((int)under.X, (int)under.Y);
        GetImageColor(shadowed, x, y).R.Should().BeLessThan((byte)(GetImageColor(lit, x, y).R - 60), "the sphere's shadow darkens the floor under it");
        GetImageColor(lit, x, y).R.Should().BeGreaterThan(150, "with no shadow the floor is lit by the sun");
        UnloadModel(floor);
        UnloadModel(sphere);
    }

    [NeedsVulkanFact]
    public void A_Caster_Outside_The_View_Still_Shadows_The_Floor_It_Sees_And_Is_Left_Out_Of_The_Cameras_Pass()
    {
        var config = Config.Default.WithWindow("shadow test", 160, 120) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        // The sun falls toward +x at 45 degrees, so a cube six units up and six to the left of the
        // origin, far outside the camera's narrow view, throws its shadow onto the origin.
        CreateDirectionalLight(Vector3.Normalize(new Vector3(1, -1, 0)), Color.White, 1, castsShadows: true);
        var floor = GenMeshPlane(10, 10, 1, 1);
        var cube = GenMeshCube(1.5f, 1.5f, 1.5f);
        var material = new ModelMaterial(Color.White);
        var camera = new Camera3D(new Vector3(0, 5, 3), Vector3.Zero, Vector3.UnitY, 30);
        var caster = new Vector3(-6, 6, 0);
        var seen = GetWorldToScreen(caster, camera);
        (seen.X is >= 0 and < 160 && seen.Y is >= 0 and < 120).Should().BeFalse("the cube lies outside the view");
        Action Scene(bool cast) => () =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawMesh(floor, material, Matrix4x4.Identity);
            if (cast) DrawMesh(cube, material, Matrix4x4.CreateTranslation(caster));
            EndMode3D();
        };

        var shadowed = Capture(Scene(cast: true), "outside");
        var calls = GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ModelRenderer>().CallsByPass.ToDictionary();
        var lit = Capture(Scene(cast: false), "nothing");

        var under = GetWorldToScreen(Vector3.Zero, camera);
        var (x, y) = ((int)under.X, (int)under.Y);
        GetImageColor(shadowed, x, y).R.Should().BeLessThan((byte)(GetImageColor(lit, x, y).R - 60), "the cube's shadow reaches the floor the camera sees");
        calls.GetValueOrDefault("camera").Should().Be(1, "the camera's pass draws the floor and leaves the cube out");
        calls.Where(c => c.Key.StartsWith("cascade", StringComparison.Ordinal)).Max(c => c.Value)
            .Should().Be(2, $"the cascade holding the origin draws the cube with the floor, {string.Join(", ", calls.Select(c => $"{c.Key} {c.Value}"))}");
        UnloadMesh(floor);
        UnloadMesh(cube);
    }

    [NeedsVulkanFact]
    public void A_Mesh_Drawn_With_A_Shader_That_Keeps_Its_Positions_Is_Left_Out_Of_A_Cascade_It_Lies_Outside()
    {
        // A floor under the camera and a cube drawn with a shader of the program's own far off to the
        // side, outside the camera's view and the nearest cascade's box, in the farthest cascade's.
        // Its vertex stage hands the positions on as they are, and once the program says so the
        // camera's pass and the nearest cascade's leave the cube out, as they would one drawn with the
        // model pass's own shader.
        var config = Config.Default.WithWindow("shadow test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.3f, -1, 0.2f)), Color.White, 1, castsShadows: true);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            [shader("vertex")]
            ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ModelInstance instance)
            {
                return transformModelVertex(position, normal, uv, instance);
            }

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return float4(lit(input.color.rgb, input), 1);
            }
            """, "kept.slang");
        var floor = GenMeshPlane(4, 4, 1, 1);
        var cube = GenMeshCube(1, 1, 1);
        var plain = new ModelMaterial(Color.White);
        var own = new ModelMaterial(Color.White) { Shader = shader };
        var camera = new Camera3D(new Vector3(0, 4, 3), Vector3.Zero, Vector3.UnitY, 40);
        Dictionary<string, int> Calls(bool keeps)
        {
            SetShaderKeepsPositions(shader, keeps);
            Capture(() =>
            {
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawMesh(floor, plain, Matrix4x4.Identity);
                DrawMesh(cube, own, Matrix4x4.CreateTranslation(20, 0.5f, 0));
                EndMode3D();
            }, keeps ? "kept" : "moved");
            return GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ModelRenderer>().CallsByPass.ToDictionary();
        }

        var moved = Calls(keeps: false);
        var kept = Calls(keeps: true);
        string Read(Dictionary<string, int> calls) => string.Join(", ", calls.Select(c => $"{c.Key} {c.Value}"));
        var nearest = kept.Keys.Where(k => k.StartsWith("cascade", StringComparison.Ordinal)).Order(StringComparer.Ordinal).First();
        moved.GetValueOrDefault(nearest).Should().Be(2, $"a shader that may move vertices is drawn whole in every pass, {Read(moved)}");
        kept.GetValueOrDefault(nearest).Should().Be(1, $"the nearest cascade leaves out the cube outside its box, {Read(kept)}");
        kept.GetValueOrDefault("camera").Should().Be(1, $"the camera's pass leaves out the cube outside its view, {Read(kept)}");
        kept.Where(c => c.Key.StartsWith("cascade", StringComparison.Ordinal)).Max(c => c.Value)
            .Should().Be(2, $"the cascade whose box holds the cube draws it, {Read(kept)}");
        UnloadMesh(floor);
        UnloadMesh(cube);
        UnloadShader(shader);
    }
}

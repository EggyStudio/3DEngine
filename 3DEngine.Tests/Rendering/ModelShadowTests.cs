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
    public void A_Roof_Drawn_Into_The_Shadow_Maps_Alone_Keeps_The_Sun_Off_A_Cave_And_Is_Seen_Nowhere()
    {
        // A cave's floor with slabs on it 4 to 90 units out and its roof 45 units over the floor,
        // which a game drawing only what its eye sees leaves out of the view from inside, as the
        // voxel game leaves out the rock over its caves, the eye 30 units up looking down so each
        // slab's top is a few pixels deep. Left out, the sun reaches the tops at the game's shadow
        // distances, as it reached the game's at 45 blocks; drawn into the shadow maps alone, the
        // roof keeps them dark and shows nowhere, so from above the floor is seen through it in its
        // shadow.
        var config = Config.Default.WithWindow("cave roof test", 640, 360) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetAmbientLight(Color.Black, 0);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.3f, -1, 0.2f)), Color.White, 3, castsShadows: true);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var roof = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        roof.Materials[0] = new ModelMaterial(Color.Gray) { ShadowOnly = true };
        var eye = new Vector3(0, 30, 0);
        var inside = new Camera3D(eye, eye + new Vector3(0, -0.766f, -0.643f), Vector3.UnitY, 70);
        var above = new Camera3D(new Vector3(0, 90, 20), new Vector3(0, 0, -20), Vector3.UnitY, 50);
        float[] distances = [4, 10, 25, 45, 90];
        Image Drawn(Camera3D camera, bool roofed, string name) => Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(block, new Vector3(0, -0.5f, -150), Vector3.UnitY, 0, new Vector3(400, 1, 400), Color.Gray);
            foreach (var d in distances) DrawModelEx(block, new Vector3(0, 0.5f, -d), Vector3.UnitY, 0, new Vector3(4, 1, 4), Color.White);
            if (roofed) DrawModelEx(roof, new Vector3(0, 50, -150), Vector3.UnitY, 0, new Vector3(400, 10, 400), Color.Gray);
            EndMode3D();
        }, name);
        int Lit(Image image, Camera3D camera, Vector3 at)
        {
            var pixel = GetWorldToScreenEx(at, camera, 640, 360);
            var c = GetImageColor(image, (int)pixel.X, (int)pixel.Y);
            return Math.Max(c.R, Math.Max(c.G, c.B));
        }

        foreach (var reach in new[] { 96f, 150f })
        {
            SetShadowDistance(reach);
            var open = Drawn(inside, false, $"open-{reach}");
            var roofed = Drawn(inside, true, $"roofed-{reach}");
            foreach (var d in distances)
            {
                Lit(open, inside, new Vector3(0, 1, -d)).Should().BeGreaterThan(100, $"with no roof the sun reaches the block {d} units out at a shadow distance of {reach}");
                Lit(roofed, inside, new Vector3(0, 1, -d)).Should().BeLessThan(8, $"the roof drawn into the shadow maps alone keeps the sun off the block {d} units out at {reach}");
                Lit(roofed, inside, new Vector3(3.5f, 0, -d)).Should().BeLessThan(8, $"and off the floor {d} units out at {reach}");
            }
        }
        var fromAbove = Drawn(above, true, "above");
        Lit(fromAbove, above, new Vector3(5, 55, -20)).Should().BeLessThan(8, "the roof's sunlit top is not drawn, and the floor seen through it lies in its shadow");
        UnloadModel(block);
        UnloadModel(roof);
    }

    [NeedsVulkanFact]
    public void A_Shader_That_Reads_The_Suns_Shadow_Itself_Lights_By_Its_Read_In_Place_Of_The_Point()
    {
        // A block two units high on a floor under a sun along x and down, whose shadow lies from
        // 0.5 to 2.5 along x, the floor drawn through a shader that hands litLight the sun's shadow
        // read at its point moved by `shift`, as a program stepping the shadow along a grid of its
        // own reads it once. Moved by nothing it lights the floor as litLight's own read does, and
        // moved one unit along x the shadow it reads lies one unit back, from -0.5 to 1.5.
        var config = Config.Default.WithWindow("shadow read test", 320, 180) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetAmbientLight(Color.Black, 0);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(1, -1, 0)), Color.White, 2, castsShadows: true);
        var shaders = new[] { "LitLight light = litLight(albedo, input);", "float held;\n    LitLight light = litLight(albedo, input, lightReaching(input.world + shift, normalize(input.normal), held), held);" }
            .Select((body, i) => LoadShaderFromMemory($$"""
                import modelpass;
                import lights;

                uniform float3 shift;

                [shader("fragment")]
                float4 fragmentMain(ModelVertexOutput input) : SV_Target
                {
                    float3 albedo = baseColor(input).rgb;
                    {{body}}
                    return float4(toDisplay(light.diffuse + light.specular), 1.0);
                }
                """, $"shadow-read-{i}.slang")).ToArray();
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var floor = LoadModelFromMesh(GenMeshCube(20, 1, 20));
        var camera = new Camera3D(new Vector3(1.5f, 6, 6), new Vector3(1.5f, 0, 0), Vector3.UnitY, 50);
        Image Drawn(Shader shader, float shift, string name)
        {
            floor.Materials[0].Shader = shader;
            SetShaderValue(shader, GetShaderLocation(shader, "shift"), new Vector3(shift, 0, 0));
            return Capture(() =>
            {
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModel(floor, new Vector3(0, -0.5f, 0), 1, Color.White);
                DrawModelEx(block, new Vector3(0, 1, 0), Vector3.UnitY, 0, new Vector3(1, 2, 1), Color.White);
                EndMode3D();
            }, name);
        }
        int Lit(Image image, float x)
        {
            var pixel = GetWorldToScreenEx(new Vector3(x, 0, 0), camera, 320, 180);
            var c = GetImageColor(image, (int)pixel.X, (int)pixel.Y);
            return Math.Max(c.R, Math.Max(c.G, c.B));
        }

        var plain = Drawn(shaders[0], 0, "plain");
        var read = Drawn(shaders[1], 0, "read");
        var moved = Drawn(shaders[1], 1, "moved");
        Lit(plain, 2).Should().BeLessThan(8, "the floor at 2 lies in the block's shadow");
        Lit(plain, 3.5f).Should().BeGreaterThan(100, "and at 3.5 past it");
        var differing = 0;
        for (int y = 0; y < 180; y++)
            for (int x = 0; x < 320; x++)
                if (GetImageColor(read, x, y) != GetImageColor(plain, x, y)) differing++;
        differing.Should().Be(0, "the shadow read at the point is the read litLight makes there");
        Lit(moved, 2).Should().BeGreaterThan(100, "read one unit on, the floor at 2 reads the lit floor at 3");
        Lit(moved, 1).Should().BeLessThan(8, "and the floor at 1 the shadow at 2");
        Lit(moved, 3.5f).Should().BeGreaterThan(100);
        UnloadModel(block);
        UnloadModel(floor);
        foreach (var shader in shaders) UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Low_Suns_Long_Shadow_Is_Cast_Whole_Across_The_Cascades_Seen_From_Its_Far_End()
    {
        // A trunk a unit across and six high under a crown three units wide, under a sun 10 degrees
        // up, whose shadows run along the ground to x = -34 and from x = -30 to -50, seen from the
        // far end of the shadow looking back at the tree. At so low a sun the near cascade's tile
        // reaches far along the ground toward the sun, past its own near plane, before which the
        // tree was left out and clipped, so ground in the shadow read lit. From 55 units out the
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

    [NeedsVulkanFact]
    public void A_Two_Sided_Wall_Lit_From_Its_Back_Takes_The_Sun_As_Evenly_As_One_Lit_From_Its_Front()
    {
        // Two thin two-sided walls a unit high and two across, side by side under the voxel game's
        // sun at ten in the morning, 56 degrees up, the first's normal toward the sun and the second's
        // away from it, both seen from the sun's side. With the shadow's point moved along the normal
        // as given, behind the second wall as the sun sees it, that wall shadowed itself, its light
        // 0.1909 where the first's was 0.4403 and spread 0.298 of its mean; moved to the sun's side of
        // each, both read 0.4403, even.
        var config = Config.Default.WithWindow("two-sided wall test", 320, 180) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var toSun = Vector3.Normalize(new Vector3(0.479f, 0.830f, 0.287f));
        CreateDirectionalLight(-toSun, Color.White, 2, castsShadows: true);
        var floor = LoadModelFromMesh(GenMeshPlane(30, 30, 1, 1));
        var wall = LoadModelFromMesh(GenMeshPlane(1, 1, 1, 1));
        wall.Materials[0] = new ModelMaterial(new Color(200, 180, 80)) { Roughness = 1, DoubleSided = true, AlphaMode = MaterialAlphaMode.Opaque };
        var camera = new Camera3D(new Vector3(4, 1.5f, 0), new Vector3(0, 0.5f, 0), Vector3.UnitY, 50);
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(floor, Vector3.Zero, 1, new Color(110, 72, 44));
            DrawModelEx(wall, new Vector3(0, 0.5f, -1.2f), Vector3.UnitZ, -90, new Vector3(1, 1, 2), Color.White);
            DrawModelEx(wall, new Vector3(0, 0.5f, 1.2f), Vector3.UnitZ, 90, new Vector3(1, 1, 2), Color.White);
            EndMode3D();
        }, "walls");
        // A wall's light over a grid of points on its lit face, its mean and its spread, the
        // standard deviation over the mean.
        (float Mean, float Spread) Face(float z)
        {
            var lights = new List<float>();
            for (float y = 0.15f; y <= 0.85f; y += 0.1f)
                for (float along = -0.8f; along <= 0.8f; along += 0.1f)
                {
                    var at = GetWorldToScreenEx(new Vector3(0.01f, y, z + along), camera, image.Width, image.Height);
                    var c = GetImageColor(image, (int)at.X, (int)at.Y);
                    lights.Add(0.2126f * MathF.Pow(c.R / 255f, 2.2f) + 0.7152f * MathF.Pow(c.G / 255f, 2.2f) + 0.0722f * MathF.Pow(c.B / 255f, 2.2f));
                }
            var mean = lights.Average();
            return (mean, MathF.Sqrt(lights.Average(l => (l - mean) * (l - mean))) / mean);
        }
        var front = Face(-1.2f);
        var back = Face(1.2f);
        back.Spread.Should().BeLessThan(0.05f, $"the wall lit from its back takes the sun as evenly as the one lit from its front, spread {back.Spread:0.000} against {front.Spread:0.000}");
        (back.Mean / front.Mean).Should().BeInRange(0.95f, 1.05f, $"the wall lit from its back takes as much of the sun, {back.Mean:0.0000} against {front.Mean:0.0000}");
        UnloadModel(floor);
        UnloadModel(wall);
    }
}

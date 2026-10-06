using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void A_Spot_Lights_Inside_Its_Cone_And_A_Range_Ends_A_Light()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(45f));
        ecs.Add(camera, new Transform(new Vector3(0, 0, 4)));
        var wall = ecs.Spawn();
        ecs.Add(wall, new Mesh([new(-2, -2, 0), new(2, -2, 0), new(2, 2, 0), new(-2, -2, 0), new(2, 2, 0), new(-2, 2, 0)]));
        ecs.Add(wall, new Material(Vector4.One));
        ecs.Add(wall, new Transform(Vector3.Zero));

        // A narrow spot 2 units in front of the wall, pointing at it along -Z.
        var lamp = ecs.Spawn();
        ecs.Add(lamp, Light.Spot(Vector3.One, 8f, innerAngle: 5f, outerAngle: 8f));
        ecs.Add(lamp, new Transform(new Vector3(0, 0, 2)));
        var spot = Capture(() => ClearBackground(Color.Black), "spot");

        // The same light as a point that stops 1 unit short of the wall.
        ecs.GetRef<Light>(lamp) = Light.Point(Vector3.One, 8f, range: 1f);
        var ranged = Capture(() => ClearBackground(Color.Black), "ranged");

        GetImageColor(spot, 32, 32).R.Should().BeGreaterThan(200, "the middle of the wall is inside the cone");
        GetImageColor(spot, 52, 32).R.Should().BeLessThan(10, "the wall's edge is far outside an 8 degree cone");
        GetImageColor(ranged, 32, 32).R.Should().BeLessThan(10, "the wall is past the light's range");
    }

    // A byte of a captured frame as the linear light it encodes, since the model pass lights in
    // linear space and stores sRGB.
    private static float Linear(byte value)
    {
        var c = value / 255f;
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    // A white wall facing a camera 4 units away down +Z, filling the frame.
    private static int SpawnWallAndCamera(EcsWorld ecs)
    {
        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(45f));
        ecs.Add(camera, new Transform(new Vector3(0, 0, 4)));
        var wall = ecs.Spawn();
        ecs.Add(wall, new Mesh([new(-2, -2, 0), new(2, -2, 0), new(2, 2, 0), new(-2, -2, 0), new(2, 2, 0), new(-2, 2, 0)]));
        ecs.Add(wall, new Material(Vector4.One));
        ecs.Add(wall, new Transform(Vector3.Zero));
        return wall;
    }

    [NeedsVulkanFact]
    public void A_Smooth_Surface_Mirrors_A_Light_A_Rough_One_Scatters()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        var wall = SpawnWallAndCamera(ecs);

        // Pointing straight at the wall the way the camera looks, so the middle of the wall
        // mirrors it into the camera, and its diffuse light equals an ambient light's.
        var lamp = ecs.Spawn();
        ecs.Add(lamp, Light.Directional(Vector3.One, 0.5f));
        ecs.Add(lamp, new Transform(Vector3.Zero));
        ecs.GetRef<Material>(wall).RoughnessFactor = 0.25f;
        var smooth = Capture(() => ClearBackground(Color.Black), "smooth");
        ecs.GetRef<Material>(wall).RoughnessFactor = 0.9f;
        var rough = Capture(() => ClearBackground(Color.Black), "rough");
        ecs.GetRef<Light>(lamp) = Light.Ambient(Vector3.One, 0.5f);
        var ambient = Capture(() => ClearBackground(Color.Black), "ambient");

        var diffuse = Linear(GetImageColor(ambient, 32, 32).R);
        diffuse.Should().BeInRange(0.48f, 0.58f, "half a white light on a white wall returns about half the light, stored as sRGB 188");
        Linear(GetImageColor(rough, 32, 32).R).Should().BeApproximately(diffuse, 0.06f, "a rough surface spreads its highlight too thin to see");
        Linear(GetImageColor(smooth, 32, 32).R).Should().BeGreaterThan(diffuse + 0.3f, "a smooth one gathers it where the wall mirrors the light");
    }

    [NeedsVulkanFact]
    public void A_Metal_Reflects_In_Its_Own_Color_And_Scatters_No_Diffuse_Light()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        var wall = SpawnWallAndCamera(ecs);
        ref var material = ref ecs.GetRef<Material>(wall);
        material.Albedo = new Vector4(1, 0.2f, 0.2f, 1);
        material.RoughnessFactor = 0.3f;

        // From the side, so the middle of the wall shows its diffuse light and no highlight.
        var lamp = ecs.Spawn();
        ecs.Add(lamp, Light.Directional(Vector3.One, 1f));
        ecs.Add(lamp, new Transform(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 3), Vector3.One));
        var plastic = Capture(() => ClearBackground(Color.Black), "plastic");
        ecs.GetRef<Material>(wall).MetallicFactor = 1;
        var metal = Capture(() => ClearBackground(Color.Black), "metal");

        Linear(GetImageColor(plastic, 32, 32).R).Should().BeGreaterThan(0.3f, "red plastic scatters the light that falls on it");
        Linear(GetImageColor(metal, 32, 32).R).Should().BeLessThan(0.05f, "a metal scatters none, and this light is not mirrored toward the camera");
    }

    [NeedsVulkanFact]
    public void A_Normal_Map_Turns_A_Flat_Surface_Toward_A_Light()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 4, 0), Vector3.Zero, -Vector3.UnitZ, 45);
        var ecs = GetApp().World.Resource<EcsWorld>();
        var sun = ecs.Spawn();
        // From +X, 20 degrees over the plane, so a surface tilted toward +X faces it. The light's
        // -Z turned a quarter about Y points down -X, and then 20 degrees about Z points it down.
        ecs.Add(sun, Light.Directional(Vector3.One, 1f));
        ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.Concatenate(
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.35f)), Vector3.One));

        // A plane facing up whose texture coordinates run along X, and a normal map leaning every
        // normal 45 degrees toward +u.
        var plane = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        plane.Materials[0] = plane.Materials[0] with { Roughness = 1 };
        var flat = Capture(Draw, "flat");
        var leaning = LoadTextureFromImage(GenImageColor(4, 4, new Color(218, 128, 218)));
        plane.Materials[0] = plane.Materials[0] with { NormalMap = leaning };
        var mapped = Capture(Draw, "mapped");

        ((int)GetImageColor(mapped, 32, 32).R).Should().BeGreaterThan(GetImageColor(flat, 32, 32).R + 40, "the map turns the surface toward the low light");

        // Up in the map is toward the top of the image, falling v, which on this plane is -Z. A
        // map leaning that way faces a sun low in -Z and turns from one low in +Z.
        var up = LoadTextureFromImage(GenImageColor(4, 4, new Color(128, 218, 218)));
        plane.Materials[0] = plane.Materials[0] with { NormalMap = up };
        ecs.GetRef<Transform>(sun).Rotation = Quaternion.Concatenate(
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI), Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.35f));
        var facing = Capture(Draw, "facing");
        ecs.GetRef<Transform>(sun).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.35f);
        var away = Capture(Draw, "away");
        ((int)GetImageColor(facing, 32, 32).R).Should().BeGreaterThan(GetImageColor(away, 32, 32).R + 40, "the map's up leans toward -Z");

        UnloadModel(plane);
        UnloadTexture(leaning);
        UnloadTexture(up);

        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(plane, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }
    }

    [NeedsVulkanFact]
    public void Light_Summed_Past_One_Keeps_Its_Hue_Instead_Of_Clamping_To_White()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        SpawnWallAndCamera(ecs);

        // An orange light four times over: (4, 2, 1), which a plain clamp turns white.
        var lamp = ecs.Spawn();
        ecs.Add(lamp, Light.Ambient(new Vector3(1f, 0.5f, 0.25f), 4f));
        var image = Capture(() => ClearBackground(Color.Black), "orange");

        var color = GetImageColor(image, 32, 32);
        color.R.Should().BeGreaterThan(245, "the brightest channel comes close to full");
        Linear(color.G).Should().BeApproximately(0.5f * Linear(color.R), 0.04f, "green keeps half of red's light");
        Linear(color.B).Should().BeApproximately(0.25f * Linear(color.R), 0.03f, "blue keeps a quarter, so the light reads as the same orange");
    }

    [NeedsVulkanFact]
    public void A_Directional_Light_Casts_A_Shadow_Only_When_Asked()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        SpawnWallAndCamera(ecs);

        // A square a unit in front of the wall, right of the middle, and a sun pointing down -X and
        // -Z at 45 degrees, which throws the square's shadow a unit to its left on the wall. That
        // puts it over the wall's middle, where the square itself does not hide it from the camera.
        var square = ecs.Spawn();
        ecs.Add(square, new Mesh([new(0.25f, -0.5f, 1), new(1.25f, -0.5f, 1), new(1.25f, 0.5f, 1), new(0.25f, -0.5f, 1), new(1.25f, 0.5f, 1), new(0.25f, 0.5f, 1)]));
        ecs.Add(square, new Material(Vector4.One));
        ecs.Add(square, new Transform(Vector3.Zero));

        var sun = ecs.Spawn();
        ecs.Add(sun, Light.Directional(Vector3.One, 1f) with { CastsShadows = true });
        ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4), Vector3.One));
        var shadowed = Capture(() => ClearBackground(Color.Black), "shadowed");

        ecs.GetRef<Light>(sun).CastsShadows = false;
        var unshadowed = Capture(() => ClearBackground(Color.Black), "unshadowed");

        // Pixel 27 across is the wall a quarter unit left of the middle, in the shadow. Pixel 9
        // down from the top is the wall above the square's reach.
        GetImageColor(shadowed, 27, 32).R.Should().BeLessThan(10, "the square stands between the sun and this part of the wall");
        GetImageColor(shadowed, 27, 9).R.Should().BeGreaterThan(120, "the wall above the square's shadow is lit");
        GetImageColor(unshadowed, 27, 32).R.Should().BeGreaterThan(120, "a light that does not cast shadows lights the wall behind the square");
    }

    [NeedsVulkanFact]
    public void A_Shadow_Falls_The_Same_With_A_Smaller_Or_Larger_Map_Made_Between_Frames()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        SpawnWallAndCamera(ecs);
        var square = ecs.Spawn();
        ecs.Add(square, new Mesh([new(0.25f, -0.5f, 1), new(1.25f, -0.5f, 1), new(1.25f, 0.5f, 1), new(0.25f, -0.5f, 1), new(1.25f, 0.5f, 1), new(0.25f, 0.5f, 1)]));
        ecs.Add(square, new Material(Vector4.One));
        ecs.Add(square, new Transform(Vector3.Zero));
        var sun = ecs.Spawn();
        ecs.Add(sun, Light.Directional(Vector3.One, 1f) with { CastsShadows = true });
        ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4), Vector3.One));

        foreach (var size in new[] { 512, 4096, 1000 })
        {
            SetShadowMapSize(size);
            var image = Capture(() => ClearBackground(Color.Black), $"map{size}");
            GetImageColor(image, 27, 32).R.Should().BeLessThan(10, $"the square shadows the wall with tiles {size} wide");
            GetImageColor(image, 27, 9).R.Should().BeGreaterThan(120);
        }
        GetApp().World.Resource<ShadowSettings>().TileSize.Should().Be(1024, "a size is rounded up to a power of two");
    }

    [NeedsVulkanFact]
    public void A_Shadow_Fades_Out_Toward_The_Shadow_Distance_Rather_Than_Stopping_At_A_Line()
    {
        Open(64, 256);
        // A long wall left of the ground the camera looks down along, under a low sun from the
        // left, so its shadow covers that ground as far as shadows are drawn.
        CreateDirectionalLight(Vector3.Normalize(new Vector3(1, -0.35f, 0)), Color.White, 1, castsShadows: true);
        var ground = LoadModelFromMesh(GenMeshPlane(800, 800, 1, 1));
        var wall = LoadModelFromMesh(GenMeshCube(2, 80, 800));
        var camera = new Camera3D(new Vector3(20, 30, 0), new Vector3(20, 0, -200), Vector3.UnitY, 40);

        var image = Capture(() =>
        {
            ClearBackground(Color.Blue);
            BeginMode3D(camera);
            DrawModel(ground, new Vector3(0, 0, -390), 1, Color.White);
            DrawModel(wall, new Vector3(-10, 40, -390), 1, Color.White);
            EndMode3D();
        }, "fade");

        // Up the middle column from the camera's feet toward the horizon, the ground goes from
        // shadowed to lit where shadows end.
        // The ground is gray, and the sky above the horizon blue.
        var column = Enumerable.Range(0, 256).Select(y => GetImageColor(image, 32, 255 - y)).ToArray();
        var ground0 = column.TakeWhile(c => c.B <= c.G + 8).Select(c => (int)c.G).ToArray();
        var (dark, lit) = (ground0[..10].Average(), ground0[^10..].Average());
        lit.Should().BeGreaterThan(dark + 40, "the ground near the camera is shadowed and the far ground is lit");
        var between = ground0.Count(g => g > dark + (lit - dark) * 0.2 && g < lit - (lit - dark) * 0.2);
        between.Should().BeGreaterThan(2, "the shadow fades over a band of rows, where it used to step from dark to lit in one");
        UnloadModel(ground);
        UnloadModel(wall);
    }

    [NeedsVulkanFact]
    public void A_Scene_Drawn_Only_Into_Render_Textures_Casts_Shadows_Fitted_To_Each_Camera()
    {
        Open(64, 32);
        // Two views of two cubes on ground a hundred units apart, as a split screen draws each
        // player's view into a texture of its own, with nothing drawn into the window in 3D. A
        // sun from the left at 45 degrees throws each lifted cube's shadow two to four units to
        // its right, past where the cube itself shows from above.
        CreateDirectionalLight(Vector3.Normalize(new Vector3(1, -1, 0)), Color.White, 1, castsShadows: true);
        var ground = LoadModelFromMesh(GenMeshPlane(400, 400, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(2, 2, 2));
        var left = LoadRenderTexture(32, 32);
        var right = LoadRenderTexture(32, 32);
        var views = new[] { (left, 0f), (right, 100f) };

        var image = Capture(() =>
        {
            foreach (var (target, x) in views)
            {
                BeginTextureMode(target);
                ClearBackground(Color.Blue);
                // Straight down, with -Z up the view, so +X is to the right.
                BeginMode3D(new Camera3D(new Vector3(x, 12, 0), new Vector3(x, 0, 0), -Vector3.UnitZ, 30));
                DrawModel(ground, Vector3.Zero, 1, Color.White);
                DrawModel(cube, new Vector3(x, 3, 0), 1, Color.Red);
                EndMode3D();
                EndTextureMode();
            }
            ClearBackground(Color.Black);
            DrawTextureRec(left.Texture, new Rectangle(0, 0, 32, -32), Vector2.Zero, Color.White);
            DrawTextureRec(right.Texture, new Rectangle(0, 0, 32, -32), new Vector2(32, 0), Color.White);
        }, "split");

        // About five pixels a unit on the ground: pixel 28 is two and a half units right of the
        // cube, in its shadow, and pixel 3 as far left of it, in the sun.
        foreach (var x0 in new[] { 0, 32 })
        {
            var shadowed = GetImageColor(image, x0 + 28, 16);
            var lit = GetImageColor(image, x0 + 3, 16);
            ((int)lit.G).Should().BeGreaterThan(shadowed.G + 40, $"the view at {x0} shadows its own ground, which its own camera's cascades reach");
        }
        GraphicsDevice.ValidationErrors.Count.Should().Be(_validationErrorsBefore);
        UnloadModel(ground);
        UnloadModel(cube);
        UnloadRenderTexture(left);
        UnloadRenderTexture(right);
    }

    [NeedsVulkanFact]
    public void A_Point_Light_Casts_Shadows_On_Every_Side()
    {
        Open(64, 64);
        // A low light in the middle of the ground, a cube to its +X and one to its -Z, seen from
        // straight above with -Z up the image.
        var lamp = CreatePointLight(new Vector3(0, 1.5f, 0), Color.White, 30, castsShadows: true);
        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 15, 0), Vector3.Zero, -Vector3.UnitZ, 45);
        Image Draw(string name) => Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, Color.White);
            DrawModel(cube, new Vector3(2, 0.5f, 0), 1, Color.White);
            DrawModel(cube, new Vector3(0, 0.5f, -2), 1, Color.White);
            EndMode3D();
        }, name);

        var shadowed = Draw("point shadows");
        SetLightCastsShadows(lamp, false);
        var open = Draw("point open");

        // The ground 3.5 units out each way, at (px, py) for x and z of ±3.5.
        int behindX = GetImageColor(shadowed, 50, 32).R, clearX = GetImageColor(shadowed, 14, 32).R;
        int behindZ = GetImageColor(shadowed, 32, 14).R, clearZ = GetImageColor(shadowed, 32, 50).R;
        behindX.Should().BeLessThan(clearX / 3, $"the cube at +X shadows the ground past it ({behindX} against {clearX})");
        // (3.5, 0, 2) is on the +X face too, past the cube's edge, so that face lights it.
        GetImageColor(shadowed, 50, 42).R.Should().BeGreaterThan((byte)(clearX / 2), "the +X face lights the ground the cube does not hide");
        behindZ.Should().BeLessThan(clearZ / 3, $"the cube at -Z shadows the ground past it ({behindZ} against {clearZ})");
        ((int)GetImageColor(open, 50, 32).R).Should().BeGreaterThan(clearX * 8 / 10, "without shadows the light reaches past the cube");
        UnloadModel(ground);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void Of_More_Shadowed_Point_Lights_Than_There_Is_Room_For_The_Ones_Near_The_Camera_Cast()
    {
        Open(64, 64);
        // Four lamps far off, made first, and a fifth over the scene the camera looks at, which a
        // limit taken in the order lights were made would leave without a shadow.
        for (int i = 0; i < 4; i++) CreatePointLight(new Vector3(80 + 10 * i, 1.5f, 80), Color.Red, 30, 5, castsShadows: true);
        CreatePointLight(new Vector3(0, 1.5f, 0), Color.White, 30, castsShadows: true);
        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 15, 0), Vector3.Zero, -Vector3.UnitZ, 45);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, Color.White);
            DrawModel(cube, new Vector3(2, 0.5f, 0), 1, Color.White);
            EndMode3D();
        }, "near lamp");

        // As in the test above, the ground 3.5 units out along +X, behind the cube, and along -X.
        int behind = GetImageColor(image, 50, 32).G, clear = GetImageColor(image, 14, 32).G;
        behind.Should().BeLessThan(clear / 3, $"the lamp near the camera shadows the ground past the cube ({behind} against {clear})");
        UnloadModel(ground);
        UnloadModel(cube);
    }
}

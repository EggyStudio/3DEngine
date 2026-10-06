using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void A_Mirror_In_A_Probes_Box_Reflects_The_Room_Rather_Than_The_Sky()
    {
        Open(64, 64);
        // A blue sky outside, and a red room around a mirror ball, lit by a lamp inside. The ball
        // sits below the probe's middle, which a probe inside it would see only the ball from.
        SetEnvironmentMap(GenImageColor(64, 32, new Color(40, 90, 255)));
        CreatePointLight(new Vector3(0, 2, 2), Color.White, 20);
        var room = LoadModelFromMesh(GenMeshCube(8, 6, 8));
        room.Materials[0].DoubleSided = true; // seen from inside, where a single-sided cube shows nothing
        var ball = LoadModelFromMesh(GenMeshSphere(1, 32, 32));
        ball.Materials[0] = new ModelMaterial(Color.White) { Metallic = 1, Roughness = 0.05f };
        var camera = new Camera3D(new Vector3(0, -1.5f, 3), new Vector3(0, -1.5f, 0), Vector3.UnitY, 60);
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(room, Vector3.Zero, 1, new Color(220, 40, 40));
            DrawModel(ball, new Vector3(0, -1.5f, 0), 1, Color.White);
            EndMode3D();
        }

        var sky = Capture(Draw, "sky in the ball");
        var probe = CreateReflectionProbe(Vector3.Zero, new Vector3(8, 6, 8));
        for (int frame = 0; frame < 120 && !IsReflectionProbeReady(probe); frame++)
        {
            BeginDrawing();
            Draw();
            EndDrawing();
        }
        IsReflectionProbeReady(probe).Should().BeTrue("the probe is captured and filtered within a few frames");
        var room0 = Capture(Draw, "room in the ball");

        // The middle of the ball, which mirrors what is behind the camera.
        var before = GetImageColor(sky, 32, 32);
        var after = GetImageColor(room0, 32, 32);
        ((int)before.B).Should().BeGreaterThan(before.R, $"without a probe the ball mirrors the blue sky, not {before}");
        ((int)after.R).Should().BeGreaterThan(after.B + 30, $"with one it mirrors the red room, not {after}");
        UnloadReflectionProbe(probe);
        UnloadModel(room);
        UnloadModel(ball);
    }

    [NeedsVulkanFact]
    public void A_Probe_Captures_Again_When_A_Lamp_In_Its_Room_Goes_Out_And_Not_When_It_Flickers()
    {
        Open(64, 64);
        // The red room around a mirror ball, lit by a lamp inside it, and a dim sun, so the scene
        // has a light left when the lamp goes out rather than being drawn unlit with none.
        var lamp = CreatePointLight(new Vector3(0, 2, 2), Color.White, 20);
        CreateDirectionalLight(-Vector3.UnitY, Color.White, 0.05f);
        var room = LoadModelFromMesh(GenMeshCube(8, 6, 8));
        room.Materials[0].DoubleSided = true; // seen from inside, where a single-sided cube shows nothing
        var ball = LoadModelFromMesh(GenMeshSphere(1, 32, 32));
        ball.Materials[0] = new ModelMaterial(Color.White) { Metallic = 1, Roughness = 0.05f };
        var camera = new Camera3D(new Vector3(0, -1.5f, 3), new Vector3(0, -1.5f, 0), Vector3.UnitY, 60);
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(room, Vector3.Zero, 1, new Color(220, 40, 40));
            DrawModel(ball, new Vector3(0, -1.5f, 0), 1, Color.White);
            EndMode3D();
        }
        void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                Draw();
                EndDrawing();
            }
        }

        var probe = CreateReflectionProbe(Vector3.Zero, new Vector3(8, 6, 8));
        for (int frame = 0; frame < 120 && !IsReflectionProbeReady(probe); frame++) Frames(1);
        Frames(30);
        var lit = GetImageColor(Capture(Draw, "the lamp on"), 32, 32);
        var probes = GetApp().World.Resource<ReflectionProbes>().ByEntity.Values.Single();

        // A tenth dimmer is within a flicker.
        SetLightColor(lamp, Color.White, 18);
        Frames(10);
        probes.Relit.Should().Be(0, "a lamp a tenth dimmer is within a flicker");

        SetLightColor(lamp, Color.White, 0);
        Frames(3);
        probes.Relit.Should().Be(1, "the lamp going out asks for the probe once");
        IsReflectionProbeReady(probe).Should().BeFalse("its capture is of the lamp on");
        for (int frame = 0; frame < 240 && !IsReflectionProbeReady(probe); frame++) Frames(1);
        IsReflectionProbeReady(probe).Should().BeTrue("the probe is captured again with the lamp out, both its passes");
        probes.Captured.Should().Be(probes.Wanted);
        probes.Passes.Should().BeGreaterThanOrEqualTo(ReflectionProbes.Passes);
        var dark = GetImageColor(Capture(Draw, "the lamp out"), 32, 32);
        ((int)dark.R).Should().BeLessThan(lit.R - 60, $"the ball no longer mirrors a lit room, {lit} before and {dark} after");
        UnloadReflectionProbe(probe);
        UnloadModel(room);
        UnloadModel(ball);
    }

    [NeedsVulkanFact]
    public void A_Probe_Captures_A_Room_Drawn_Only_Into_A_Render_Texture()
    {
        Open(64, 64);
        CreatePointLight(new Vector3(0, 1, 0), Color.White, 6);
        var room = LoadModelFromMesh(GenMeshCube(6, 6, 6));
        room.Materials[0] = new ModelMaterial(new Color(220, 40, 40)) { DoubleSided = true };
        var target = LoadRenderTexture(32, 32);
        var camera = new Camera3D(new Vector3(0, 0, 2), Vector3.Zero, Vector3.UnitY, 60);
        var probe = CreateReflectionProbe(Vector3.Zero, new Vector3(6, 6, 6));
        for (int frame = 0; frame < 120 && !IsReflectionProbeReady(probe); frame++)
        {
            BeginDrawing();
            BeginTextureMode(target);
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(room, Vector3.Zero, 1, Color.White);
            EndMode3D();
            EndTextureMode();
            ClearBackground(Color.Black);
            EndDrawing();
        }
        IsReflectionProbeReady(probe).Should().BeTrue("a probe captures the meshes a render texture draws when the window draws none");

        var (texels, size) = ProbeFaces();
        var texel = (size * size / 2 + size / 2) * 4;
        ((float)texels[texel]).Should().BeGreaterThan(2 * (float)texels[texel + 2], "the capture holds the red room");
        UnloadReflectionProbe(probe);
        UnloadRenderTexture(target);
        UnloadModel(room);
    }

    [NeedsVulkanFact]
    public void A_Probe_Keeps_Light_Many_Times_Brighter_Than_A_Frame_Shows()
    {
        Open(64, 64);
        // A dark room whose wall at +X gives off twelve times white light, far past what a byte
        // after the tonemap holds.
        CreatePointLight(new Vector3(0, 0, 0), Color.White, 0.1f);
        var wall = LoadModelFromMesh(GenMeshCube(0.2f, 6, 6));
        wall.Materials[0] = new ModelMaterial(Color.Black) { Emissive = Color.White, EmissiveIntensity = 12 };
        var room = LoadModelFromMesh(GenMeshCube(6, 6, 6));
        var camera = new Camera3D(new Vector3(0, 0, 2), new Vector3(1, 0, 0), Vector3.UnitY, 60);
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(room, Vector3.Zero, 1, new Color(30, 30, 30));
            DrawModel(wall, new Vector3(2.8f, 0, 0), 1, Color.White);
            EndMode3D();
        }

        var probe = CreateReflectionProbe(Vector3.Zero, new Vector3(6, 6, 6));
        for (int frame = 0; frame < 120 && !IsReflectionProbeReady(probe); frame++)
        {
            BeginDrawing();
            Draw();
            EndDrawing();
        }
        IsReflectionProbeReady(probe).Should().BeTrue();

        // The first mip's +X face, a mirror's view of the glowing wall.
        var (texels, size) = ProbeFaces();
        var bright = (float)texels[(size * size / 2 + size / 2) * 4];
        bright.Should().BeGreaterThan(8, "a capture in half floats holds the light as it was drawn, where eight bits at a quarter exposure held 6.4 at most");
        UnloadReflectionProbe(probe);
        UnloadModel(wall);
        UnloadModel(room);
    }

    [NeedsVulkanFact]
    public void A_Probe_Refreshed_Sees_Its_Room_Change_Where_One_Captured_On_A_Change_Does_Not()
    {
        Open(64, 64);
        var wall = LoadModelFromMesh(GenMeshCube(0.2f, 6, 6));
        var room = LoadModelFromMesh(GenMeshCube(6, 6, 6));
        var camera = new Camera3D(new Vector3(0, 0, 2), new Vector3(1, 0, 0), Vector3.UnitY, 60);
        void Glow(Color color) => wall.Materials[0] = new ModelMaterial(Color.Black) { Emissive = color, EmissiveIntensity = 4 };
        void Draw(int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModel(room, Vector3.Zero, 1, new Color(30, 30, 30));
                DrawModel(wall, new Vector3(2.8f, 0, 0), 1, Color.White);
                EndMode3D();
                EndDrawing();
            }
        }
        // The middle of the first mip's +X face, a mirror's view of the wall.
        Vector3 Wall()
        {
            var (texels, size) = ProbeFaces();
            var at = (size * size / 2 + size / 2) * 4;
            return new Vector3((float)texels[at], (float)texels[at + 1], (float)texels[at + 2]);
        }

        Glow(Color.Red);
        var probe = CreateReflectionProbe(Vector3.Zero, new Vector3(6, 6, 6));
        for (int frame = 0; frame < 120 && !IsReflectionProbeReady(probe); frame++) Draw(1);
        IsReflectionProbeReady(probe).Should().BeTrue();
        Wall().X.Should().BeGreaterThan(Wall().Z, "the wall glows red");

        Glow(Color.Blue);
        Draw(30);
        Wall().X.Should().BeGreaterThan(Wall().Z, "a material changing asks for no capture");

        SetReflectionProbeRefresh(probe, 0.0001f);
        for (int frame = 0; frame < 60 && Wall().X >= Wall().Z; frame++)
        {
            Draw(1);
            IsReflectionProbeReady(probe).Should().BeTrue("a probe ready for its placement stays ready while it is refreshed");
        }
        Wall().Z.Should().BeGreaterThan(Wall().X, "the refresh captured the wall as it glows now");
        UnloadReflectionProbe(probe);
        UnloadModel(wall);
        UnloadModel(room);
    }

    // The one probe's first mip as the GPU filtered it, its six faces' RGBA half floats, and the width of a face.
    private (Half[] Texels, int Size) ProbeFaces()
    {
        var map = GetApp().World.Resource<ReflectionProbes>().ByEntity.Values.Single().Map!;
        var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
        return (device.ReadCubeFaces(map), (int)map.Size);
    }

    [NeedsVulkanFact]
    public void A_Shadow_Eighty_Units_Away_Falls_In_A_Far_Cascade()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();

        // The scene of the test above twenty times over, seen from eighty units, past the forty
        // the single map reached.
        var big = new Vector3(20);
        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(45f));
        ecs.Add(camera, new Transform(new Vector3(0, 0, 80)));
        var wall = ecs.Spawn();
        ecs.Add(wall, new Mesh([new(-2, -2, 0), new(2, -2, 0), new(2, 2, 0), new(-2, -2, 0), new(2, 2, 0), new(-2, 2, 0)]));
        ecs.Add(wall, new Material(Vector4.One));
        ecs.Add(wall, new Transform(Vector3.Zero, Quaternion.Identity, big));
        var square = ecs.Spawn();
        ecs.Add(square, new Mesh([new(0.25f, -0.5f, 1), new(1.25f, -0.5f, 1), new(1.25f, 0.5f, 1), new(0.25f, -0.5f, 1), new(1.25f, 0.5f, 1), new(0.25f, 0.5f, 1)]));
        ecs.Add(square, new Material(Vector4.One));
        ecs.Add(square, new Transform(Vector3.Zero, Quaternion.Identity, big));
        var sun = ecs.Spawn();
        ecs.Add(sun, Light.Directional(Vector3.One, 1f) with { CastsShadows = true });
        ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4), Vector3.One));

        var image = Capture(() => ClearBackground(Color.Black), "far");

        GetImageColor(image, 27, 32).R.Should().BeLessThan(10, "the square shadows the wall eighty units from the camera");
        GetImageColor(image, 27, 9).R.Should().BeGreaterThan(120, "the wall above the shadow is lit");

        SetShadowDistance(40);
        var near = Capture(() => ClearBackground(Color.Black), "near");
        GetImageColor(near, 27, 32).R.Should().BeGreaterThan(120, "with shadows reaching forty units, the wall at eighty has none");
    }

    [NeedsVulkanFact]
    public void A_Spot_Light_Casts_A_Shadow_In_Its_Tile()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        SpawnWallAndCamera(ecs);

        // A spot at (-2, 0, 2) aimed at the wall's middle, and a strip a unit in front of the wall
        // from x -1 to -0.5, whose shadow falls from x 0 to 1 on the wall, right of the middle,
        // where the strip itself does not hide it from the camera.
        var strip = ecs.Spawn();
        ecs.Add(strip, new Mesh([new(-1, -0.5f, 1), new(-0.5f, -0.5f, 1), new(-0.5f, 0.5f, 1), new(-1, -0.5f, 1), new(-0.5f, 0.5f, 1), new(-1, 0.5f, 1)]));
        ecs.Add(strip, new Material(Vector4.One));
        ecs.Add(strip, new Transform(Vector3.Zero));
        var spot = ecs.Spawn();
        ecs.Add(spot, Light.Spot(Vector3.One, 30f, 35, 45) with { CastsShadows = true });
        ecs.Add(spot, new Transform(new Vector3(-2, 0, 2), Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 4), Vector3.One));
        var shadowed = Capture(() => ClearBackground(Color.Black), "spot");

        ecs.GetRef<Light>(spot).CastsShadows = false;
        var unshadowed = Capture(() => ClearBackground(Color.Black), "spot unshadowed");

        // Pixel 41 across is the wall half a unit right of the middle, in the shadow, and pixel 26
        // the wall a little left of it, lit and clear of the strip.
        var lit = GetImageColor(shadowed, 26, 32).R;
        lit.Should().BeGreaterThan(60, "the spot lights the wall beside the strip");
        GetImageColor(shadowed, 41, 32).R.Should().BeLessThan((byte)(lit / 4), "the strip stands between the spot and this part of the wall");
        GetImageColor(unshadowed, 41, 32).R.Should().BeGreaterThan(60, "a spot that does not cast shadows lights it");
    }

    [NeedsVulkanFact]
    public void Two_Spot_Lights_Cast_Shadows_At_Once()
    {
        Open(64, 64);
        // A red spot left and a blue one right, each low and aimed past a cube toward the middle,
        // so each cube's shadow falls on the ground between it and the middle, which only the other
        // spot then lights. Seen from straight above, with -Z up the image.
        var red = CreateSpotLight(new Vector3(-4, 2, 0), Vector3.Normalize(new Vector3(3, -2, 0)), new Color(255, 0, 0), 40, 30, 35, castsShadows: true);
        var blue = CreateSpotLight(new Vector3(4, 2, 0), Vector3.Normalize(new Vector3(-3, -2, 0)), new Color(0, 0, 255), 40, 30, 35, castsShadows: true);
        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 12, 0), Vector3.Zero, -Vector3.UnitZ, 45);
        Image Draw(string name) => Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, Color.White);
            DrawModel(cube, new Vector3(-2.5f, 0.5f, 0), 1, Color.White);
            DrawModel(cube, new Vector3(2.5f, 0.5f, 0), 1, Color.White);
            EndMode3D();
        }, name);

        var both = Draw("two spots");
        SetLightCastsShadows(red, false);
        SetLightCastsShadows(blue, false);
        var none = Draw("two spots unshadowed");

        // The ground a unit left of the middle is in the red cube's shadow, and a unit right in the blue one's.
        var left = GetImageColor(both, 25, 32);
        var right = GetImageColor(both, 38, 32);
        left.R.Should().BeLessThan((byte)(GetImageColor(none, 25, 32).R / 4), $"the red cube shadows the ground left of the middle, {left}");
        left.B.Should().BeGreaterThan(40, "which the blue spot still lights");
        right.B.Should().BeLessThan((byte)(GetImageColor(none, 38, 32).B / 4), $"the blue cube shadows the ground right of it, {right}");
        right.R.Should().BeGreaterThan(40, "which the red spot still lights");
        UnloadModel(ground);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_Masked_Surface_Casts_The_Shadow_Of_Its_Cut_Out()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 4), Vector3.Zero, Vector3.UnitY, 45);
        var wall = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        // A unit square a unit in front of the wall, from x 0.25 to 1.25, cut out on its left half
        // and solid on its right. The sun along -X and -Z throws its shadow a unit to the left.
        var square = LoadModelFromMesh(GenMeshPlane(1, 1, 1, 1));
        var texture = LoadTextureFromImage(new Image([255, 255, 255, 40, 255, 255, 255, 230], 2, 1));
        SetTextureFilter(texture, TextureFilter.Point);
        square.Materials[0] = new ModelMaterial(Color.White, texture) { AlphaMode = MaterialAlphaMode.Mask };
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, 0, -1)), Color.White, 1, castsShadows: true);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(wall, Vector3.Zero, Vector3.UnitX, 90, Vector3.One, Color.White);
            DrawModelEx(square, new Vector3(0.75f, 0, 1), Vector3.UnitX, 90, Vector3.One, Color.White);
            EndMode3D();
        });

        // The wall half a unit left of the middle is behind the cut-out half, and the middle behind the solid one.
        var hole = GetImageColor(image, 22, 32).R;
        var solid = GetImageColor(image, 32, 32).R;
        hole.Should().BeGreaterThan(60, "light passes through the cut-out half");
        solid.Should().BeLessThan((byte)(hole / 3), "the solid half shadows the wall");
        UnloadModel(square);
        UnloadModel(wall);
    }

    [NeedsVulkanFact]
    public void A_Half_Clear_Surface_Casts_A_Shadow_Between_None_And_A_Solid_Ones()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 4), Vector3.Zero, Vector3.UnitY, 45);
        var wall = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        // A unit square a unit in front of the wall, which the sun along -X and -Z throws a unit
        // to the left, drawn solid and then half clear.
        var square = LoadModelFromMesh(GenMeshPlane(1, 1, 1, 1));
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, 0, -1)), Color.White, 1, castsShadows: true);
        byte Shadow(Color tint)
        {
            var image = Capture(() =>
            {
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModelEx(wall, Vector3.Zero, Vector3.UnitX, 90, Vector3.One, Color.White);
                DrawModelEx(square, new Vector3(0.75f, 0, 1), Vector3.UnitX, 90, Vector3.One, tint);
                EndMode3D();
            });
            // The wall at the middle, where the square's shadow falls, left of where the square is drawn.
            return GetImageColor(image, 22, 32).R;
        }

        var solid = Shadow(Color.White);
        var half = Shadow(new Color(255, 255, 255, 128));
        var none = Shadow(new Color(255, 255, 255, 0));
        solid.Should().BeLessThan((byte)(none / 3), "a solid square shadows the wall");
        half.Should().BeGreaterThan((byte)(solid + 15), "a half clear square lets some of the light through");
        half.Should().BeLessThan((byte)(none - 15), "and holds some of it back");
        UnloadModel(square);
        UnloadModel(wall);
    }

    // CI installs the layer and sets E3D_REQUIRE_VALIDATION, so a missing layer there fails here
    // instead of letting every frame pass unchecked.
    [NeedsVulkanFact]
    public void The_Validation_Layer_Runs_Where_The_Build_Requires_It()
    {
        Open(16, 16);
        Capture(() => ClearBackground(Color.Black));
        if (Environment.GetEnvironmentVariable("E3D_REQUIRE_VALIDATION") == "1")
            GraphicsDevice.ValidationActive.Should().BeTrue("E3D_REQUIRE_VALIDATION is set, and a Debug build enables the layer when it is installed");
    }
}

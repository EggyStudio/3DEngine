using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// Global illumination with the screen's probes left out, at Low or for debugging, the cascades'
// light read alone, and at Low the glow lights read from probes standing for them alone.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Low_Leaves_The_Screens_Probes_Out_And_Their_Debug_Toggle_Leaves_Them_Out_At_Any_Quality()
    {
        Open();
        CreatePointLight(new Vector3(0.5f, 2.6f, 0.8f), Color.White, 6, range: 10);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        void Draw()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(-1.6f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 6), new Color(220, 20, 20));
            DrawModelEx(slab, new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 30, new Vector3(1, 1.5f, 1), Color.White);
        }
        // The screen's probes made at Medium, then left out there through the debugging toggle,
        // then at Low, which leaves them out, the cascades read alone, then no global illumination.
        // With no glow light Low makes no probe at all.
        SetGlobalIllumination(GlobalIllumination.Medium);
        var gi = GetApp().World.Resource<GlobalIlluminationSettings>();
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        Capture(Draw);
        renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Screen.Should().NotBeNull("Medium stands probes on the window's surfaces");
        gi.ScreenOff = true;
        Capture(Draw);
        renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Screen.Should().BeNull("the toggle leaves them neither made nor traced at any quality");
        gi.ScreenOff = false;
        SetGlobalIllumination(GlobalIllumination.Low);
        var left = Capture(Draw);
        renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Screen.Should().BeNull("Low leaves the screen's probes out");
        SceneFieldCommands.IlluminationState(GetApp().World).Should().Contain("no screen probes at Low, the cascades' light read alone");
        SetGlobalIllumination(GlobalIllumination.Off);
        var still = Capture(Draw);

        var lit = Mean(left, 58, 48, 6, 10);
        var dark = Mean(still, 58, 48, 6, 10);
        (lit.X - dark.X).Should().BeGreaterThan(20, $"the cascades bring the wall's light to the block's side, {lit} against {dark}");
        UnloadModel(slab);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void At_Low_A_Glow_Light_Is_Read_From_Probes_Standing_For_It_Alone()
    {
        // The strip's room, the strip carried as a glow light. At Low, which leaves the screen's
        // probes out, probes stand for the glow lights alone and a pixel reads the texels around it,
        // which gives the floor the light a pixel marching to every light itself gives it, the
        // probes left out whole through the debugging toggle, and with no glow light none are made.
        Open();
        SetGlobalIllumination(GlobalIllumination.Low);
        // The two reads' light compared as they give it, with no knee bending the floor's brightest.
        SetHighlightKnee(0.8f, 0);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var strip = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        strip.Materials[0].Emissive = new Color(255, 230, 190);
        strip.Materials[0].EmissiveIntensity = 30;
        var camera = new Camera3D(new Vector3(0, 1.4f, 2.3f), new Vector3(0, 1.2f, -2.5f), Vector3.UnitY, 65);
        void Room()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(5.6f, 0.3f, 5.6f), Color.White);
            DrawModelEx(slab, new Vector3(0, 3.15f, 0), Vector3.UnitY, 0, new Vector3(5.6f, 0.3f, 5.6f), Color.White);
            DrawModelEx(slab, new Vector3(0, 1.5f, -2.65f), Vector3.UnitY, 0, new Vector3(5.6f, 3, 0.3f), Color.White);
            DrawModelEx(slab, new Vector3(-2.65f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 5.6f), Color.White);
            DrawModelEx(slab, new Vector3(2.65f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 5.6f), Color.White);
            DrawModelEx(strip, new Vector3(0, 1.5f, -2.45f), Vector3.UnitY, 0, new Vector3(2, 0.06f, 0.06f), Color.White);
        }
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        var renderer = GetApp().World.Resource<Engine.Renderer>();

        settings.ScreenOff = true;
        var marched = Capture(Room, camera);
        var gi = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!;
        gi.Screen.Should().BeNull("the toggle leaves even the probes standing for the glow lights out");
        settings.ScreenOff = false;
        var read = Capture(Room, camera);
        gi.GlowOnly(renderer.RenderWorld).Should().BeTrue("the strip is carried as a glow light");
        gi.Screen.Should().NotBeNull("probes stand for the glow lights alone");
        SceneFieldCommands.IlluminationState(GetApp().World).Should().Contain("standing for the glow lights alone");

        var (byMarching, byReading) = (Mean(marched, 0, 60, 160, 36), Mean(read, 0, 60, 160, 36));
        byReading.X.Should().BeGreaterThan(40, $"the strip lights the floor, {byReading}");
        Vector3.Distance(byMarching, byReading).Should().BeLessThan(6, $"as a pixel marching to it does, {byReading} against {byMarching}");

        settings.GlowOff = true;
        Capture(Room, camera);
        gi.Screen.Should().BeNull("with no glow light the screen's probes left out are not made at all");
        SceneFieldCommands.IlluminationState(GetApp().World).Should().NotContain("standing for the glow lights");
        UnloadModel(slab);
        UnloadModel(strip);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Far_From_The_Worlds_Origin_Glow_Lights_Leave_No_More_Of_The_Land_Black_Than_Near_It()
    {
        // The voxel game's desert village at a field of view of 40 degrees, sand, houses and 56
        // torches under a low sun, drawn 10,000 units from the world's origin and at it. Far from it,
        // a far texel's neighbor read back from the window's depth fell on the same point, its normal
        // made from the two a value that is not a number, which the glow lights' light at the
        // screen's probes and the glow pass carried and their history spread, 26,258 pixels of the
        // land black at 640 by 360 on the development machine's GPU where 6,570 at the origin, the
        // houses' shadows; facing the eye where its neighbors give it none, 6,917.
        double Black(float offset)
        {
            var config = Config.Default.WithWindow("gi test", 640, 360) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            SetSceneField(4, 0.25f, 2);
            SetGlobalIllumination(GlobalIllumination.High);
            CreateDirectionalLight(Vector3.Normalize(new Vector3(0.6f, -0.35f, 0.4f)), new Color(255, 200, 150), 2, castsShadows: true);
            var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            var torch = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            torch.Materials[0].Emissive = new Color(255, 200, 120);
            torch.Materials[0].EmissiveIntensity = 4;
            var random = new Random(1);
            var houses = Enumerable.Range(0, 40).Select(_ => new Vector3(random.Next(-60, 60), 0, random.Next(-160, -40))).ToArray();
            var torches = Enumerable.Range(0, 56).Select(_ => new Vector3(random.Next(-60, 60) + 0.5f, 1.2f, random.Next(-160, -40) + 0.5f)).ToArray();
            var at = new Vector3(offset, 0, -offset);
            var eye = at + new Vector3(0, 20, 20);
            float pitch = -18 * MathF.PI / 180;
            var camera = new Camera3D(eye, eye + new Vector3(0, MathF.Sin(pitch), -MathF.Cos(pitch)), Vector3.UnitY, 40);
            var sand = new Color(219, 207, 163);
            for (int frame = 0; frame < 120; frame++)
            {
                BeginDrawing();
                ClearBackground(new Color(150, 195, 240));
                BeginMode3D(camera);
                DrawModelEx(block, at + new Vector3(0, -0.5f, -200), Vector3.UnitY, 0, new Vector3(600, 1, 600), sand);
                foreach (var house in houses) DrawModelEx(block, at + house + new Vector3(0, 2, 0), Vector3.UnitY, 0, new Vector3(5, 4, 5), sand);
                foreach (var lamp in torches) DrawModelEx(torch, at + lamp, Vector3.UnitY, 0, new Vector3(0.15f, 0.4f, 0.15f), Color.White);
                EndMode3D();
                EndDrawing();
            }
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            var black = 0;
            for (int i = 0; i < light.Length; i += 4)
                if (!(light[i] + light[i + 1] + light[i + 2] >= 1e-4f)) black++;
            UnloadModel(block);
            UnloadModel(torch);
            CloseWindow();
            UseApp(null);
            return black;
        }

        var (near, far) = (Black(0), Black(10_000));
        far.Should().BeLessThan(near * 1.15, $"the land far from the origin is as lit as near it, {far} pixels black where {near}");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Screen_Probe_Fed_A_Value_That_Is_Not_A_Number_Leaves_The_Light_It_Blends_Into_Finite()
    {
        // The middle probe's light a value that is not a number for three frames, as the voxel game's
        // desert village fed its probes one where their normals were, and then its own again. Kept by
        // the window's blend of the probes around and by the frame before's, it spread over the land
        // and stayed there; taken as none, the frame after reads as the frame before it was fed.
        Open();
        SetGlobalIllumination(GlobalIllumination.Low);
        CreatePointLight(new Vector3(0.5f, 2.6f, 0.8f), Color.White, 6, range: 10);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var lamp = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        lamp.Materials[0].Emissive = new Color(255, 200, 120);
        lamp.Materials[0].EmissiveIntensity = 4;
        // A floor, a red wall and a block, and a small lamp on the floor, whose light the probes
        // carry to the glow pass.
        void Draw()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(-1.6f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 6), new Color(220, 20, 20));
            DrawModelEx(slab, new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 30, new Vector3(1, 1.5f, 1), Color.White);
            DrawModelEx(lamp, new Vector3(1.2f, 0.1f, 0.8f), Vector3.UnitY, 0, new Vector3(0.2f, 0.2f, 0.2f), Color.White);
        }
        static int Black(Image image)
        {
            var black = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var c = GetImageColor(image, x, y);
                    if (Math.Max(c.R, Math.Max(c.G, c.B)) < 3) black++;
                }
            return black;
        }
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        var before = Capture(Draw);
        var probes = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Screen!;
        settings.LoggedScreenProbe = probes.Down / 2 * probes.Across + probes.Across / 2;
        settings.LoggedScreenProbeNotANumber = true;
        for (int frame = 0; frame < 3; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50));
            Draw();
            EndMode3D();
            EndDrawing();
        }
        settings.LoggedScreenProbeNotANumber = false;
        settings.LoggedScreenProbe = -1;
        var after = Capture(Draw);
        Black(after).Should().BeLessThanOrEqualTo(Black(before), $"no pixel the probe's non-number reached stays black, {Black(after)} black after it where {Black(before)} before");
        UnloadModel(slab);
        UnloadModel(lamp);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Back_Of_A_Two_Sided_Wall_Takes_The_Screens_Probes_As_Its_Front_Does()
    {
        // Two thin two-sided walls side by side facing the eye over a red emissive slab, the first's
        // normal toward the eye and the second's away from it, read at Medium with the screen's
        // probes and with them left out. Looked up along the normal as given, the second wall took
        // no screen probe, its red 1.000 of what it was with them left out where the first's was
        // 1.560; along the normal turned to the eye, 1.515.
        Open();
        SetGlobalIllumination(GlobalIllumination.Medium);
        SetAmbientLight(new Color(150, 180, 230), 0.3f);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var glow = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        glow.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 40, 30), EmissiveIntensity = 3 };
        var wall = LoadModelFromMesh(GenMeshPlane(1, 1, 1, 1));
        wall.Materials[0] = new ModelMaterial(new Color(200, 200, 200)) { Roughness = 1, DoubleSided = true, AlphaMode = MaterialAlphaMode.Opaque };
        var camera = new Camera3D(new Vector3(0, 1, 4), new Vector3(0, 0.8f, 0), Vector3.UnitY, 50);
        void Draw()
        {
            DrawModelEx(slab, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(12, 1, 12), new Color(120, 120, 120));
            DrawModelEx(glow, new Vector3(0, 0.05f, 0.6f), Vector3.UnitY, 0, new Vector3(3, 0.1f, 0.4f), Color.White);
            DrawModelEx(wall, new Vector3(-0.8f, 1, 0), Vector3.UnitX, 90, new Vector3(1.4f, 1, 2), Color.White);
            DrawModelEx(wall, new Vector3(0.8f, 1, 0), Vector3.UnitX, -90, new Vector3(1.4f, 1, 2), Color.White);
        }
        var gi = GetApp().World.Resource<GlobalIlluminationSettings>();
        var with = Capture(Draw, camera);
        gi.ScreenOff = true;
        var without = Capture(Draw, camera);
        gi.ScreenOff = false;
        // How much the screen's probes change each wall's red low on its face, near the slab.
        float Change(float x)
        {
            var at = GetWorldToScreenEx(new Vector3(x, 0.4f, 0.01f), camera, with.Width, with.Height);
            return Mean(with, (int)at.X - 3, (int)at.Y - 2, 6, 4).X / Mean(without, (int)at.X - 3, (int)at.Y - 2, 6, 4).X;
        }
        var (front, back) = (Change(-0.8f), Change(0.8f));
        (back - 1).Should().BeApproximately(front - 1, 0.25f * MathF.Abs(front - 1) + 0.01f, $"the back of a wall takes the screen's probes as the front does, {back:0.000} against {front:0.000}");
        foreach (var model in new[] { slab, glow, wall }) UnloadModel(model);
    }
}

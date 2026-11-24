using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A cascade holding fewer probes a side than the grid the probes' images are made at (gi.side).
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Cascade_Of_Eight_Probes_A_Side_In_A_Grid_Of_Twelve_Lights_The_Frame_As_A_Grid_Of_Eight()
    {
        // A portico on a sunlit ground, its floor and back wall in the shade of its roof and lit
        // by the ground's light alone, read from the first cascade's rays with the merge and the
        // light past the first bounce left out, so the counts of the cascades past it reach none
        // of it. Held to 8 a side in a grid of 12, the first cascade's probes stand where a grid
        // of 8 stands them, two probes in from the grid's sides, the probes past them untraced,
        // and the pixels read between its own alone.
        const int width = 160, height = 90;
        var config = Config.Default.WithWindow("side", width, height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.125f);
        SetSceneFieldCenter(new Vector3(-1, 1, 0));
        SetGlobalIllumination(GlobalIllumination.Low);
        SetAmbientLight(Color.Black, 0);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.6f, -1, 0.2f)), Color.White, 3);
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        (settings.ScreenOff, settings.MergeOff, settings.AgainOff, settings.Alone) = (true, true, true, 0);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        block.Materials[0] = new ModelMaterial(new Color(200, 200, 200)) { Roughness = 0.9f };
        void Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
            DrawModelEx(block, new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2, Vector3.UnitY, 0, new Vector3(x1 - x0, y1 - y0, z1 - z0), Color.White);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var stages = GlobalIlluminationStages.Of(device);
        var camera = new Camera3D(new Vector3(3, 1.2f, 0.5f), new Vector3(-4, 0.6f, 0), Vector3.UnitY, 60);

        // The frame and the first cascade's rays' light, settled at the counts given.
        (float[] Frame, float[] Rays, int Grid) Settled(int[] sides)
        {
            settings.ProbesSides = sides;
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 240; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                Box(-16, 16, -1, 0, -16, 16);
                Box(-4.3f, -4, 0, 2.3f, -3, 3);
                Box(-4.3f, 0, 2, 2.3f, -3, 3);
                EndMode3D();
                EndDrawing();
            }
            var gi = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Probes!;
            return (device.ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image), stages.ReadProbeVolume(gi, 0, merged: false), gi.Probes);
        }

        static double Luminance(float[] texels, int i) => 0.2126 * texels[i] + 0.7152 * texels[i + 1] + 0.0722 * texels[i + 2];

        var eight = Settled([8]);
        var twelve = Settled([8, 12]);
        eight.Grid.Should().Be(8);
        twelve.Grid.Should().Be(12, "the images are made at the most probes a side any cascade holds");

        // The first cascade's rays' light, each probe of the grid of 8 against the one two probes
        // in from the sides of the grid of 12, and the light of the grid of 12's probes past them.
        var n = (int)Math.Round(Math.Sqrt(eight.Rays.Length / 4.0 / (8 * 8 * 8)));
        twelve.Rays.Length.Should().Be(12 * n * 12 * n * 12 * 4);
        double raysApart = 0, raysHeld = 0, past = 0;
        for (int z = 0; z < 12; z++)
            for (int y = 0; y < 12 * n; y++)
                for (int x = 0; x < 12 * n; x++)
                {
                    var (px, py) = (x / n, y / n);
                    var at = ((z * 12 * n + y) * 12 * n + x) * 4;
                    if (px < 2 || px > 9 || py < 2 || py > 9 || z < 2 || z > 9)
                    {
                        past += Luminance(twelve.Rays, at);
                        continue;
                    }
                    var of = (((z - 2) * 8 * n + y - 2 * n) * 8 * n + x - 2 * n) * 4;
                    (raysApart, raysHeld) = (raysApart + Math.Abs(Luminance(twelve.Rays, at) - Luminance(eight.Rays, of)), raysHeld + Luminance(eight.Rays, of));
                }

        double frameApart = 0, frameHeld = 0;
        for (int i = 0; i < width * height; i++)
            (frameApart, frameHeld) = (frameApart + Math.Abs(Luminance(twelve.Frame, i * 4) - Luminance(eight.Frame, i * 4)), frameHeld + Luminance(eight.Frame, i * 4));
        var rays = raysApart / Math.Max(raysHeld, 1e-9);
        var picture = frameApart / Math.Max(frameHeld, 1e-9);
        output.WriteLine($"the first cascade's rays' light apart by {rays:E3} of {raysHeld:E3}, the frame by {picture:E3} of {frameHeld:E3}, the probes past its own holding {past:E3}");
        UnloadModel(block);
        // The rays' light read the same and the frame 1.7e-8 apart, where a grid of 12 standing
        // its first cascade's probes from its own corner, as a grid of 8 does, put them 1.44 and
        // 0.019 apart.
        past.Should().Be(0, "the probes of the grid past the first cascade's own are never traced");
        rays.Should().BeLessThan(1e-4, "the first cascade's probes stand and trace as a grid of 8 stands them");
        picture.Should().BeLessThan(1e-4, "the pixels read the first cascade's own probes as a grid of 8 holds them");
    }
}

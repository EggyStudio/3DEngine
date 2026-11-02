using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A closed corridor under a sky, its far end held by a cascade whose probes stand further apart
// than the corridor is wide, so most of them stand outside it, beside it, over it and under its
// floor, where they see the sky.
public sealed partial class CascadeEdgeTests
{
    [NeedsVulkanFact]
    public void A_Closed_Corridors_Far_End_Takes_None_Of_The_Sky_Its_Probes_Outside_It_Saw()
    {
        // Four units wide and three high, its end 34 units from the eye, which the field lies
        // around, past the third cascade's half width of 32 in the fourth, whose probes stand
        // sixteen units apart. The far end sees none of them, and blended by distance alone it took
        // the sky the probes outside saw, as the far end of Sponza's atrium did, 0.92 of the light
        // of the same end with the corridor open. With none of their sky it takes 0.499, the light
        // they see on the corridor's outer walls and the ground, which a surface that sees none of
        // its probes still blends, where with the field ahead of the eye, the end in the third
        // cascade eight apart, it took 0.220. Their reach's slack held to two of the finest cells,
        // a quarter of the sixteen's eighth, it takes 0.491, so the slack does not light it. At Low
        // the end lies past both cascades of probes, where the light from beyond them takes their
        // place, closed or open alike.
        const GlobalIllumination quality = GlobalIllumination.High;
        var config = Config.Default.WithWindow("corridor", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetGlobalIllumination(quality);
        var sky = new byte[64 * 32 * 4];
        for (int i = 0; i < 64 * 32; i++)
            (sky[i * 4], sky[i * 4 + 1], sky[i * 4 + 2], sky[i * 4 + 3]) = i < 64 * 16 ? ((byte)120, (byte)165, (byte)230, (byte)255) : ((byte)90, (byte)100, (byte)110, (byte)255);
        SetEnvironmentMap(new Image(sky, 64, 32));
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var stone = new Color(150, 150, 150);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var eye = new Vector3(0, 1.5f, 4);
        var camera = new Camera3D(eye, new Vector3(0, 1.5f, -30), Vector3.UnitY, 50);

        // The mean light over the far end's middle, the corridor closed or its floor alone drawn.
        double FarEnd(bool closed)
        {
            for (int frame = 0; frame < 60; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModelEx(block, new Vector3(0, -0.25f, -12), Vector3.UnitY, 0, new Vector3(6, 0.5f, 38), stone);
                DrawModelEx(block, new Vector3(0, 1.5f, -30.5f), Vector3.UnitY, 0, new Vector3(6, 3, 1), stone);
                if (closed)
                {
                    DrawModelEx(block, new Vector3(-2.5f, 1.5f, -12), Vector3.UnitY, 0, new Vector3(1, 3, 38), stone);
                    DrawModelEx(block, new Vector3(2.5f, 1.5f, -12), Vector3.UnitY, 0, new Vector3(1, 3, 38), stone);
                    DrawModelEx(block, new Vector3(0, 3.5f, -12), Vector3.UnitY, 0, new Vector3(6, 1, 38), stone);
                    DrawModelEx(block, new Vector3(0, 1.5f, 6.5f), Vector3.UnitY, 0, new Vector3(6, 3, 1), stone);
                }
                EndMode3D();
                EndDrawing();
            }
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            var at = GetWorldToScreenEx(new Vector3(0, 1.5f, -30), camera, Width, Height);
            double sum = 0;
            for (int y = (int)at.Y - 3; y < (int)at.Y + 3; y++)
                for (int x = (int)at.X - 3; x < (int)at.X + 3; x++)
                {
                    var p = (y * Width + x) * 4;
                    sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
                }
            return sum / 36;
        }

        var open = FarEnd(closed: false);
        var closed = FarEnd(closed: true);
        UnloadModel(block);
        (closed / open).Should().BeLessThan(0.55, $"the closed corridor hides the sky from its far end at {quality}, {closed:0.00000} against {open:0.00000} open");
    }
}

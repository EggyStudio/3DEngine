using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The faint glow lights at a probe's ray's hit taken as one point, against every light by its form.
public sealed partial class BounceRoomsTests
{
    [NeedsVulkanTheory]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Hits_Faint_Glow_Lights_Taken_As_One_Point_Read_Within_A_Level_Of_Their_Own_Forms(GlobalIllumination quality)
    {
        // A closed hall lit by a grid of small glowing blocks alone, where a hit on a wall or the
        // ceiling takes a few blocks near it and many far off, settled with the faint lights at a hit
        // as one point and with every light by its own form. The pictures lie within six tenths of
        // a level of each other on the mean, where the faint lights given nothing lie a level apart,
        // and differ somewhere, so some light did take its point.
        byte[] Settled(int view, bool points)
        {
            var config = Config.Default.WithWindow("points", 160, 90) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            SetSceneField(4, 0.15f);
            SetGlobalIllumination(quality);
            SetAmbientLight(Color.Black, 0);
            GetApp().World.Resource<GlobalIlluminationSettings>().PointsOff = !points;
            var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            var glow = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            glow.Materials[0].Emissive = new Color(255, 220, 170);
            glow.Materials[0].EmissiveIntensity = 6;
            var camera = view == 0
                ? new Camera3D(new Vector3(-7, 2.4f, -7), new Vector3(4, 0.5f, 4), Vector3.UnitY, 70)
                : new Camera3D(new Vector3(0, 2.6f, 7.5f), new Vector3(0, 2.2f, -8), Vector3.UnitY, 70);
            var white = new Color(200, 200, 195);
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 120; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                SetSceneFieldCenter(new Vector3(0, 1.5f, 0));
                DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(16.6f, 0.3f, 16.6f), white);
                DrawModelEx(slab, new Vector3(0, 3.15f, 0), Vector3.UnitY, 0, new Vector3(16.6f, 0.3f, 16.6f), white);
                DrawModelEx(slab, new Vector3(0, 1.5f, -8.15f), Vector3.UnitY, 0, new Vector3(16.6f, 3, 0.3f), white);
                DrawModelEx(slab, new Vector3(0, 1.5f, 8.15f), Vector3.UnitY, 0, new Vector3(16.6f, 3, 0.3f), white);
                DrawModelEx(slab, new Vector3(-8.15f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 16), white);
                DrawModelEx(slab, new Vector3(8.15f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 16), white);
                for (int x = -3; x <= 3; x++)
                    for (int z = -3; z <= 3; z++)
                        DrawModelEx(glow, new Vector3(x * 2, 0.15f, z * 2), Vector3.UnitY, 0, new Vector3(0.3f, 0.3f, 0.3f), Color.White);
                EndMode3D();
                EndDrawing();
            }
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var bytes = Srgb(((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image));
            UnloadModel(slab);
            UnloadModel(glow);
            CloseWindow();
            UseApp(null);
            return bytes;
        }

        var moved = 0L;
        foreach (var view in new[] { 0, 8 })
        {
            var (pointed, whole) = (Settled(view, points: true), Settled(view, points: false));
            var apart = Enumerable.Range(0, pointed.Length).Select(i => (long)Math.Abs(pointed[i] - whole[i])).ToArray();
            ((double)apart.Sum() / apart.Length).Should().BeLessThan(0.6,
                $"at {quality} the faint lights as one point read within a level of their own forms on the mean in view {view}, where given nothing they read a level darker");
            moved += apart.Sum();
        }
        moved.Should().BeGreaterThan(0, $"at {quality} some faint light at a hit took its point");
    }
}

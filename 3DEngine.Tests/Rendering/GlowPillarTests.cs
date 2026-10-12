using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The voxel game's room where a lamp's shadow showed a seam: a lamp of four glowstones beside a
/// pillar of two columns joined above, and the red wall behind, whose light past the pillar is read
/// along a line across the pillar's shadow.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class GlowPillarTests : IDisposable
{
    private const int Width = 640, Height = 360;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [NeedsVulkanFact]
    public void A_Lamps_Light_Past_A_Pillars_Edge_Rises_On_The_Wall_Behind_Without_A_Step()
    {
        // In the game the wall's light past the pillar rose by a fifth in 20 pixels where the march
        // to the lamp's face grazed the pillar's edge, since a way out of the pillar ended at the
        // step that left it, at least half a cell under the edge, and the share inside fell short of
        // the share outside by that much. A path-traced reference rises smoothly there.
        var config = Config.Default.WithWindow("glow pillar", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var lamp = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        lamp.Materials[0].Emissive = new Color(255, 200, 120);
        lamp.Materials[0].EmissiveIntensity = 4;
        var white = new Color(212, 216, 217);
        void Box(Vector3 from, Vector3 to, Model model, Color color) => DrawModelEx(model, (from + to) / 2, Vector3.UnitY, 0, to - from, color);
        var eye = new Vector3(9.5f, 56.62f, -35.4f);
        float heading = 185 * MathF.PI / 180, pitch = -3 * MathF.PI / 180;
        var camera = new Camera3D(eye, eye + new Vector3(MathF.Sin(heading) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(heading) * MathF.Cos(pitch)), Vector3.UnitY, 70);
        void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                Box(new(2, 54, -41), new(16, 55, -32), block, white);
                Box(new(2, 59, -41), new(16, 60, -32), block, white);
                Box(new(2, 55, -41), new(3, 59, -32), block, white);
                Box(new(15, 55, -41), new(16, 59, -32), block, white);
                Box(new(3, 55, -41), new(15, 59, -40), block, white);
                Box(new(3, 55, -33), new(15, 59, -32), block, new Color(142, 33, 33));
                Box(new(4, 55, -37), new(5, 57, -35), lamp, new Color(230, 190, 110));
                Box(new(6, 55, -37), new(7, 57, -35), block, white);
                Box(new(8, 55, -37), new(9, 57, -35), block, white);
                Box(new(6, 57, -37), new(9, 59, -35), block, white);
                EndMode3D();
                EndDrawing();
            }
        }
        Frames(400);

        // The wall's light, linear, along x from 9 to 11.5 blocks at the lamp's height, a point a
        // tenth of a block, over eight frames, which the screen probes' points move about in.
        var line = new double[26];
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        for (int frame = 0; frame < 8; frame++)
        {
            Frames(1);
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            for (int i = 0; i < line.Length; i++)
            {
                var at = GetWorldToScreenEx(new Vector3(9 + i * 0.1f, 56.5f, -33), camera, Width, Height);
                int p = ((int)at.Y * Width + (int)at.X) * 4;
                line[i] += (0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2]) / 8;
            }
        }
        var rises = Enumerable.Range(0, line.Length - 1).Select(i => Math.Abs(line[i] - line[i + 1]) / Math.Max(line[i], line[i + 1])).ToArray();
        var steepest = rises.Max();
        // The steepest tenth of a block fell by 9.8% of the light where the march grazed the
        // pillar's edge, the median tenth by 4.1%, and by 7.2% after on the development machine's
        // GPU, the climb toward the lamp's side.
        steepest.Should().BeLessThan(0.085, $"the light climbs smoothly toward the lamp, not by a step, along {string.Join(" ", line.Select(v => v.ToString("0.0000")))}");
        UnloadModel(block);
        UnloadModel(lamp);
    }
}

using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A canyon under a sky with no sun, seen from above along its length as the voxel game's hills
/// were, its floor's light read down the picture's middle across the ends of the screen's probes,
/// the world's cascades and the last cascade, past which the sky's light reaches the floor unhidden.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class CascadeEdgeTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private const int Width = 320, Height = 180;

    [NeedsVulkanTheory]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void The_Floors_Light_Fades_Across_Each_Cascades_End_Rather_Than_Stepping(GlobalIllumination quality)
    {
        // Read from one cascade alone, the floor's light stepped from one row to the next by 14% at
        // the screen's probes' end at High, by 10% at the third cascade's and by 15% at the last's,
        // past which the sky's light lit it unhidden, and by 25% at Low, whose screen probes end with
        // its last cascade, as the voxel game's hills at sunset stepped from 0.6 to 1.0 of the
        // sky's light where the last ended; faded across each one's outer band, by 3.7% and 1.9%.
        var config = Config.Default.WithWindow("cascade edge", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetGlobalIllumination(quality);
        SetAmbientLight(new Color(140, 170, 220), 1);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var eye = new Vector3(0, 20, 10);
        var down = 12 * MathF.PI / 180;
        var camera = new Camera3D(eye, eye + new Vector3(0, -MathF.Sin(down), -MathF.Cos(down)), Vector3.UnitY, 50);
        for (int frame = 0; frame < 60; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(block, new Vector3(0, -0.5f, -200), Vector3.UnitY, 0, new Vector3(60, 1, 420), new Color(180, 180, 180));
            DrawModelEx(block, new Vector3(-11, 6, -200), Vector3.UnitY, 0, new Vector3(2, 12, 420), new Color(160, 160, 160));
            DrawModelEx(block, new Vector3(11, 6, -200), Vector3.UnitY, 0, new Vector3(2, 12, 420), new Color(160, 160, 160));
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        UnloadModel(block);

        // Each row's floor light from below the horizon to the picture's foot, the middle sixteen
        // pixels averaged, out to 125 blocks along the canyon.
        var rows = new List<(float Along, double Light)>();
        for (int y = Height / 3; y < Height; y++)
        {
            var ray = GetScreenToWorldRayEx(new Vector2(Width / 2f, y + 0.5f), camera, Width, Height);
            if (ray.Direction.Y >= -1e-4f) continue;
            var along = -(ray.Position + ray.Direction * (-ray.Position.Y / ray.Direction.Y)).Z;
            if (along > 125) continue;
            double sum = 0;
            for (int x = Width / 2 - 8; x < Width / 2 + 8; x++)
            {
                int p = (y * Width + x) * 4;
                sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
            }
            rows.Add((along, sum / 16));
        }
        rows.Should().HaveCountGreaterThan(40, "the floor is seen out past the last cascade");
        var (step, at) = rows.Zip(rows.Skip(1), (a, b) => (Math.Abs(b.Light / a.Light - 1), a.Along)).MaxBy(s => s.Item1);
        step.Should().BeLessThan(0.06, $"the floor's light at {quality} changes from one row to the next by no step, as it did {at:0} blocks out");
    }
}

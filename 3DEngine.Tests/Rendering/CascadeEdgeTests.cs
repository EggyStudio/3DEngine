using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A canyon under a sky with no sun, seen from above along its length as the voxel game's hills
/// were, its floor's light read down the picture's middle across the ends of the screen's probes,
/// the world's cascades and the last cascade, past which the sky's light reaches the floor unhidden,
/// and a closed room under the same sky, with the eye inside turned through a circle and in the
/// frames before its last cascade is built.
/// </summary>
[Collection("E3D")]
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

    [NeedsVulkanTheory]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Closed_Rooms_Walls_Stay_Dark_At_Every_Heading_Of_An_Eye_Inside(GlobalIllumination quality)
    {
        // The cascades lie 0.35 of their width ahead of the eye, which stands 1.6 to 9.6 cells from
        // the side behind it. With the last cascade's band across that side, the walls took the
        // sky's light whole wherever the band reached them, by the heading as the cascades turned
        // with the eye, at High at six headings of twelve 0.11 to 0.14 of the open floor's light and
        // at Low at four up to 0.021; with each band kept clear of the eye, under 0.0004 at every
        // heading at both.
        var config = Config.Default.WithWindow("closed room", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetGlobalIllumination(quality);
        // A sky of one blue over a gray ground, as an environment map, which the room hides, where an
        // ambient light lights every surface whether the sky reaches it or not.
        var sky = new byte[64 * 32 * 4];
        for (int i = 0; i < 64 * 32; i++)
            (sky[i * 4], sky[i * 4 + 1], sky[i * 4 + 2], sky[i * 4 + 3]) = i < 64 * 16 ? ((byte)120, (byte)165, (byte)230, (byte)255) : ((byte)90, (byte)100, (byte)110, (byte)255);
        SetEnvironmentMap(new Image(sky, 64, 32));
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var stone = new Color(150, 150, 150);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var eye = new Vector3(0.5f, 1.6f, 1);

        // The picture's mean light after a few frames looking along a heading, the room closed or
        // only its floor drawn.
        double Seen(int heading, bool closed, int frames)
        {
            var toward = heading * MathF.PI / 180;
            var camera = new Camera3D(eye, eye + new Vector3(MathF.Sin(toward), -0.25f, -MathF.Cos(toward)), Vector3.UnitY, 70);
            for (int frame = 0; frame < frames; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModelEx(block, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(64, 1, 64), stone);
                if (closed)
                {
                    DrawModelEx(block, new Vector3(-3.5f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(1, 3, 8), stone);
                    DrawModelEx(block, new Vector3(3.5f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(1, 3, 8), stone);
                    DrawModelEx(block, new Vector3(0, 1.5f, -3.5f), Vector3.UnitY, 0, new Vector3(6, 3, 1), stone);
                    DrawModelEx(block, new Vector3(0, 1.5f, 3.5f), Vector3.UnitY, 0, new Vector3(6, 3, 1), stone);
                    DrawModelEx(block, new Vector3(0, 4, 0), Vector3.UnitY, 0, new Vector3(8, 2, 8), stone);
                }
                EndMode3D();
                EndDrawing();
            }
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            double sum = 0;
            for (int p = 0; p < Width * Height * 4; p += 4)
                sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
            return sum / (Width * Height);
        }

        var open = Seen(0, closed: false, 8);
        Seen(0, closed: true, 16);
        var headings = Enumerable.Range(0, 12).Select(i => (Heading: i * 30, Light: Seen(i * 30, closed: true, 3) / open)).ToList();
        UnloadModel(block);
        var (heading, most) = headings.MaxBy(h => h.Light);
        most.Should().BeLessThan(0.002, $"the room hides the sky at {quality} at every heading, where at {heading} degrees it read {most:0.0000} of the open floor's light");
    }

    [NeedsVulkanFact]
    public void A_Face_Before_The_Eye_Stays_Dark_Where_The_Eye_Stands_Nearest_The_Last_Cascades_Side()
    {
        // A surface facing the eye is leaned four cells toward the side behind the eye as the
        // probes are read for it, and the eye may stand 1.6 cells from that side, so a pillar's
        // face a block and a half before the eye, held at its leaned point, lay past the side,
        // held nothing and took the sky's light whole, the picture 0.157 of the open floor's light;
        // held at the face itself where that holds more, 0.0006. The voxel game's cave showed it
        // at three headings of a turn.
        var config = Config.Default.WithWindow("near the side", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        var sky = new byte[64 * 32 * 4];
        for (int i = 0; i < 64 * 32; i++)
            (sky[i * 4], sky[i * 4 + 1], sky[i * 4 + 2], sky[i * 4 + 3]) = i < 64 * 16 ? ((byte)120, (byte)165, (byte)230, (byte)255) : ((byte)90, (byte)100, (byte)110, (byte)255);
        SetEnvironmentMap(new Image(sky, 64, 32));
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var stone = new Color(150, 150, 150);
        var renderer = GetApp().World.Resource<Engine.Renderer>();

        // The eye looks west from the room's middle, the room moved along x to where the last
        // cascade's side behind the eye, to the east, lies two and a half to three and a half of its
        // cells off, nearer than the lean.
        var west = Vector3.Normalize(new Vector3(-1, -0.25f, 0));
        double Seen(Vector3 eye, bool closed, int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(new Camera3D(eye, eye + west, Vector3.UnitY, 70));
                var x = eye.X;
                DrawModelEx(block, new Vector3(x, -0.5f, 0), Vector3.UnitY, 0, new Vector3(64, 1, 64), stone);
                if (closed)
                {
                    DrawModelEx(block, new Vector3(x - 3.5f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(1, 3, 8), stone);
                    DrawModelEx(block, new Vector3(x + 3.5f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(1, 3, 8), stone);
                    DrawModelEx(block, new Vector3(x, 1.5f, -3.5f), Vector3.UnitY, 0, new Vector3(6, 3, 1), stone);
                    DrawModelEx(block, new Vector3(x, 1.5f, 3.5f), Vector3.UnitY, 0, new Vector3(6, 3, 1), stone);
                    DrawModelEx(block, new Vector3(x, 4, 0), Vector3.UnitY, 0, new Vector3(8, 2, 8), stone);
                    DrawModelEx(block, new Vector3(x - 1.5f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.5f, 3, 0.5f), stone);
                }
                EndMode3D();
                EndDrawing();
            }
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            double sum = 0;
            for (int p = 0; p < Width * Height * 4; p += 4)
                sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
            return sum / (Width * Height);
        }

        var open = Seen(new Vector3(0, 1.6f, 0), closed: false, 8);
        var plan = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!.Plan!;
        var last = plan.Cascades - 1;
        float Behind(Vector3 eye) => (plan.OriginAround(last, eye, west).X + SceneFieldPlan.Resolution * plan.CellOf(last) - eye.X) / plan.CellOf(last);
        var place = Enumerable.Range(0, 64).Select(i => new Vector3(i * 0.25f, 1.6f, 0)).First(eye => Behind(eye) is > 2.5f and < 3.5f);
        var seen = Seen(place, closed: true, 24) / open;
        UnloadModel(block);
        seen.Should().BeLessThan(0.002, $"the pillar's face before the eye, {Behind(place):0.0} cells from the last cascade's side behind it, holds the probes' light and hides the sky");
    }

    [NeedsVulkanFact]
    public void A_Closed_Rooms_Floor_Stays_Hidden_From_An_Ambient_Light_Before_The_Last_Cascade_Is_Built()
    {
        // The field built a cascade a frame, its last comes in the fourth. Held by the last cascade
        // whether built or not, the floor took the ambient light unhidden in the bounce's place and
        // read 9.4 times the light it settles at in the three frames before; held by the coarsest
        // cascade built, 0.54, 0.54 and 1.46 times it, the bounce gathered over fewer cascades.
        var config = Config.Default.WithWindow("closed room", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(new Color(140, 170, 220), 1);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 2.5f, 3), new Vector3(0, 0.5f, -2), Vector3.UnitY, 70);
        var white = new Color(200, 200, 200);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var floor = new List<double>();
        for (int frame = 0; frame < 8; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(block, new Vector3(0, -0.25f, 0), Vector3.UnitY, 0, new Vector3(11, 0.5f, 11), white);
            DrawModelEx(block, new Vector3(0, 4.25f, 0), Vector3.UnitY, 0, new Vector3(11, 0.5f, 11), white);
            DrawModelEx(block, new Vector3(-5.25f, 2, 0), Vector3.UnitY, 0, new Vector3(0.5f, 4, 11), white);
            DrawModelEx(block, new Vector3(5.25f, 2, 0), Vector3.UnitY, 0, new Vector3(0.5f, 4, 11), white);
            DrawModelEx(block, new Vector3(0, 2, -5.25f), Vector3.UnitY, 0, new Vector3(11, 4, 0.5f), white);
            DrawModelEx(block, new Vector3(0, 2, 5.25f), Vector3.UnitY, 0, new Vector3(11, 4, 0.5f), white);
            EndMode3D();
            EndDrawing();
            if (frame is > 2 and < 7) continue;

            // The floor's mean light over the middle half of the picture's lowest quarter.
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            double sum = 0;
            for (int y = Height * 3 / 4; y < Height; y++)
                for (int x = Width / 4; x < Width * 3 / 4; x++)
                {
                    int p = (y * Width + x) * 4;
                    sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
                }
            floor.Add(sum / (Height / 4 * (Width / 2)));
        }
        UnloadModel(block);

        var settled = floor[^1];
        floor.Take(3).Should().OnlyContain(light => light < 2 * settled, "the room hides the ambient light from its floor before its last cascade is built");
    }
}

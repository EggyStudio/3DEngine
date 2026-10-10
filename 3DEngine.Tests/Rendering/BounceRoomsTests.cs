using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The rooms of <c>shaders_bounce_rooms</c> at each quality of the light that bounces against
/// references path-traced through the GPU's rays, each view's frame held within its measured
/// difference from its reference.
/// </summary>
/// <remarks>
/// A reference is the view's light traced with 4096 paths a pixel at 160 by 90, averaged to 80 by
/// 45, in linear light, checked in under <c>References/bounce</c>; setting
/// <c>E3D_WRITE_REFERENCES=1</c> traces them again. The frame's linear light is averaged the same,
/// and a view's difference is its pixels' differences summed over the reference's light, so one
/// region's excess does not hide another's shortfall. The test runs where the GPU traces rays, as
/// the references are traced through them and the bounds were measured on such a GPU, an RTX 4070.
/// </remarks>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class BounceRoomsTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private const int Width = 160, Height = 90, Down = 2;

    // Each view at each quality, bound 15% over what it read on an RTX 4070, where the views
    // read 0.15 to 0.17 of their light apart from their references in the Cornell box, 0.08 to
    // 0.09 in the thin room, 0.26 in the corridor, 0.27 to 0.30 in the window's, 0.02 among the red
    // walls, 0.48 to 0.51 in the strip's, 0.10 to 0.12 on the grazing floor and 0.12 in the carried
    // lamp's.
    [NeedsRayQueryTheory]
    [InlineData(1, GlobalIllumination.Low, 0.20)]
    [InlineData(1, GlobalIllumination.Medium, 0.18)]
    [InlineData(1, GlobalIllumination.High, 0.20)]
    [InlineData(2, GlobalIllumination.Low, 0.11)]
    [InlineData(2, GlobalIllumination.Medium, 0.10)]
    [InlineData(2, GlobalIllumination.High, 0.11)]
    [InlineData(3, GlobalIllumination.Low, 0.30)]
    [InlineData(3, GlobalIllumination.Medium, 0.30)]
    [InlineData(3, GlobalIllumination.High, 0.30)]
    [InlineData(4, GlobalIllumination.Low, 0.35)]
    [InlineData(4, GlobalIllumination.Medium, 0.32)]
    [InlineData(4, GlobalIllumination.High, 0.33)]
    [InlineData(5, GlobalIllumination.Low, 0.03)]
    [InlineData(5, GlobalIllumination.Medium, 0.03)]
    [InlineData(5, GlobalIllumination.High, 0.03)]
    [InlineData(6, GlobalIllumination.Low, 0.59)]
    [InlineData(6, GlobalIllumination.Medium, 0.57)]
    [InlineData(6, GlobalIllumination.High, 0.56)]
    [InlineData(7, GlobalIllumination.Low, 0.12)]
    [InlineData(7, GlobalIllumination.Medium, 0.13)]
    [InlineData(7, GlobalIllumination.High, 0.15)]
    [InlineData(8, GlobalIllumination.Low, 0.15)]
    [InlineData(8, GlobalIllumination.Medium, 0.14)]
    [InlineData(8, GlobalIllumination.High, 0.15)]
    public void Each_Room_Reads_Within_Its_Measured_Difference_From_Its_Reference(int view, GlobalIllumination quality, double bound)
    {
        var config = Config.Default.WithWindow("bounce rooms", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        using var rooms = new Rooms();
        var camera = Rooms.Views[view - 1];
        void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                rooms.Draw();
                EndMode3D();
                EndDrawing();
            }
        }

        var path = Path.Combine(Folder(), $"view-{view}.pfm");
        if (!File.Exists(path) || Environment.GetEnvironmentVariable("E3D_WRITE_REFERENCES") == "1")
        {
            // Traced at High, where the window's meshes are built for the GPU's rays.
            SetGlobalIllumination(GlobalIllumination.High);
            Frames(SceneFieldPlan.SettleFrames + 30);
            using var folder = new TestFolder("engine-bounce-");
            var traced = Path.Combine(folder.Path, $"view-{view}.png");
            BounceReference.Trace(GetApp().World, traced, 4096);
            var (width, height, light) = BounceReference.ReadPfm(traced + ".pfm");
            WritePfm(path, Averaged(light, width, height, 3));
        }

        SetGlobalIllumination(quality);
        Frames(SceneFieldPlan.SettleFrames + 90);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var decoded = renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!;
        var frame = Averaged(((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(decoded.ColorView.Image), Width, Height, 4);
        var (_, _, reference) = BounceReference.ReadPfm(path);
        double differs = 0, lit = 0;
        for (int i = 0; i < reference.Length; i++)
        {
            differs += Math.Abs(frame[i] - reference[i]);
            lit += reference[i];
        }
        var share = differs / lit;
        share.Should().BeLessThan(bound, $"view {view} at {quality} differs from its reference by {share:0.000} of its light");
    }

    // The references' folder beside this file.
    private static string Folder([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(source)!, "References", "bounce");

    // Light of width by height, channels a pixel, rows from the top, averaged over Down by Down
    // pixels, three channels a pixel, a pixel past the picture's light taken as nothing.
    private static float[] Averaged(float[] light, int width, int height, int channels)
    {
        var (w, h) = (width / Down, height / Down);
        var averaged = new float[w * h * 3];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                for (int c = 0; c < 3; c++)
                {
                    float sum = 0;
                    for (int dy = 0; dy < Down; dy++)
                        for (int dx = 0; dx < Down; dx++)
                        {
                            var value = light[((y * Down + dy) * width + x * Down + dx) * channels + c];
                            sum += float.IsFinite(value) ? value : 0;
                        }
                    averaged[(y * w + x) * 3 + c] = sum / (Down * Down);
                }
        return averaged;
    }

    // A PFM file of the averaged light, its rows from the bottom as the format has them and as
    // BounceReference.ReadPfm reads them.
    private static void WritePfm(string path, float[] light)
    {
        var (w, h) = (Width / Down, Height / Down);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        file.Write(Encoding.ASCII.GetBytes($"PF\n{w} {h}\n-1.0\n"));
        for (int y = h - 1; y >= 0; y--)
            file.Write(MemoryMarshal.AsBytes(light.AsSpan(y * w * 3, w * 3)));
    }

    // The rooms, their lights and their views as shaders_bounce_rooms has them.
    private sealed class Rooms : IDisposable
    {
        private readonly Model _slab, _glow, _strip;

        public static readonly Camera3D[] Views =
        [
            new(new Vector3(0, 2.5f, 8), new Vector3(0, 2.4f, 0), Vector3.UnitY, 45),
            new(new Vector3(38.3f, 1.4f, 1.6f), new Vector3(41.8f, 1.1f, -0.4f), Vector3.UnitY, 60),
            new(new Vector3(87.5f, 1.4f, 0), new Vector3(72, 1, 0), Vector3.UnitY, 60),
            new(new Vector3(122.6f, 1.6f, 2.6f), new Vector3(118, 0.8f, -1), Vector3.UnitY, 65),
            new(new Vector3(172, 4, 9), new Vector3(162, 0.6f, -5), Vector3.UnitY, 50),
            new(new Vector3(200, 1.4f, 2.3f), new Vector3(200, 1.3f, -2.5f), Vector3.UnitY, 65),
            new(new Vector3(234.5f, 0.35f, 5.5f), new Vector3(245, 0.15f, -5), Vector3.UnitY, 60),
            new(new Vector3(282.6f, 1.7f, 2.6f), new Vector3(278.5f, 1, -1.5f), Vector3.UnitY, 65),
        ];

        public Rooms()
        {
            CreateDirectionalLight(Vector3.Normalize(new Vector3(0.8f, -1, 0.25f)), new Color(255, 245, 230), 1.5f, castsShadows: true);
            CreatePointLight(new Vector3(0, 4.2f, 0), new Color(255, 236, 210), 9, range: 12);
            CreatePointLight(new Vector3(40, 2.2f, 0), Color.White, 0.6f, range: 8);
            CreatePointLight(new Vector3(43, 1.5f, 0), Color.White, 25, range: 10, castsShadows: true);
            CreatePointLight(new Vector3(240, 2.6f, 0), new Color(255, 236, 210), 14, range: 16);
            CreatePointLight(new Vector3(278, 2, -1.5f), new Color(255, 230, 200), 8, range: 10, castsShadows: true);
            _slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            _glow = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            _glow.Materials[0].Emissive = new Color(255, 240, 220);
            _glow.Materials[0].EmissiveIntensity = 6;
            _strip = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            _strip.Materials[0].Emissive = new Color(255, 230, 190);
            _strip.Materials[0].EmissiveIntensity = 30;
        }

        public void Draw()
        {
            var white = new Color(220, 220, 215);
            var red = new Color(200, 30, 30);
            var green = new Color(30, 170, 40);
            Box(_slab, new Vector3(0, -0.15f, 0), new Vector3(5.6f, 0.3f, 5.6f), white);
            Box(_slab, new Vector3(0, 5.15f, 0), new Vector3(5.6f, 0.3f, 5.6f), white);
            Box(_slab, new Vector3(0, 2.5f, -2.65f), new Vector3(5.6f, 5.6f, 0.3f), white);
            Box(_slab, new Vector3(-2.65f, 2.5f, 0), new Vector3(0.3f, 5.6f, 5.6f), red);
            Box(_slab, new Vector3(2.65f, 2.5f, 0), new Vector3(0.3f, 5.6f, 5.6f), green);
            Box(_slab, new Vector3(-0.9f, 1.5f, -0.8f), new Vector3(1.4f, 3, 1.4f), white, 20);
            Box(_slab, new Vector3(1, 0.7f, 0.7f), new Vector3(1.4f, 1.4f, 1.4f), white, -18);
            Box(_glow, new Vector3(0, 4.97f, 0), new Vector3(1.6f, 0.06f, 1.6f), Color.White);

            Room(new Vector3(40, 0, 0), new Vector3(4, 3, 4), 0.1f, white);
            Box(_slab, new Vector3(40.8f, 0.5f, -0.6f), new Vector3(1, 1, 1), white);

            Room(new Vector3(80, 0, 0), new Vector3(16, 2.5f, 2), 0.3f, white, openToward: true);

            Box(_slab, new Vector3(120, -0.15f, 0), new Vector3(6.6f, 0.3f, 6.6f), white);
            Box(_slab, new Vector3(120, 3.15f, 0), new Vector3(6.6f, 0.3f, 6.6f), white);
            Box(_slab, new Vector3(120, 1.5f, -3.15f), new Vector3(6.6f, 3, 0.3f), white);
            Box(_slab, new Vector3(120, 1.5f, 3.15f), new Vector3(6.6f, 3, 0.3f), white);
            Box(_slab, new Vector3(123.15f, 1.5f, 0), new Vector3(0.3f, 3, 6), green);
            Box(_slab, new Vector3(116.85f, 0.25f, 0), new Vector3(0.3f, 0.5f, 6), white);
            Box(_slab, new Vector3(116.85f, 2.8f, 0), new Vector3(0.3f, 0.4f, 6), white);
            Box(_slab, new Vector3(116.85f, 1.55f, -2.5f), new Vector3(0.3f, 2.1f, 1), white);
            Box(_slab, new Vector3(116.85f, 1.55f, 2.5f), new Vector3(0.3f, 2.1f, 1), white);

            Box(_slab, new Vector3(165, -0.15f, -6), new Vector3(30, 0.3f, 30), white);
            float[] apart = [1, 3, 6];
            for (int i = 0; i < 3; i++)
            {
                var z = -i * 5f;
                Box(_slab, new Vector3(160, 0.75f, z), new Vector3(1, 1.5f, 1), white);
                Box(_slab, new Vector3(160.5f + apart[i] + 0.15f, 1.5f, z), new Vector3(0.3f, 3, 2.5f), red);
            }

            Room(new Vector3(200, 0, 0), new Vector3(5, 3, 5), 0.3f, white);
            Box(_strip, new Vector3(200, 1.5f, -2.45f), new Vector3(2, 0.06f, 0.06f), Color.White);

            Room(new Vector3(240, 0, 0), new Vector3(12, 3, 12), 0.3f, white);

            Room(new Vector3(280, 0, 0), new Vector3(6, 3, 6), 0.3f, white);
            Box(_slab, new Vector3(277.15f, 1.5f, 0), new Vector3(0.3f, 3, 6), red);
            Box(_slab, new Vector3(280, 1.25f, -1), new Vector3(0.2f, 2.5f, 2.5f), green);
            DrawSphere(new Vector3(278, 2, -1.5f), 0.08f, new Color(255, 230, 200));
        }

        private void Room(Vector3 floor, Vector3 size, float thick, Color color, bool openToward = false)
        {
            var (w, h, d) = (size.X, size.Y, size.Z);
            var t = thick;
            Box(_slab, floor + new Vector3(0, -t / 2, 0), new Vector3(w + 2 * t, t, d + 2 * t), color);
            Box(_slab, floor + new Vector3(0, h + t / 2, 0), new Vector3(w + 2 * t, t, d + 2 * t), color);
            Box(_slab, floor + new Vector3(0, h / 2, -d / 2 - t / 2), new Vector3(w + 2 * t, h, t), color);
            Box(_slab, floor + new Vector3(0, h / 2, d / 2 + t / 2), new Vector3(w + 2 * t, h, t), color);
            if (!openToward) Box(_slab, floor + new Vector3(-w / 2 - t / 2, h / 2, 0), new Vector3(t, h, d), color);
            Box(_slab, floor + new Vector3(w / 2 + t / 2, h / 2, 0), new Vector3(t, h, d), color);
        }

        private static void Box(Model slab, Vector3 middle, Vector3 size, Color color, float turn = 0) =>
            DrawModelEx(slab, middle, Vector3.UnitY, turn, size, color);

        public void Dispose()
        {
            UnloadModel(_slab);
            UnloadModel(_glow);
            UnloadModel(_strip);
        }
    }
}

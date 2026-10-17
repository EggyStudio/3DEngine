using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// An emissive block on a floor with a wall half its height beside it, and the floor's light read
/// along a line across the wall's shadow. The penumbra the screen's probes give holds still as the
/// view turns and frame to frame, follows a lamp placed or a wall taken away within a round of
/// the probes' points, and with no screen probe the pixel's own marches keep the shadow.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class GlowPenumbraTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private const int Width = 320, Height = 180;

    // The frames an app is drawn before its first line is read, and a round of the probes' points
    // (GlobalIlluminationRenderer.JitterAt), which a line is read over.
    private const int Warm = 48, Round = 16;

    [NeedsVulkanFact]
    public void A_Turn_Of_A_Few_Pixels_Moves_The_Penumbra_With_The_World()
    {
        // Marched to from their tiles' middles, the probes moved the floor's light along the line
        // by 3.1 to 7.9% of it on average with turns of one to eight pixels at 640 by 360, as the
        // window's grid moved over the floor, and here by 15 to 18% with turns of two and five;
        // from points moved about their tiles, read through a blend over a round at each texel of
        // the window's depth, by 0.44 to 0.89% at 640 by 360, where a pixel that marches itself
        // reads 0.69 to 0.91%, and here by 1.8 to 3.1%, where such a pixel reads 1.2 to 3.5%. A way
        // out of the wall that ends where it left it, so a march grazing the wall's top is barely
        // under it, moved the line by 5.4% at a turn of five where 2.2% on the development
        // machine's GPU, the share under a wall one cell thick read from the field's distance there.
        using var scene = new Scene(screenOff: false);
        var still = scene.Line(0, Warm, out var moved);
        var turned = scene.Line(5, 2 * Round, out _);
        Strays(turned, still).Should().BeLessThan(0.065, "a turn of five pixels moves the penumbra with the floor");
        moved.Should().BeLessThan(0.02, "the line holds still over a round of the probes' points");
    }

    [NeedsVulkanFact]
    public void A_Lamp_Placed_Shows_At_Once_And_A_Wall_Taken_Away_Lets_It_Through_At_Once()
    {
        // The lamp's place is new, so the blend each texel held is dropped and the frame's light
        // taken alone. The line holds all the lamp's light in that frame and strays from where it
        // settles by 7.7%, its penumbra one point of a round, and by 0.8% a round on. The floor
        // behind the wall sees all of the lamp in the frame the wall goes, further from the blend
        // than the frames before had lain, and strays by 0.04%.
        using var scene = new Scene(screenOff: false);
        scene.Lamp = false;
        scene.Frames(2 * Round);
        scene.Lamp = true;
        var placed = scene.Read();
        scene.Frames(Round - 1);
        var round = scene.Read();
        var settled = scene.Line(0, Round, out _);
        (placed.Sum() / settled.Sum()).Should().BeApproximately(1, 0.03, "the lamp's light shows in the frame it is placed in");
        Strays(placed, settled).Should().BeLessThan(0.12, "the penumbra in that frame is one point of a round");
        Strays(round, settled).Should().BeLessThan(0.015, "the penumbra settles within a round");

        scene.Wall = false;
        var opened = scene.Read();
        var open = scene.Line(0, Round, out _);
        Strays(opened, open).Should().BeLessThan(0.01, "the floor behind the wall takes the lamp's light in the frame the wall goes");
    }

    [NeedsVulkanFact]
    public void The_Shadow_Settles_Within_Two_Rounds_Of_An_Apps_Start()
    {
        // Before the field holds the wall, the probes see the lamp past it. Held whole in the
        // blend, those frames left the line straying from where it settles by 6.3% two rounds after
        // the app began and under 1% only after 110 frames; with a young blend restarted where the
        // field's cascade under its point is built where it lay, by 0.5%.
        using var scene = new Scene(screenOff: false);
        scene.Frames(2 * Round - 1);
        var early = scene.Read();
        var settled = scene.Line(0, 2 * Round, out _);
        Strays(early, settled).Should().BeLessThan(0.02, "the shadow is in place two rounds after the app began");
    }

    [NeedsVulkanFact]
    public void With_No_Screen_Probe_The_Floor_Behind_The_Wall_Stays_In_Its_Shadow()
    {
        // A pixel no screen probe holds marches to the faces' halves, as a probe does. With one
        // march to the lamp's middle from a cell and a half off the floor, the floor at the wall's
        // foot saw over the wall and read 0.3 to 1.0 of the most light along the line, where the
        // lamp is hidden from it whole.
        using var scene = new Scene(screenOff: true);
        scene.Frames(Warm / 2);
        var line = scene.Read();
        var peak = line.Max();
        line.Take(5).Should().OnlyContain(light => light < 0.02 * peak, "the floor from 1.3 to 1.7 blocks out lies in the wall's shadow, the lamp hidden from it");
    }

    [NeedsVulkanFact]
    public void Each_Frame_Draws_With_The_Build_That_Leaves_Out_What_It_Does_Not_Hold()
    {
        // The model pass's fragment stage is 253,212 bytes of SPIR-V whole, 124,660 without the
        // glow lights' marches and 55,228 without global illumination as well, which lavapipe
        // compiles as it first draws with each pipeline. Each frame takes its build by its own glow
        // lights and global illumination, so the lamp shows in the frame it is placed in, as the
        // test of a lamp placed reads.
        using var scene = new Scene(screenOff: false);
        var models = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<ModelRenderer>()!;
        scene.Lamp = false;
        scene.Frames(2);
        models.Drew.Should().Be(ModelBuild.Lampless, "the frame holds no glow light");
        scene.Lamp = true;
        scene.Frames(1);
        models.Drew.Should().Be(ModelBuild.Whole, "the lamp placed is a glow light");
        scene.Lamp = false;
        scene.Frames(1);
        models.Drew.Should().Be(ModelBuild.Lampless, "the lamp taken away leaves none");
        SetGlobalIllumination(GlobalIllumination.Off);
        scene.Frames(1);
        models.Drew.Should().Be(ModelBuild.Unbounced, "global illumination is off");
    }

    // How far a line strays from another on average, as a share of the light at each point, the
    // light floored at a tenth of the other's peak so the shadow's near nothing does not divide.
    private static double Strays(double[] line, double[] from)
    {
        var floor = 0.1 * from.Max();
        return line.Zip(from, (a, b) => Math.Abs(a - b) / Math.Max(b, floor)).Average();
    }

    // The block, the wall half its height a block and a quarter from it, and a floor, seen from
    // above and to the side.
    private sealed class Scene : IDisposable
    {
        private readonly Model _block, _lamp;
        private Camera3D _camera;

        public bool Lamp { get; set; } = true;
        public bool Wall { get; set; } = true;

        public Scene(bool screenOff)
        {
            var config = Config.Default.WithWindow("glow penumbra", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            SetSceneField(4, 0.25f, 2);
            SetGlobalIllumination(GlobalIllumination.High);
            SetAmbientLight(Color.Black, 0);
            if (screenOff) GetApp().World.Resource<GlobalIlluminationSettings>().ScreenOff = true;
            _block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            _lamp = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            _lamp.Materials[0].Emissive = new Color(255, 200, 120);
            _lamp.Materials[0].EmissiveIntensity = 4;
            _camera = View(0);
        }

        // The view turned so the picture's middle moves `shift` pixels.
        private static Camera3D View(int shift)
        {
            var eye = new Vector3(4.5f, 2.5f, 2.5f);
            var focal = Width / 2f / MathF.Tan(MathF.Atan(MathF.Tan(70 * MathF.PI / 360) * Width / Height));
            var ahead = Vector3.Transform(new Vector3(2.5f, 0, 0) - eye, Matrix4x4.CreateRotationY(MathF.Atan(shift / focal)));
            return new Camera3D(eye, eye + ahead, Vector3.UnitY, 70);
        }

        public void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(_camera);
                DrawModelEx(_block, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(64, 1, 64), new Color(200, 200, 200));
                if (Lamp)
                    DrawModelEx(_lamp, new Vector3(0, 0.5f, 0), Vector3.UnitY, 0, Vector3.One, new Color(230, 190, 110));
                if (Wall)
                    DrawModelEx(_block, new Vector3(1.125f, 0.25f, 0), Vector3.UnitY, 0, new Vector3(0.25f, 0.5f, 2), new Color(120, 120, 120));
                EndMode3D();
                EndDrawing();
            }
        }

        // The view turned by `shift` pixels drawn `warm` frames, then the line over a round, with
        // in moved how far the round's frames strayed from it on average.
        public double[] Line(int shift, int warm, out double moved)
        {
            _camera = View(shift);
            Frames(warm);
            var lines = Enumerable.Range(0, Round).Select(_ => Read()).ToArray();
            var mean = Enumerable.Range(0, lines[0].Length).Select(i => lines.Average(l => l[i])).ToArray();
            moved = lines.Average(l => Strays(l, mean));
            return mean;
        }

        // The next frame's floor light, linear, along x across the wall's shadow from 1.3 to 4.3
        // blocks at z 0, each point bilinear between the pixels around it.
        public double[] Read()
        {
            Frames(1);
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            return [.. Enumerable.Range(0, 31).Select(i =>
            {
                var at = GetWorldToScreenEx(new Vector3(1.3f + i * 0.1f, 0, 0), _camera, Width, Height);
                int x0 = (int)MathF.Floor(at.X - 0.5f), y0 = (int)MathF.Floor(at.Y - 0.5f);
                float fx = at.X - 0.5f - x0, fy = at.Y - 0.5f - y0;
                double At(int px, int py)
                {
                    int p = (Math.Clamp(py, 0, Height - 1) * Width + Math.Clamp(px, 0, Width - 1)) * 4;
                    return 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
                }
                return (At(x0, y0) * (1 - fx) + At(x0 + 1, y0) * fx) * (1 - fy) + (At(x0, y0 + 1) * (1 - fx) + At(x0 + 1, y0 + 1) * fx) * fy;
            })];
        }

        public void Dispose()
        {
            UnloadModel(_block);
            UnloadModel(_lamp);
        }
    }
}

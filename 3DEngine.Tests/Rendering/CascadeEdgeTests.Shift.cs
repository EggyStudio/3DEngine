using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A closed corridor walked down, the field following the eye, each cascade's shift bringing in the
// corridor's walls ahead as a slab built over frames.
public sealed partial class CascadeEdgeTests
{
    [NeedsVulkanFact]
    public void A_Closed_Corridors_Light_Holds_While_The_Slabs_A_Walk_Brings_Into_The_Field_Are_Built()
    {
        // A corridor two units wide, three high and twenty-four long, closed at both ends under the
        // closed room's sky, its walls a unit thick in pieces a unit long, so at 64 triangles a
        // frame, five pieces, each slab a cascade's shift brings in is built over frames. A shift
        // moves its cascade with its build's first part, the cells it brings in holding nothing
        // until the last, and read so the rays through them went on past the walls they stand for
        // to the sky, as Sponza's atrium's far arches flashed lit while the field followed the eye
        // down it. Walked down a tenth of a unit a frame, the corridor's light leapt over the frame
        // before's in a frame a slab waited, and with those cells read in the next cascade out,
        // which holds the walls there, it holds in every frame.
        // The frames unpaced, so none waits and the field's triangle budget is never raised past the
        // 64 it is set to, where paced at 60 a frame of the suite's warm caches waited and the slabs
        // were built in a frame or two.
        var config = Config.Default.WithWindow("closed corridor", Width, Height) with { Headless = true, Offscreen = true, Samples = 1, HeadlessFps = 0 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.25f, 2, triangleBudget: 64);
        SetGlobalIllumination(GlobalIllumination.High);
        var sky = new byte[64 * 32 * 4];
        for (int i = 0; i < 64 * 32; i++)
            (sky[i * 4], sky[i * 4 + 1], sky[i * 4 + 2], sky[i * 4 + 3]) = i < 64 * 16 ? ((byte)120, (byte)165, (byte)230, (byte)255) : ((byte)90, (byte)100, (byte)110, (byte)255);
        SetEnvironmentMap(new Image(sky, 64, 32));
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var stone = new Color(150, 150, 150);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>;

        // The picture's mean light with the eye at z looking down the corridor, its floor alone
        // drawn or the whole of it.
        double Seen(float z, bool closed)
        {
            var eye = new Vector3(0, 1.6f, z);
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(eye, eye + new Vector3(0, -0.15f, -1), Vector3.UnitY, 70));
            for (int i = 0; i < 24; i++)
            {
                var along = -0.5f - i;
                DrawModelEx(block, new Vector3(0, -0.5f, along), Vector3.UnitY, 0, new Vector3(4, 1, 1), stone);
                if (!closed) continue;
                DrawModelEx(block, new Vector3(-1.5f, 1.5f, along), Vector3.UnitY, 0, new Vector3(1, 3, 1), stone);
                DrawModelEx(block, new Vector3(1.5f, 1.5f, along), Vector3.UnitY, 0, new Vector3(1, 3, 1), stone);
                DrawModelEx(block, new Vector3(0, 3.5f, along), Vector3.UnitY, 0, new Vector3(4, 1, 1), stone);
            }
            if (closed)
                foreach (var end in new[] { 0.5f, -24.5f })
                    DrawModelEx(block, new Vector3(0, 1.5f, end), Vector3.UnitY, 0, new Vector3(4, 5, 1), stone);
            EndMode3D();
            EndDrawing();
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            double sum = 0;
            for (int p = 0; p < Width * Height * 4; p += 4)
                sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
            return sum / (Width * Height);
        }
        double Settled(float z, bool closed)
        {
            for (int frame = 0; frame < 600; frame++)
            {
                Seen(z, closed);
                if (fields()?.Plan is { Built: true, FramesBuilt: > 60 }) break;
            }
            fields()!.Plan!.Built.Should().BeTrue("the field is built around the eye");
            return Seen(z, closed);
        }

        var open = Settled(-4, closed: false);
        var before = Settled(-4, closed: true) / open;
        // Down the corridor a tenth of a unit a frame for eight units, the first cascade shifting
        // every two and the second every four.
        var (most, at, waited) = (0.0, 0, 0);
        for (int frame = 0; frame < 80; frame++)
        {
            var light = Seen(-4 - 0.1f * (frame + 1), closed: true) / open;
            if (light / before > most) (most, at) = (light / before, frame);
            before = light;
            if (fields()!.Plan!.Unbuilt is not null) waited++;
        }
        UnloadModel(block);
        waited.Should().BeGreaterThan(4, "the walk's shifts build their slabs over frames");
        most.Should().BeLessThan(1.5, $"the corridor's light holds while the slabs its walk brings in are built, where at the walk's frame {at} it rose to {most:0.00} times the frame before's, over {waited} frames a slab waited");
    }
}

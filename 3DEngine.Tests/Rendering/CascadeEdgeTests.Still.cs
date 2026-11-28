using System.Globalization;
using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A closed corridor under the sun and a lamp, the cascades walked down it in small steps as an
// eye walking it moves them, nothing else changing, and the light the surfaces gather through the
// split cascades read at fixed points on its walls, floor, ceiling and lintel while each of the
// field's cascades' ends sweeps over them.
public sealed partial class CascadeEdgeTests
{
    [NeedsRayQueryTheory]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void The_Split_Cascades_Light_At_A_Fixed_Point_Holds_While_The_Eye_Walks(GlobalIllumination quality)
    {
        // A corridor four units wide, three high and thirty-six long, closed at both ends, its
        // ceiling open over three units near its far end, where the sun reaches the floor, a lamp
        // by its right wall and a lintel across it under the ceiling. The field's three cascades of
        // cells 0.15, 0.3 and 0.6 lie around the middle the field is given, which walks six units
        // down the corridor as an eye walking it would move them, ten of the last cascade's cells
        // and forty of the first's, so each cascade's end ahead passes over the points. The picture
        // is drawn from where the walk begins, so each point stays on its pixels. Under the split
        // layout, the default, each pixel takes the light its point's split cascades gather, their
        // rays' hits lit by the world's probes, and the light past them where it takes their place
        // across the last cascade's outer band, drawn on white surfaces a term at a time. Each
        // point is read at each step against its own mean over the walk.
        const int Wide = 640, High = 360;
        var config = Config.Default.WithWindow("still corridor", Wide, High) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        SetGlobalIllumination(quality);
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        settings.AlbedoOff = true;
        settings.SpecularOff = true;
        var field = GetApp().World.Resource<SceneFieldSettings>();
        var sky = new byte[64 * 32 * 4];
        for (int i = 0; i < 64 * 32; i++)
            (sky[i * 4], sky[i * 4 + 1], sky[i * 4 + 2], sky[i * 4 + 3]) = i < 64 * 16 ? ((byte)120, (byte)165, (byte)230, (byte)255) : ((byte)90, (byte)100, (byte)110, (byte)255);
        SetEnvironmentMap(new Image(sky, 64, 32));
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.3f, -1, -0.4f)), new Color(255, 245, 225), 3, castsShadows: true);
        CreatePointLight(new Vector3(1.4f, 2.3f, -9), new Color(255, 225, 190), 4, range: 12);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var stone = new Color(150, 150, 150);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>;
        var eye = new Camera3D(new Vector3(0, 1.6f, 0), new Vector3(0, 1.3f, -10), Vector3.UnitY, 70);
        void Draw()
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(eye);
            DrawModelEx(block, new Vector3(0, -0.25f, -16), Vector3.UnitY, 0, new Vector3(5, 0.5f, 37), stone);
            DrawModelEx(block, new Vector3(-2.25f, 1.5f, -16), Vector3.UnitY, 0, new Vector3(0.5f, 3, 37), stone);
            DrawModelEx(block, new Vector3(2.25f, 1.5f, -16), Vector3.UnitY, 0, new Vector3(0.5f, 3, 37), stone);
            DrawModelEx(block, new Vector3(0, 3.25f, -9), Vector3.UnitY, 0, new Vector3(5, 0.5f, 23), stone);
            DrawModelEx(block, new Vector3(0, 3.25f, -29), Vector3.UnitY, 0, new Vector3(5, 0.5f, 11), stone);
            DrawModelEx(block, new Vector3(0, 2.7f, -14), Vector3.UnitY, 0, new Vector3(4, 0.6f, 0.6f), stone);
            DrawModelEx(block, new Vector3(0, 1.5f, 2.75f), Vector3.UnitY, 0, new Vector3(5, 3.5f, 0.5f), stone);
            DrawModelEx(block, new Vector3(0, 1.5f, -34.75f), Vector3.UnitY, 0, new Vector3(5, 3.5f, 0.5f), stone);
            EndMode3D();
            EndDrawing();
        }

        // The fixed points: the floor, both walls and the ceiling a unit apart along the corridor,
        // and the lintel's face toward the eye.
        var points = new List<(string Name, Vector3 At)>();
        for (int z = -4; z >= -22; z--)
        {
            points.Add(($"floor {z}", new Vector3(-0.8f, 0, z)));
            points.Add(($"left wall {z}", new Vector3(-2, 1.2f, z)));
            points.Add(($"right wall {z}", new Vector3(2, 1.8f, z)));
            if (z >= -12) points.Add(($"ceiling {z}", new Vector3(0.4f, 3, z)));
        }
        foreach (var x in new[] { -1.2f, 0f, 1.2f })
            points.Add(($"lintel {x}", new Vector3(x, 2.7f, -13.7f)));
        var pixels = points.Select(point => GetWorldToScreenEx(point.At, eye, Wide, High)).ToArray();

        // Each point's light, the mean of the pixels within a pixel of its place in the picture.
        double[] Read(float[] light, int stride) => [.. pixels.Select(at =>
        {
            double sum = 0;
            for (int y = (int)at.Y - 1; y <= (int)at.Y + 1; y++)
                for (int x = (int)at.X - 1; x <= (int)at.X + 1; x++)
                {
                    var p = (y * Wide + x) * stride;
                    sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
                }
            return sum / 9;
        })];
        double[] Drawn(int term)
        {
            settings.Term = term;
            Draw();
            return Read(device.ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image), 4);
        }
        // The light the surfaces gather from all around: the cascades' (term 1), and across the
        // last cascade's outer band, where that light fades out, the light past them that fades
        // in, the environment's (7) and the sun's share of the ground's (11), each drawn in a frame
        // of its own.
        double[] Indirect()
        {
            var sum = new double[points.Count];
            foreach (var term in (int[])[1, 7, 11])
            {
                var read = Drawn(term);
                for (int i = 0; i < sum.Length; i++)
                    sum[i] += read[i];
            }
            return sum;
        }

        field.Center = eye.Position;
        settings.Term = 1;
        for (int frame = 0; frame < 600; frame++)
        {
            Draw();
            if (frame > SceneFieldPlan.SettleFrames && fields()?.Plan is { Built: true, FramesBuilt: > 60 }) break;
        }
        for (int frame = 0; frame < 120; frame++)
            Draw();

        // The walk, from half a step on so that no step falls where a cascade's box snaps, with
        // each cascade's corner along the corridor at each step.
        const float Step = 0.03f;
        var steps = (int)MathF.Round(6 / Step);
        var walk = new List<double[]>();
        var corners = new List<float[]>();
        for (int step = 0; step <= steps; step++)
        {
            field.Center = eye.Position - new Vector3(0, 0, Step * (step + 0.5f));
            walk.Add(Indirect());
            corners.Add([.. Enumerable.Range(0, 3).Select(c => fields()!.Plan!.OriginAround(c, field.Center.Value).Z)]);
        }

        // At the walk's end, the light on the stone's own color against the traced reference's, the
        // whole picture's less the lights' own, which both draw alike, where the device traces it.
        var against = new double[points.Count];
        if (quality == GlobalIllumination.High)
        {
            settings.AlbedoOff = false;
            var own = Drawn(12);
            var ownReflected = Drawn(13);
            var gathered = Indirect();
            settings.Term = 0;
            Draw();
            using var folder = new TestFolder("engine-still-corridor-");
            var traced = Path.Combine(folder.Path, "corridor.png");
            BounceReference.Trace(GetApp().World, traced, 256).Should().StartWith("traced");
            var reference = Read(BounceReference.ReadPfm(traced + ".pfm").Light, 3);
            against = [.. Enumerable.Range(0, points.Count).Select(i => gathered[i] / (reference[i] - own[i] - ownReflected[i]))];
        }
        UnloadModel(block);

        // Each point's swing at each step, its light against its own mean over the walk, and the
        // steps at which a cascade's box snapped on by a spacing of its probes, which changes the
        // world's probes that light the split's hits near the cascade's end. Away from those steps
        // and the frames the probes take to settle after them, a point's light is the split's own
        // as the eye moves.
        var means = Enumerable.Range(0, points.Count).Select(i => walk.Average(read => read[i])).ToArray();
        var swings = Enumerable.Range(0, points.Count).Select(i => walk.Select(read => Math.Abs(read[i] / means[i] - 1)).ToArray()).ToArray();
        var worst = Enumerable.Range(0, points.Count).MaxBy(i => swings[i].Max());
        var worstStep = Array.IndexOf(swings[worst], swings[worst].Max());
        var sorted = swings.SelectMany(s => s).Order().ToArray();
        var ninetyNinth = sorted[(int)(0.99 * (sorted.Length - 1))];
        bool Snapped(int step, int c) => step > 0 && corners[step][c] != corners[step - 1][c];
        var snaps = Enumerable.Range(1, steps).Where(step => Enumerable.Range(0, 3).Any(c => Snapped(step, c))).ToArray();
        var apart = Enumerable.Range(1, steps).Where(step => snaps.All(snap => Math.Abs(step - snap) > 12)).ToArray();
        var (still, stillAt) = Enumerable.Range(0, points.Count)
            .SelectMany(i => apart.Select(step => (Math.Abs(walk[step][i] - walk[step - 1][i]) / means[i], i)))
            .MaxBy(change => change.Item1);
        var level = string.Join(" and ", Enumerable.Range(0, 3).Where(c => Snapped(worstStep, c)).Select(c => $"cascade {c}"));
        still.Should().BeLessThan(0.02, string.Create(CultureInfo.InvariantCulture,
            $"the split cascades' light at each point holds within a fiftieth between the cascades' snaps, where the {points[stillAt].Name}'s moved by {still:0.0000} in a step"));

        // Where a snap changes the cascade whose probes light the split's hits, the light the hits
        // send on differs, and across the last cascade's outer band the light past the cascades,
        // the sky's and the sun's on the ground, fades in as though the corridor were open. The
        // floor and the right wall at z -16 to -18 read about twice their mean until the last
        // cascade's box snaps at the walk's 80th step and under half of it after. The swing is
        // held to what it reads, 1.0064 at worst and 0.9802 at its 99th part at High, with some
        // room past them.
        var swung = string.Create(CultureInfo.InvariantCulture,
            $"the {points[worst].Name} swung by {swings[worst].Max():0.0000} at the walk's step {worstStep} as {(level.Length > 0 ? level : "no cascade")} snapped, the 99th part of all swings {ninetyNinth:0.0000}");
        swings[worst].Max().Should().BeLessThan(1.07, swung);
        ninetyNinth.Should().BeLessThan(1.04, swung);

        // A point the first two cascades hold at the walk's end reads near the traced light, so a
        // steady wrong light shows as well as a swing, from 0.755 to 1.751 of it, with some room.
        if (quality == GlobalIllumination.High)
            foreach (var i in Enumerable.Range(0, points.Count).Where(i => points[i].At.Z >= -14))
                against[i].Should().BeInRange(0.71, 1.86, string.Create(CultureInfo.InvariantCulture,
                    $"the {points[i].Name}'s light at the walk's end is near the traced reference's, {against[i]:0.000} of it"));
    }
}

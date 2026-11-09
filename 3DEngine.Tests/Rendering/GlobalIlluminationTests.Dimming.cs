using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A view's light changing over the probes' cycles of sixteen frames.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Lamp_Dimming_Slowly_Dims_The_View_Evenly_From_One_Frame_To_The_Next()
    {
        // The voxel game's room at night as the walk's test has it, white with a red wall and a
        // green one, lit by one emissive block on its floor, at High, the lamp dimmed by less a
        // frame than a change of the lights counts, so the probes' cycles carry on through it, and
        // the mean of every frame read. Each whole cycle's mean lies under the one before, and the
        // light shown eases from one to the next across the cycle after it, the world's probes'
        // and the screen's, so the view dims evenly where taken whole at each cycle's first frame
        // it stepped there and held between.
        var config = Config.Default.WithWindow("gi test", 320, 180) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var lamp = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        lamp.Materials[0].Emissive = new Color(255, 200, 120);
        void Box(Vector3 min, Vector3 max, Color color) => DrawModelEx(block, (min + max) / 2, Vector3.UnitY, 0, max - min, color);
        var white = new Color(212, 216, 217);
        var eye = new Vector3(16.5f, 6.62f, -11.8f);
        const int settle = 60, dimming = 96;
        var means = new double[dimming];
        for (int frame = 0; frame < settle + dimming; frame++)
        {
            lamp.Materials[0].EmissiveIntensity = 4 * MathF.Pow(0.994f, Math.Max(0, frame - settle));
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(eye, eye + new Vector3(0, -0.17f, -1), Vector3.UnitY, 70));
            Box(new Vector3(10, 4, -22), new Vector3(23, 5, -9), white);
            Box(new Vector3(10, 11, -22), new Vector3(23, 12, -9), white);
            Box(new Vector3(10, 5, -22), new Vector3(23, 11, -21), white);
            Box(new Vector3(10, 5, -10), new Vector3(23, 11, -9), white);
            Box(new Vector3(10, 5, -21), new Vector3(11, 11, -10), new Color(170, 35, 35));
            Box(new Vector3(22, 5, -21), new Vector3(23, 11, -10), new Color(75, 140, 40));
            DrawModelEx(lamp, new Vector3(16.5f, 5.5f, -15.5f), Vector3.UnitY, 0, Vector3.One, new Color(230, 190, 110));
            EndMode3D();
            if (frame >= settle)
            {
                var at = frame - settle;
                Screenshots.Capture(GetApp().World, (rgba, width, height) =>
                {
                    long sum = 0;
                    for (int i = 0; i < width * height * 4; i += 4) sum += rgba[i] + rgba[i + 1] + rgba[i + 2];
                    means[at] = sum / (width * height * 3.0);
                }).Should().BeNull();
            }
            EndDrawing();
        }
        // The last frames' captures come back over the frames after.
        for (int frame = 0; frame < 4; frame++)
        {
            BeginDrawing();
            EndDrawing();
        }
        UnloadModel(block);
        UnloadModel(lamp);
        CloseWindow();
        UseApp(null);

        // Each frame's fall from the one before, and the typical fall, which the lamp's own dimming
        // gives a frame, three times of which a step at a cycle's first frame passes.
        var falls = means.Zip(means.Skip(1), (a, b) => a - b).ToList();
        var typical = falls.Order().ElementAt(falls.Count / 2);
        typical.Should().BeGreaterThan(0, "the lamp dims the room");
        falls.Max().Should().BeLessThan(3 * typical,
            $"the view dims evenly, its mean {string.Join(", ", means.Select(m => $"{m:0.00}"))} levels");
    }
}

using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A threshold along a wall's foot on a floor thinner than a cell, a lamp in the open under the floor.
public sealed partial class GlobalIlluminationTests
{
    [NeedsRayQueryTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Medium, 2.39f)]
    [InlineData(GlobalIllumination.High, 2.33f)]
    public void The_Wall_Above_A_Threshold_On_A_Thin_Floor_Reads_A_Lamp_Under_The_Floor_Within_Its_Measured_Leak(GlobalIllumination quality, float bound)
    {
        // A closed room of walls 0.3 thick in cells of 0.5 on a floor 0.1 thick, its top at 0.125,
        // a bright lamp in the open under the floor and a dim one inside, and a threshold 0.1 high
        // and 0.4 deep along the east wall's foot, as Sponza's main door has. The wall a tenth to
        // four tenths above the threshold is read in linear light with the bright lamp and without
        // it. The screen's probes there meet the threshold's top on screen, and lit by the field's
        // normal, which the field turns down there, the hits took the light of the world's probes
        // under the floor, the wall 5.05 times its light without the lamp at High and 4.29 at
        // Medium on the development machine's GPU, where with the floor's top at 0 or 0.375 the
        // two normals read alike. Lit by the depth's normal the wall reads 2.02 and 2.08, and at
        // Low, which has no screen's probes, 1.51, the world's probes seeing the lamp through the
        // floor thinner than their cells as its floor reads it, 1.54. The bounds are 15% over
        // these.
        const int width = 320, height = 180;
        var config = Config.Default.WithWindow("threshold", width, height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.5f, 2);
        SetSceneFieldCenter(new Vector3(1, 1.5f, 1));
        SetGlobalIllumination(quality);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        const float Half = 0.15f, Low = -1, Up = 3, Floor = 0.125f, High = Floor + 3, Rise = 0.1f, Deep = 0.4f;
        void Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
            DrawModelEx(slab, new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2, Vector3.UnitY, 0, new Vector3(x1 - x0, y1 - y0, z1 - z0), new Color(200, 200, 200));
        var under = CreatePointLight(new Vector3(1, Floor - 0.8f, 1), Color.White, 25, range: 10, castsShadows: true);
        CreatePointLight(new Vector3(1, Floor + 2.2f, 1), Color.White, 0.6f, range: 8);
        var camera = new Camera3D(new Vector3(-0.4f, Floor + 1.6f, 2.4f), new Vector3(2.8f, Floor + 0.2f, -0.2f), Vector3.UnitY, 60);
        float Wall()
        {
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 90; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                Box(-8, 8, Floor - 2, Floor - 1.6f, -8, 8);
                Box(-8, 8, Floor - 0.1f, Floor, -8, 8);
                Box(Up - Half, Up + Half, Floor, High, Low - Half, Up + Half);
                Box(Low - Half, Low + Half, Floor, High, Low - Half, Up + Half);
                Box(Low + Half, Up - Half, Floor, High, Low - Half, Low + Half);
                Box(Low + Half, Up - Half, Floor, High, Up - Half, Up + Half);
                Box(Low - Half, Up + Half, High, High + 0.6f, Low - Half, Up + Half);
                Box(Up - Half - Deep, Up - Half, Floor, Floor + Rise, Low + Half, Up - Half);
                EndMode3D();
                EndDrawing();
            }
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            var (sum, count) = (0.0, 0);
            foreach (var place in Enumerable.Range(0, 9).SelectMany(i => new[] { 0.1f, 0.25f, 0.4f }.Select(h => new Vector3(Up - Half - 0.01f, Floor + Rise + h, -0.6f + i * 0.4f))))
            {
                var at = GetWorldToScreenEx(place, camera, width, height);
                var (x, y) = ((int)at.X, (int)at.Y);
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                var i = (y * width + x) * 4;
                sum += 0.2126 * light[i] + 0.7152 * light[i + 1] + 0.0722 * light[i + 2];
                count++;
            }
            count.Should().BeGreaterThan(8, "the wall above the threshold is in view");
            return (float)(sum / count);
        }

        var lit = Wall();
        UnloadLight(under);
        var dark = Wall();
        UnloadModel(slab);
        dark.Should().BeGreaterThan(0, "the dim lamp inside lights the wall");
        (lit / dark).Should().BeLessThan(bound, $"the wall reads {lit:0.0000} with the lamp under the floor and {dark:0.0000} without it");
    }
}

using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A wall thinner than a cell standing on a floor that runs on past it, whose bottom face lies
// buried against the floor's top.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Lamp_Outside_A_Closed_Room_Of_Thin_Walls_Lights_None_Of_Its_Floor_Along_A_Walls_Foot()
    {
        // A closed room of walls 0.3 thick in cells of 0.5, each face a tenth from the middles of
        // the cells beside it, so no cell's middle lies inside a wall, on a floor running on past
        // them, a bright lamp outside low beside the east wall and a dim one inside. Its floor along
        // the east wall's foot is read in linear light with the bright lamp and without it. The
        // splat took a face as lying behind a cell within 25 degrees of straight back, so along the
        // foot the wall's bottom face, its edge 22 degrees off straight back from the cells there,
        // lay nearer than the far face and was kept as the face behind. Looking down and not against
        // the face in front, it held nothing, those cells kept the distance in front, and the
        // probes' rays along the floor passed under the wall.
        const int width = 320, height = 180;
        var config = Config.Default.WithWindow("wall foot", width, height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.5f, 2);
        SetSceneFieldCenter(new Vector3(1, 1.5f, 1));
        SetGlobalIllumination(GlobalIllumination.Low);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        const float Half = 0.15f, High = 3, Low = -1, Up = 3;
        void Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
            DrawModelEx(slab, new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2, Vector3.UnitY, 0, new Vector3(x1 - x0, y1 - y0, z1 - z0), new Color(200, 200, 200));
        var outside = CreatePointLight(new Vector3(Up + 1.2f, 0.5f, 1), Color.White, 25, range: 10, castsShadows: true);
        CreatePointLight(new Vector3(1, 2.2f, 1), Color.White, 0.6f, range: 8);
        var camera = new Camera3D(new Vector3(-0.4f, 1.6f, 2.4f), new Vector3(2.8f, 0.2f, -0.2f), Vector3.UnitY, 60);
        float Foot()
        {
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 90; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                Box(-8, 8, -0.6f, 0, -8, 8);
                Box(Up - Half, Up + Half, 0, High, Low - Half, Up + Half);
                Box(Low - Half, Low + Half, 0, High, Low - Half, Up + Half);
                Box(Low + Half, Up - Half, 0, High, Low - Half, Low + Half);
                Box(Low + Half, Up - Half, 0, High, Up - Half, Up + Half);
                Box(Low - Half, Up + Half, High, High + 0.6f, Low - Half, Up + Half);
                EndMode3D();
                EndDrawing();
            }
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            // The floor a tenth of a unit from the east wall's inside, nine places along its foot.
            var (sum, count) = (0.0, 0);
            foreach (var place in Enumerable.Range(0, 9).Select(i => new Vector3(Up - Half - 0.1f, 0.01f, -0.6f + i * 0.4f)))
            {
                var at = GetWorldToScreenEx(place, camera, width, height);
                var (x, y) = ((int)at.X, (int)at.Y);
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                var i = (y * width + x) * 4;
                sum += 0.2126 * light[i] + 0.7152 * light[i + 1] + 0.0722 * light[i + 2];
                count++;
            }
            count.Should().BeGreaterThan(4, "the foot is in view");
            return (float)(sum / count);
        }

        var lit = Foot();
        UnloadLight(outside);
        var dark = Foot();
        UnloadModel(slab);
        dark.Should().BeGreaterThan(0, "the dim lamp inside lights the floor");
        (lit / dark).Should().BeLessThan(1.1f, $"the lamp outside lights none of the floor inside, {lit:0.0000} against {dark:0.0000}");
    }
}

using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A doorway through a thick wall between two closed rooms on a floor with the open under it.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Doorways_Jamb_In_Closed_Rooms_Takes_None_Of_The_Sky_From_The_Probes_Under_The_Floor()
    {
        // Two closed rooms on a floor 0.5 thick with its top at 3, their outer walls carried 3
        // below it with the open under them, a doorway 1.2 wide and 2.2 high through the wall 3
        // thick between them, a dim lamp in one and a bright sky around them. In cells of 0.5 the
        // first cascade's probes stand 4 apart, at 2 and 6 high and 2 either side of the doorway's
        // middle, so the eight around the jamb beside the floor all stand where it does not see
        // them, four in the rooms beside the wall and four in the open under the floor, whose
        // downward faces' rays meet nothing. Kept at a twentieth of their weight, the four under
        // the floor gave the jamb the light of the walls under the floor that the sky lights, 2.46
        // times its light under a black sky where this test reads it, though nothing of the sky
        // reaches the rooms. Left out, the jamb reads alike under both skies to the fifth place,
        // and the bound leaves 5% over that.
        const int width = 160, height = 90;
        const float Floor = 3, Half = 1.5f;
        float Jamb(bool sky)
        {
            var config = Config.Default.WithWindow("doorway", width, height) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            SetSceneField(3, 0.5f, 2);
            SetSceneFieldCenter(new Vector3(0, 1, 0));
            SetGlobalIllumination(GlobalIllumination.Low);
            GridLayout.ForTheGridsOwnReading();
            if (sky) SetAmbientLight(new Color(200, 220, 255), 1);
            else SetAmbientLight(Color.Black, 0);
            var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            slab.Materials[0] = new ModelMaterial(new Color(180, 180, 180)) { Roughness = 1 };
            void Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
                DrawModelEx(slab, new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2, Vector3.UnitY, 0, new Vector3(x1 - x0, y1 - y0, z1 - z0), Color.White);
            CreatePointLight(new Vector3(-Half - 1.5f, Floor + 1.2f, -1.5f), Color.White, 2, range: 6);
            var camera = new Camera3D(new Vector3(-Half - 2.5f, Floor + 1.2f, -2.5f), new Vector3(-Half, Floor + 0.3f, 0.6f), Vector3.UnitY, 60);
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 20; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                Box(-6, 6, Floor - 0.5f, Floor, -6, 6);
                Box(-6.5f, -6, Floor - 3, Floor + 4, -6.5f, 6.5f);
                Box(6, 6.5f, Floor - 3, Floor + 4, -6.5f, 6.5f);
                Box(-6, 6, Floor - 3, Floor + 4, -6.5f, -6);
                Box(-6, 6, Floor - 3, Floor + 4, 6, 6.5f);
                Box(-6.5f, 6.5f, Floor + 4, Floor + 4.5f, -6.5f, 6.5f);
                Box(-Half, Half, Floor, Floor + 4, -6, -0.6f);
                Box(-Half, Half, Floor, Floor + 4, 0.6f, 6);
                Box(-Half, Half, Floor + 2.2f, Floor + 4, -0.6f, 0.6f);
                EndMode3D();
                EndDrawing();
            }
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            renderer.RenderWorld.TryGet<SceneFieldRenderer>()!.Plan!.BuiltOrigin(0).Should().Be(new Vector3(-16),
                "the first cascade stands where the rooms were set against its probes");
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            var (sum, count) = (0.0, 0);
            foreach (var place in new[] { 0.1f, 0.3f, 0.6f }.SelectMany(h => new[] { 0.1f, 0.25f, 0.4f }.Select(f => new Vector3(-Half + f * 2 * Half, Floor + h, 0.59f))))
            {
                var at = GetWorldToScreenEx(place, camera, width, height);
                var (x, y) = ((int)at.X, (int)at.Y);
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                var i = (y * width + x) * 4;
                sum += 0.2126 * light[i] + 0.7152 * light[i + 1] + 0.0722 * light[i + 2];
                count++;
            }
            UnloadModel(slab);
            CloseWindow();
            count.Should().Be(9, "the jamb beside the floor is in view");
            return (float)(sum / count);
        }

        var lit = Jamb(sky: true);
        var dark = Jamb(sky: false);
        dark.Should().BeGreaterThan(0, "the lamp lights the jamb");
        (lit / dark).Should().BeLessThan(1.05f, $"the jamb reads {lit:0.00000} under the bright sky and {dark:0.00000} under a black one");
    }
}

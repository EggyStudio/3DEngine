using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The thin room of the bounce rooms at Low, where no screen probe stands.
public sealed partial class BounceRoomsTests
{
    [NeedsVulkanFact]
    public void At_Low_The_Lamp_Outside_The_Thin_Room_Lights_None_Of_Its_Floor_Along_The_Walls_Foot()
    {
        // The room of walls a tenth of a unit thick, under the field's cell of 0.15, seen from inside,
        // its floor along the back and right walls' foot read in linear light with the bright lamp
        // close outside the right wall and without it. Each pixel there judged the probes beyond the
        // walls seen, the point it judges their reach from lying within the walls' cells, and took
        // the lamp's light in triangles, until a pixel by a wall thinner than a cell read the probes
        // through the field (byThinWall in lights.slang).
        const int width = 480, height = 270;
        var config = Config.Default.WithWindow("thin room", width, height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.Low);
        using var rooms = new Rooms();
        var camera = Rooms.Views[1];
        float Foot()
        {
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 90; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                rooms.Draw();
                EndMode3D();
                EndDrawing();
            }
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            // The floor a tenth of a unit from each wall's inside, nine places along each foot.
            var places = Enumerable.Range(0, 9).SelectMany(i => new[]
            {
                new Vector3(38.4f + i * 0.4f, 0.01f, -1.9f),
                new Vector3(41.9f, 0.01f, -1.6f + i * 0.4f),
            });
            var (sum, count) = (0.0, 0);
            foreach (var place in places)
            {
                var at = GetWorldToScreenEx(place, camera, width, height);
                var (x, y) = ((int)at.X, (int)at.Y);
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                var i = (y * width + x) * 4;
                sum += 0.2126 * light[i] + 0.7152 * light[i + 1] + 0.0722 * light[i + 2];
                count++;
            }
            return (float)(sum / count);
        }

        var lit = Foot();
        UnloadLight(rooms.Outside);
        var dark = Foot();
        dark.Should().BeGreaterThan(0, "the dim lamp inside lights the floor");
        (lit / dark).Should().BeLessThan(1.1f, $"the lamp outside lights none of the floor inside, {lit:0.000} against {dark:0.000}");
    }
}

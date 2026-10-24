using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A floor and a wall along it read across the field's edge under a sky and a sun, past which the
// light from all around is a sky over a ground (distantAround in lights.slang).
public sealed partial class CascadeEdgeTests
{
    [NeedsVulkanTheory]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Floor_And_A_Wall_Along_It_Take_Their_Light_Across_The_Fields_Edge_With_No_Step(GlobalIllumination quality)
    {
        // A floor 420 units long and a wall along its left side facing +x, under a sky and a sun from
        // the right at 50 degrees, seen from above along their length out past the field's last
        // cascade, whose side ahead lies about 109 units before the eye.
        var config = Config.Default.WithWindow("field edge", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetGlobalIllumination(quality);
        SetAmbientLight(new Color(140, 170, 220), 1);
        var up = 50 * MathF.PI / 180;
        CreateDirectionalLight(new Vector3(-MathF.Cos(up), -MathF.Sin(up), 0), new Color(255, 240, 220), 1.5f, castsShadows: true);
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
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        UnloadModel(block);

        // The light at each pixel a line of points along the floor and along the wall's face falls
        // on, from 20 to 160 units out, each pixel once, in order away from the eye.
        List<(float Along, double Light)> Along(Func<float, Vector3> at)
        {
            var seen = new List<(float, double)>();
            var last = (-1, -1);
            for (float along = 20; along <= 160; along += 0.05f)
            {
                var pixel = GetWorldToScreenEx(at(along), camera, Width, Height);
                var (x, y) = ((int)pixel.X, (int)pixel.Y);
                if ((x, y) == last || x < 0 || y < 0 || x >= Width || y >= Height) continue;
                last = (x, y);
                int p = (y * Width + x) * 4;
                seen.Add((along, 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2]));
            }
            return seen;
        }
        var floor = Along(along => new Vector3(4, 0, eye.Z - along));
        var wall = Along(along => new Vector3(-10, 3, eye.Z - along));
        floor.Should().HaveCountGreaterThan(40, "the floor is seen out past the field");
        wall.Should().HaveCountGreaterThan(40, "the wall is seen out past the field");

        // The largest change from one pixel to the next along each, and the wall's light past the
        // field's last cascade against its light inside it. With the environment's light taken whole
        // past the probes the wall's largest step at High was 2.32%, where the field's last cascade
        // ends, and it read 6.3% darker past the field than inside it; as a sky over a ground, 0.88%
        // and 2.8%, the floor's 0.27% either way, since the sky's light from above is all a floor
        // takes of the term. Low's probes end 28 units out, where the wall stepped by 0.44%, so both
        // of its readings lie past them.
        (double Step, float At) Largest(List<(float Along, double Light)> line) =>
            line.Zip(line.Skip(1), (a, b) => (Math.Abs(b.Light / a.Light - 1), a.Along)).MaxBy(s => s.Item1);
        var (floorStep, floorAt) = Largest(floor);
        var (wallStep, wallAt) = Largest(wall);
        floorStep.Should().BeLessThan(0.01, $"the floor's light at {quality} changes from one pixel to the next by no step, as it did {floorAt:0} units out");
        wallStep.Should().BeLessThan(0.015, $"the wall's light at {quality} changes from one pixel to the next by no step, as it did {wallAt:0} units out");
        var inside = wall.Where(w => w.Along is > 70 and < 85).Average(w => w.Light);
        var past = wall.Where(w => w.Along is > 125 and < 140).Average(w => w.Light);
        (past / inside).Should().BeApproximately(1, 0.04, $"the wall past the field at {quality} takes the sky over it and the ground's light under it as the probes inside give it");
    }
}

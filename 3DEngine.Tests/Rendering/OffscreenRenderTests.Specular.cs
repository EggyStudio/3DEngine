using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void Small_Glossy_Spheres_Hold_Their_Highlights_As_The_Camera_Turns_By_A_Part_Of_A_Pixel()
    {
        // A row of glossy metal spheres a few pixels across, which send back their light by their
        // gloss alone, a lamp's highlight on each narrower than a pixel. Each pixel's highlight is
        // whichever way its middle's normal mirrors, so the camera turned by a quarter of a pixel
        // lights other pixels, and the highlights flicker as it turns. With the roughness widened
        // by how fast the normal turns across a pixel, each highlight spreads over the pixels the
        // curve's ways fall in, and the frames differ by far less of their light.
        var on = Shimmer(true);
        var off = Shimmer(false);
        on.Should().BeLessThan(0.5f * off, $"the widened highlights hold still, {on:0.000} of their light changing against {off:0.000} unwidened");
    }

    // How much of their light the spheres' highlights change by, on the mean over the frame, as the
    // camera turns by a quarter of a pixel, with specular antialiasing on or off.
    private float Shimmer(bool widened)
    {
        Open(128, 64);
        SetSpecularAntialiasing(widened);
        var ball = LoadModelFromMesh(GenMeshSphere(0.25f, 24, 24));
        ball.Materials[0] = ball.Materials[0] with { Roughness = 0.05f, Metallic = 1, Color = Color.White };
        CreatePointLight(new Vector3(0, 3, 4), Color.White, 40, range: 20);
        Image Frame(float aside, string name)
        {
            var camera = new Camera3D(new Vector3(aside, 1, 8), new Vector3(aside, 0, 0), Vector3.UnitY, 45);
            return Capture(() =>
            {
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                for (int k = 0; k < 8; k++)
                    DrawModel(ball, new Vector3(k - 3.5f, 0, 0), 1, Color.White);
                EndMode3D();
            }, name);
        }
        // A pixel where the spheres stand is about 0.1 units across.
        var (first, second) = (Frame(0, $"balls-{widened}-0"), Frame(0.025f, $"balls-{widened}-1"));
        double changed = 0, light = 0;
        static double Luminance(Color c) => 0.2126 * Math.Pow(c.R / 255.0, 2.2) + 0.7152 * Math.Pow(c.G / 255.0, 2.2) + 0.0722 * Math.Pow(c.B / 255.0, 2.2);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 128; x++)
            {
                var (a, b) = (Luminance(GetImageColor(first, x, y)), Luminance(GetImageColor(second, x, y)));
                changed += Math.Abs(a - b);
                light += 0.5 * (a + b);
            }
        UnloadModel(ball);
        CloseWindow();
        UseApp(null);
        return (float)(changed / Math.Max(light, 1e-9));
    }
}

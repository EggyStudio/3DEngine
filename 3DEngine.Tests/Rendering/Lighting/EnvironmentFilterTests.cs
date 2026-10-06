using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering.Lighting;

/// <summary>An environment map filtered on the GPU into its cube and irradiance, read back from the renderer.</summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class EnvironmentFilterTests : IDisposable
{
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // The environment's cube and irradiance once a frame has filtered it.
    private (FilteredCube Cube, GraphicsDevice Device) Filtered(EnvironmentMap map)
    {
        var config = Config.Default.WithWindow("environment filter test", 32, 32) with { Headless = true, Offscreen = true };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        GetApp().World.InsertResource(map);
        BeginDrawing();
        ClearBackground(Color.Black);
        EndDrawing();

        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var cube = renderer.RenderWorld.Get<ModelRenderer>().Environment;
        cube.Should().NotBeNull("the frame that first sees a map filters it");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the filter");
        return (cube!, (GraphicsDevice)renderer.Context.Graphics!);
    }

    // The texel at the middle of a face of a mip.
    private static Vector3 Middle(GraphicsDevice device, FilteredCube cube, int mip, int face)
    {
        var texels = device.ReadCubeFaces(cube, (uint)mip);
        int s = (int)Math.Max(1, cube.Size >> mip);
        int at = ((face * s + s / 2) * s + s / 2) * 4;
        return new Vector3((float)texels[at], (float)texels[at + 1], (float)texels[at + 2]);
    }

    // The light a white diffuse surface facing a normal returns, from the nine coefficients.
    private static Vector3 IrradianceAt(Vector3[] c, Vector3 n)
    {
        n = Vector3.Normalize(n);
        float[] y =
        [
            0.282095f, 0.488603f * n.Y, 0.488603f * n.Z, 0.488603f * n.X, 1.092548f * n.X * n.Y,
            1.092548f * n.Y * n.Z, 0.315392f * (3 * n.Z * n.Z - 1), 1.092548f * n.X * n.Z, 0.546274f * (n.X * n.X - n.Y * n.Y),
        ];
        var sum = Vector3.Zero;
        for (int i = 0; i < 9; i++) sum += c[i] * y[i];
        return Vector3.Max(sum, Vector3.Zero);
    }

    // The mean light over every direction of a mip, each texel weighted by the solid angle it covers.
    private static float MeanLight(GraphicsDevice device, FilteredCube cube, int mip)
    {
        var texels = device.ReadCubeFaces(cube, (uint)mip);
        int s = (int)Math.Max(1, cube.Size >> mip);
        double total = 0, weights = 0;
        for (int face = 0; face < 6; face++)
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = (x + 0.5f) / s * 2 - 1, v = (y + 0.5f) / s * 2 - 1;
            var weight = 1 / MathF.Pow(1 + u * u + v * v, 1.5f);
            var at = ((face * s + y) * s + x) * 4;
            total += weight * ((float)texels[at] + (float)texels[at + 1] + (float)texels[at + 2]) / 3;
            weights += weight;
        }
        return (float)(total / weights);
    }

    [NeedsVulkanFact]
    public void A_Uniform_Sky_Is_Its_Light_At_Every_Mip_And_Lights_A_Diffuse_Surface_Alike_Whichever_Way_It_Faces()
    {
        var (cube, device) = Filtered(EnvironmentMap.FromEquirectangular(GenImageColor(64, 32, new Color(0, 188, 0)), faceSize: 16));

        cube.MipLevels.Should().Be(5);
        for (int mip = 0; mip < cube.MipLevels; mip++)
            Middle(device, cube, mip, 4).Y.Should().BeApproximately(0.5f, 0.01f, "sRGB 188 is half the light, however rough");
        var irradiance = device.ReadCubeIrradiance(cube);
        foreach (var normal in new[] { Vector3.UnitY, -Vector3.UnitY, Vector3.UnitX, new Vector3(1, 1, -1) })
            IrradianceAt(irradiance, normal).Y.Should().BeApproximately(0.5f, 0.01f, "the whole sky's light, weighted by the cosine and divided by pi, is the sky's own");
    }

    [NeedsVulkanFact]
    public void A_Sky_Lit_From_Above_Lights_What_Faces_Up_And_Half_Lights_What_Faces_Sideways()
    {
        // White above the horizon and black below, so a surface facing up sees all of the light,
        // one facing sideways half of it and one facing down none, which three bands come close to.
        var data = new byte[64 * 32 * 4];
        for (int i = 0; i < 64 * 16; i++) data[i * 4] = data[i * 4 + 1] = data[i * 4 + 2] = 255;
        for (int i = 0; i < 64 * 32; i++) data[i * 4 + 3] = 255;
        var (cube, device) = Filtered(EnvironmentMap.FromEquirectangular(new Image(data, 64, 32), faceSize: 16));

        var irradiance = device.ReadCubeIrradiance(cube);
        IrradianceAt(irradiance, Vector3.UnitY).X.Should().BeApproximately(1f, 0.1f);
        IrradianceAt(irradiance, Vector3.UnitZ).X.Should().BeApproximately(0.5f, 0.05f);
        IrradianceAt(irradiance, -Vector3.UnitY).X.Should().BeApproximately(0f, 0.1f);
    }

    [NeedsVulkanFact]
    public void The_Top_Of_The_Image_Is_Up_And_Roughness_Blurs_Toward_The_Horizon()
    {
        // Red above the horizon and blue below.
        var data = new byte[64 * 32 * 4];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 64; x++)
        {
            var at = (y * 64 + x) * 4;
            data[at] = (byte)(y < 16 ? 255 : 0);
            data[at + 2] = (byte)(y < 16 ? 0 : 255);
            data[at + 3] = 255;
        }
        var (cube, device) = Filtered(EnvironmentMap.FromEquirectangular(new Image(data, 64, 32), faceSize: 32));

        Middle(device, cube, 0, 2).X.Should().BeApproximately(1, 0.01f, "straight up, the +Y face, is the top row, red");
        Middle(device, cube, 0, 3).Z.Should().BeApproximately(1, 0.01f, "straight down is blue");
        var rough = Middle(device, cube, (int)cube.MipLevels - 1, 4);
        (rough.X > 0.2f && rough.Z > 0.2f).Should().BeTrue("a rough surface facing the horizon sees both halves");
    }

    [NeedsVulkanFact]
    public void An_HDR_Sun_Keeps_Its_Light_And_Rough_Surfaces_Facing_It_Gather_Mostly_Sun()
    {
        var file = EnvironmentMapTests.Hdr(16, 8, y => y < 4 ? new Vector3(20, 18, 16) : new Vector3(0.05f));
        var (cube, device) = Filtered(EnvironmentMap.FromHdrFile(file, faceSize: 16));

        Middle(device, cube, 0, 2).X.Should().BeApproximately(20, 0.5f, "straight up is the sun, at its own brightness");
        Middle(device, cube, 0, 3).X.Should().BeApproximately(0.05f, 0.01f, "straight down is the ground");
        Middle(device, cube, (int)cube.MipLevels - 1, 2).X.Should().BeGreaterThan(5, "a rough surface facing up still gathers mostly sun");
    }

    // An image of a dim sky with a bright cap of directions within 0.2 radians of one, each pixel
    // lit by where its middle looks.
    private static EnvironmentMap Cap(Vector3 around)
    {
        const int Width = 256, Height = 128;
        var pixels = new Vector3[Width * Height];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            float theta = (y + 0.5f) / Height * MathF.PI, phi = ((x + 0.5f) / Width - 0.5f) * 2 * MathF.PI;
            var d = new Vector3(MathF.Sin(theta) * MathF.Sin(phi), MathF.Cos(theta), -MathF.Sin(theta) * MathF.Cos(phi));
            pixels[y * Width + x] = Vector3.Dot(d, around) > MathF.Cos(0.2f) ? new Vector3(20) : new Vector3(0.2f);
        }
        return EnvironmentMap.FromLinear(pixels, Width, Height, faceSize: 32);
    }

    [NeedsVulkanFact]
    public void A_Light_Weighs_The_Same_At_A_Pole_As_On_The_Horizon_At_Every_Mip()
    {
        // The rows near a pole hold one direction many times over, which a filter of the image's
        // own mips would weigh by their length, so a cap at the zenith would grow in each rougher
        // mip where the same cap on the horizon would not.
        var (zenith, device) = Filtered(Cap(Vector3.UnitY));
        var overhead = Enumerable.Range(0, (int)zenith.MipLevels).Select(mip => MeanLight(device, zenith, mip)).ToArray();
        CloseWindow();
        UseApp(null);
        var (horizon, again) = Filtered(Cap(-Vector3.UnitZ));
        var level = Enumerable.Range(0, (int)horizon.MipLevels).Select(mip => MeanLight(again, horizon, mip)).ToArray();

        for (int mip = 0; mip < overhead.Length; mip++)
            overhead[mip].Should().BeApproximately(level[mip], 0.05f * level[mip],
                $"mip {mip} holds as much of the cap overhead as on the horizon ({string.Join(", ", overhead.Select(m => m.ToString("0.###")))} against {string.Join(", ", level.Select(m => m.ToString("0.###")))})");
    }
}

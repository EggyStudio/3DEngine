using System.Numerics;
using FluentAssertions;
using Xunit.Abstractions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The circular occluders as they draw, far from the world's origin as at it, what they hold when
/// switched away, and the commands that save their pictures.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class CircularOccludersRenderTests(ITestOutputHelper output) : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [NeedsVulkanFact]
    public void A_Room_Far_From_The_Origin_Is_Occluded_As_The_Same_Room_At_It()
    {
        // A room far from the origin read against the same room at it through the same frames, the
        // circular occluders' own result and the occlusion after the history and the blurs alike
        // but for a few texels, since each pass works against the frame's origin and the eye. The
        // distance along the view and the normal the main pass read from its packed image are
        // said with them, and the room at the origin drawn a second time, which is drawn alike to
        // the texel, the sky's texels that the main pass leaves read open. Placed against the
        // other origin the depth rounds the other way by a step of the packed distance at some
        // texels, and a few normals fall the other way of the packing's dithered five bits, the
        // occlusion with them.
        var near = Occlusion(Vector3.Zero);
        var again = Occlusion(Vector3.Zero);
        var far = Occlusion(new Vector3(4448, 24, 600));
        var found = $"the far room against the near, {Apart(near, far)}; the near room drawn again, {Apart(near, again)}";
        output.WriteLine(found);

        var sky = Enumerable.Range(0, near.Depth.Length).Where(i => near.Depth[i] == 0).ToArray();
        sky.Should().NotBeEmpty("the room is open to the sky past its walls");
        sky.Count(i => near.Raw[i] != 1).Should().Be(0, "a texel of the sky, which the main pass leaves, reads open");
        (Parted(near.Raw, again.Raw), Parted(near.Final, again.Final)).Should().Be((0, 0), "the same room drawn twice is drawn alike, {0}", found);
        Parted(near.Raw, far.Raw).Should().BeLessThanOrEqualTo(72, "the circles are read about the eye wherever the room stands, {0}", found);
        Parted(near.Final, far.Final).Should().BeLessThanOrEqualTo(16, "and the history and the blurs keep them so, {0}", found);
        near.Final.Average(v => (double)v).Should().BeLessThan(0.97, "the room's corners and the blocks on its floor close some light off");
    }

    // What a room's drawing leaves, the circular occluders' result at 16 samples, raw and final,
    // each texel's share of the light from all around, and the distance along the view and the
    // normal the main pass read, after the same frames.
    private sealed record Drawn(float[] Raw, float[] Final, float[] Depth, Vector3[] Normal);

    private static Drawn Occlusion(Vector3 at)
    {
        Open();
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        Room(block, at, 30);
        var (device, occlusion) = Parts();
        var raw = device.ReadPixels(occlusion.CircularPart!.Raw!);
        var final = device.ReadPixels(occlusion.WindowOcclusion!);
        var packed = occlusion.CircularPart.ReadPacked()!;
        UnloadModel(block);
        return new Drawn(Channel(raw, 3), Channel(final, 0), [.. packed.Select(Distance)], [.. packed.Select(Normal)]);
    }

    // The prepass's packing, the distance along the view a float with its low ten bits the
    // normal's, five a component of its sphere map, each signed.
    private const int NormalBits = 10;

    private static float Distance(uint packed) => BitConverter.UInt32BitsToSingle(packed & ~((1u << NormalBits) - 1));

    private static Vector3 Normal(uint packed)
    {
        const int bits = NormalBits / 2;
        const float most = ((1 << bits) - 1) >> 1;
        float Component(int shift) => ((int)(packed << (32 - bits - shift)) >> (32 - bits)) / most;
        var (x, y) = (Component(0), Component(bits));
        var z = 1 - x * x - y * y;
        var root = MathF.Sqrt(Math.Max(z, 0));
        return new Vector3(x * 2 * root, y * 2 * root, z * 2 - 1);
    }

    // Each stage's texels more than 0.05 apart, of all, and the largest difference.
    private static string Apart(Drawn a, Drawn b)
    {
        string Of(IReadOnlyList<double> differences) =>
            $"{differences.Count(d => d > 0.05)} of {differences.Count} more than 0.05 apart, the most {differences.Max():0.#####}";
        double[] Each(float[] x, float[] y) => [.. Enumerable.Range(0, x.Length).Select(i => (double)Math.Abs(x[i] - y[i]))];
        return $"raw {Of(Each(a.Raw, b.Raw))}, final {Of(Each(a.Final, b.Final))}, distance {Of(Each(a.Depth, b.Depth))}, "
            + $"normal {Of([.. Enumerable.Range(0, a.Normal.Length).Select(i => (double)Vector3.Distance(a.Normal[i], b.Normal[i]))])}";
    }

    [NeedsVulkanFact]
    public void Their_Images_And_The_Sets_That_Read_Them_Go_When_The_Method_Is_Switched_Away_Or_The_Occlusion_Is_Off()
    {
        // Chosen, switched to the horizons and chosen again twice, the frames in flight past each
        // change, then the occlusion turned off with them chosen.
        Open();
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var settle = GpuTextures.RetireFrames + 4;
        Room(block, Vector3.Zero, settle);
        var (_, occlusion) = Parts();
        var circular = occlusion.CircularPart!;
        (circular.Holds, occlusion.CircledSets).Should().Be((true, 1), "chosen, they hold their images and the window reads them through one set");

        for (int turn = 0; turn < 2; turn++)
        {
            SetAmbientOcclusionMethod(AmbientOcclusionMethod.Horizons);
            Room(block, Vector3.Zero, settle);
            (circular.Holds, occlusion.CircledSets).Should().Be((false, 0), "switched away, they hold no image and the window no set reading one");
            SetAmbientOcclusionMethod(AmbientOcclusionMethod.CircularOccluders, 16);
            Room(block, Vector3.Zero, settle);
            (circular.Holds, occlusion.CircledSets).Should().Be((true, 1), "chosen again, they hold as much as the first time");
        }

        SetAmbientOcclusion(0);
        Room(block, Vector3.Zero, settle);
        circular.Raw.Should().BeNull("with the occlusion off they hold no image");
        (circular.Holds, occlusion.CircledSets).Should().Be((false, 0), "and none they let go is left with them");
        UnloadModel(block);
    }

    [NeedsVulkanFact]
    public void Their_Filters_Picture_Is_Not_Saved_While_The_Filter_Is_Off()
    {
        using var folder = new TestFolder("occluders-save-");
        Open();
        SetAmbientOcclusionMethod(AmbientOcclusionMethod.CircularOccluders, 16);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        Room(block, Vector3.Zero, 4);
        var filtered = Path.Combine(folder.Path, "filtered.png");
        Run($"ao.save {filtered} filtered").Should().StartWith("saved filtered");
        File.Delete(filtered);

        Run("ao.filter off");
        Room(block, Vector3.Zero, 4);
        Run($"ao.save {filtered} filtered").Should().Contain("filter is off", "no pass writes the filter's image while it is off");
        File.Exists(filtered).Should().BeFalse();
        Run($"ao.save {Path.Combine(folder.Path, "raw.png")} raw").Should().StartWith("saved raw", "the main pass's own still is");
        UnloadModel(block);
    }

    private static void Open()
    {
        CloseWindow();
        UseApp(null);
        var config = Config.Default.WithWindow("occluders test", 320, 180) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetAmbientOcclusion(1, 1);
        SetAmbientOcclusionMethod(AmbientOcclusionMethod.CircularOccluders, 16);
    }

    // A room of blocks with two walls and three blocks on its floor at at, drawn for frames frames,
    // the camera's eye and the point it looks at eighths of a unit from at, which a float holds at
    // 4448 as at nothing, so the camera stands where it stands in the room at the origin.
    private static void Room(Model block, Vector3 at, int frames)
    {
        var camera = new Camera3D(at + new Vector3(3.625f, 2.375f, 3.75f), at + new Vector3(-1, 0.625f, -1.25f), Vector3.UnitY, 65);
        for (int frame = 0; frame < frames; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            for (int x = -3; x <= 3; x++)
            for (int z = -3; z <= 3; z++)
                DrawModel(block, at + new Vector3(x, -0.5f, z), 1, Color.White);
            for (int y = 0; y < 3; y++)
            for (int i = -3; i <= 3; i++)
            {
                DrawModel(block, at + new Vector3(i, y + 0.5f, -3), 1, Color.White);
                DrawModel(block, at + new Vector3(-3, y + 0.5f, i), 1, Color.White);
            }
            DrawModel(block, at + new Vector3(0, 0.5f, 0), 1, Color.White);
            DrawModel(block, at + new Vector3(-1, 0.5f, -2), 1, Color.White);
            DrawModel(block, at + new Vector3(-1, 1.5f, -2), 1, Color.White);
            EndMode3D();
            EndDrawing();
        }
    }

    private static (GraphicsDevice Device, AmbientOcclusionRenderer Occlusion) Parts()
    {
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        return ((GraphicsDevice)renderer.Context.Graphics!, renderer.RenderWorld.Get<AmbientOcclusionRenderer>());
    }

    private static string Run(string line)
    {
        using (ConsoleHost.Lend(GetApp().World, GetApp()))
            return ConsoleCommands.Run(line) ?? "";
    }

    private static float[] Channel(byte[] pixels, int channel) =>
        [.. Enumerable.Range(0, pixels.Length / 4).Select(i => pixels[i * 4 + channel] / 255f)];

    // The texels of a and b more than 0.05 apart.
    private static int Parted(float[] a, float[] b)
    {
        a.Length.Should().Be(b.Length);
        return Enumerable.Range(0, a.Length).Count(i => Math.Abs(a[i] - b[i]) > 0.05);
    }
}
